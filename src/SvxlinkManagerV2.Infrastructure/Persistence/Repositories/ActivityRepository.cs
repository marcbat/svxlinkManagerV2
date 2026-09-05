using LanguageExt;
using Microsoft.EntityFrameworkCore;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Common;
using SvxlinkManagerV2.Domain.Statistics;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository EF Core de l'historique d'activité.
///
/// Toutes les lectures d'événements sont des regroupements exécutés par SQLite : la table
/// atteint plusieurs dizaines de milliers de lignes sur une machine à 512 Mo, la charger
/// en mémoire pour agréger n'est pas une option. Seules les sessions, qui se comptent en
/// unités par jour, sont rendues telles quelles.
/// </summary>
public class ActivityRepository : IActivityRepository
{
    private readonly SvxlinkDbContext _context;

    public ActivityRepository(SvxlinkDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Validation<Error, Unit>> AddEventAsync(
        ActivityEvent activityEvent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _context.ActivityEvents.Add(activityEvent);
            await _context.SaveChangesAsync(cancellationToken);
            return unit.ToSuccess();
        }
        catch (Exception ex)
        {
            return Error.Validation("ACTIVITY_WRITE_ERROR",
                    $"Erreur lors de l'enregistrement d'un événement d'activité : {ex.Message}")
                .ToFailure<Unit>();
        }
    }

    public async Task<Validation<Error, Unit>> StartSessionAsync(
        SalonSession session,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // La session précédente se termine là où la nouvelle commence : la chronologie
            // reste continue et deux sessions ne se recouvrent jamais.
            await CloseOpenSessionsInternalAsync(session.StartedAt, false, cancellationToken);

            _context.SalonSessions.Add(session);

            // Un seul SaveChanges, donc une seule transaction : jamais deux sessions ouvertes.
            await _context.SaveChangesAsync(cancellationToken);
            return unit.ToSuccess();
        }
        catch (Exception ex)
        {
            return Error.Validation("ACTIVITY_SESSION_ERROR",
                    $"Erreur lors de l'ouverture d'une session d'activité : {ex.Message}")
                .ToFailure<Unit>();
        }
    }

    public async Task<Validation<Error, Unit>> CloseOpenSessionsAsync(
        DateTimeOffset endedAt,
        bool closedOnRecovery,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await CloseOpenSessionsInternalAsync(endedAt, closedOnRecovery, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return unit.ToSuccess();
        }
        catch (Exception ex)
        {
            return Error.Validation("ACTIVITY_SESSION_ERROR",
                    $"Erreur lors de la clôture des sessions d'activité : {ex.Message}")
                .ToFailure<Unit>();
        }
    }

    private async Task CloseOpenSessionsInternalAsync(
        DateTimeOffset endedAt,
        bool closedOnRecovery,
        CancellationToken cancellationToken)
    {
        var open = await _context.SalonSessions
            .Where(s => s.EndedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var session in open)
            session.Close(endedAt, closedOnRecovery);
    }

    public async Task<DateTimeOffset?> GetLastActivityAtAsync(CancellationToken cancellationToken = default)
    {
        var lastEvent = await _context.ActivityEvents
            .OrderByDescending(e => e.OccurredAt)
            .Select(e => (DateTimeOffset?)e.OccurredAt)
            .FirstOrDefaultAsync(cancellationToken);

        // Une session close renseigne aussi une borne de vie : c'est le seul repère
        // disponible quand aucun événement n'a été produit de toute la session.
        var lastSession = await _context.SalonSessions
            .Where(s => s.EndedAt != null)
            .OrderByDescending(s => s.EndedAt)
            .Select(s => s.EndedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return Latest(lastEvent, lastSession);
    }

    public async Task<DateTimeOffset?> GetFirstActivityAtAsync(CancellationToken cancellationToken = default)
    {
        var firstEvent = await _context.ActivityEvents
            .OrderBy(e => e.OccurredAt)
            .Select(e => (DateTimeOffset?)e.OccurredAt)
            .FirstOrDefaultAsync(cancellationToken);

        var firstSession = await _context.SalonSessions
            .OrderBy(s => s.StartedAt)
            .Select(s => (DateTimeOffset?)s.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (firstEvent is null)
            return firstSession;

        if (firstSession is null)
            return firstEvent;

        return firstEvent < firstSession ? firstEvent : firstSession;
    }

    public async Task<bool> HasAnyEventAsync(
        ActivityEventType type,
        CancellationToken cancellationToken = default)
        => await _context.ActivityEvents.AnyAsync(e => e.Type == type, cancellationToken);

    public async Task<IReadOnlyList<SalonSession>> GetSessionsAsync(
        DateTimeOffset fromUtc,
        CancellationToken cancellationToken = default)
    {
        var from = fromUtc.ToUniversalTime();

        return await _context.SalonSessions
            // Une session commencée avant la fenêtre mais close à l'intérieur — ou toujours
            // ouverte — recouvre la période et doit être comptée au prorata par l'appelant.
            .Where(s => s.EndedAt == null || s.EndedAt >= from)
            .OrderBy(s => s.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ActivityEventSummary>> GetEventSummariesAsync(
        DateTimeOffset fromUtc,
        CancellationToken cancellationToken = default)
    {
        var from = fromUtc.ToUniversalTime();

        var rows = await _context.ActivityEvents
            .Where(e => e.OccurredAt >= from)
            .GroupBy(e => e.Type)
            .Select(g => new
            {
                Type = g.Key,
                Count = g.Count(),
                TotalSeconds = g.Sum(e => (long)(e.DurationSeconds ?? 0)),
                MaxSeconds = g.Max(e => e.DurationSeconds ?? 0)
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new ActivityEventSummary(r.Type, r.Count, r.TotalSeconds, r.MaxSeconds))
            .ToList()
            .AsReadOnly();
    }

    public async Task<IReadOnlyList<SalonEventSummary>> GetSalonEventSummariesAsync(
        DateTimeOffset fromUtc,
        ActivityEventType type,
        CancellationToken cancellationToken = default)
    {
        var from = fromUtc.ToUniversalTime();

        var rows = await _context.ActivityEvents
            .Where(e => e.OccurredAt >= from && e.Type == type)
            .GroupBy(e => new { e.SalonId, e.SalonName })
            .Select(g => new
            {
                g.Key.SalonId,
                g.Key.SalonName,
                Count = g.Count(),
                TotalSeconds = g.Sum(e => (long)(e.DurationSeconds ?? 0))
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new SalonEventSummary(r.SalonId, r.SalonName, r.Count, r.TotalSeconds))
            .ToList()
            .AsReadOnly();
    }

    public async Task<IReadOnlyList<CallsignSummary>> GetTopCallsignsAsync(
        DateTimeOffset fromUtc,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var from = fromUtc.ToUniversalTime();

        var rows = await _context.ActivityEvents
            .Where(e => e.OccurredAt >= from
                        && e.Type == ActivityEventType.TalkerHeard
                        && e.Callsign != null)
            .GroupBy(e => e.Callsign!)
            .Select(g => new
            {
                Callsign = g.Key,
                Count = g.Count(),
                TotalSeconds = g.Sum(e => (long)(e.DurationSeconds ?? 0)),
                LastHeardAt = g.Max(e => e.OccurredAt)
            })
            .OrderByDescending(r => r.TotalSeconds)
            .ThenByDescending(r => r.Count)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new CallsignSummary(r.Callsign, r.Count, r.TotalSeconds, r.LastHeardAt))
            .ToList()
            .AsReadOnly();
    }

    public async Task<int> GetDistinctCallsignCountAsync(
        DateTimeOffset fromUtc,
        CancellationToken cancellationToken = default)
    {
        var from = fromUtc.ToUniversalTime();

        return await _context.ActivityEvents
            .Where(e => e.OccurredAt >= from
                        && e.Type == ActivityEventType.TalkerHeard
                        && e.Callsign != null)
            .Select(e => e.Callsign)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DtmfCodeSummary>> GetDtmfSummariesAsync(
        DateTimeOffset fromUtc,
        CancellationToken cancellationToken = default)
    {
        var from = fromUtc.ToUniversalTime();

        var rows = await _context.ActivityEvents
            .Where(e => e.OccurredAt >= from
                        && e.Type == ActivityEventType.DtmfCommand
                        && e.Detail != null)
            .GroupBy(e => e.Detail!)
            .Select(g => new
            {
                Code = g.Key,
                Count = g.Count(),
                LastUsedAt = g.Max(e => e.OccurredAt)
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new DtmfCodeSummary(r.Code, r.Count, r.LastUsedAt))
            .ToList()
            .AsReadOnly();
    }

    public async Task<IReadOnlyList<HourlyActivityCell>> GetHourlyActivityAsync(
        DateTimeOffset fromUtc,
        CancellationToken cancellationToken = default)
    {
        var from = fromUtc.ToUniversalTime();

        var rows = await _context.ActivityEvents
            .Where(e => e.OccurredAt >= from && e.Type == ActivityEventType.TalkerHeard)
            // L'heure locale est figée à l'écriture : le regroupement porte sur deux entiers,
            // sans conversion de fuseau qu'SQLite ne saurait pas faire.
            .GroupBy(e => new { e.LocalDayOfWeek, e.LocalHour })
            .Select(g => new
            {
                g.Key.LocalDayOfWeek,
                g.Key.LocalHour,
                Count = g.Count(),
                TotalSeconds = g.Sum(e => (long)(e.DurationSeconds ?? 0))
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new HourlyActivityCell(r.LocalDayOfWeek, r.LocalHour, r.Count, r.TotalSeconds))
            .ToList()
            .AsReadOnly();
    }

    public async Task<IReadOnlyList<ActivityEvent>> GetRecentEventsAsync(
        DateTimeOffset fromUtc,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var from = fromUtc.ToUniversalTime();

        return await _context.ActivityEvents
            .Where(e => e.OccurredAt >= from)
            .OrderByDescending(e => e.OccurredAt)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);
    }

    public async Task<Validation<Error, int>> PurgeBeforeAsync(
        DateTimeOffset cutoffUtc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cutoff = cutoffUtc.ToUniversalTime();

            var events = await _context.ActivityEvents
                .Where(e => e.OccurredAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);

            // Une session encore ouverte n'est jamais purgée, si ancienne soit-elle :
            // elle décrit l'état courant du nœud.
            var sessions = await _context.SalonSessions
                .Where(s => s.EndedAt != null && s.EndedAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);

            return (events + sessions).ToSuccess();
        }
        catch (Exception ex)
        {
            return Error.Validation("ACTIVITY_PURGE_ERROR",
                    $"Erreur lors de la purge de l'historique d'activité : {ex.Message}")
                .ToFailure<int>();
        }
    }

    public async Task<Validation<Error, Unit>> ResetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.ActivityEvents.ExecuteDeleteAsync(cancellationToken);
            await _context.SalonSessions.ExecuteDeleteAsync(cancellationToken);
            return unit.ToSuccess();
        }
        catch (Exception ex)
        {
            return Error.Validation("ACTIVITY_RESET_ERROR",
                    $"Erreur lors de la remise à zéro de l'historique d'activité : {ex.Message}")
                .ToFailure<Unit>();
        }
    }

    private static DateTimeOffset? Latest(DateTimeOffset? left, DateTimeOffset? right)
    {
        if (left is null)
            return right;

        if (right is null)
            return left;

        return left > right ? left : right;
    }
}
