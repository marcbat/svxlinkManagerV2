using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SvxlinkManagerV2.Domain.Aggregates.AudioConfiguration;
using SvxlinkManagerV2.Domain.Aggregates.GeneralConfiguration;
using SvxlinkManagerV2.Domain.Aggregates.Reflector;
using SvxlinkManagerV2.Domain.Aggregates.SA818;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Statistics;

namespace SvxlinkManagerV2.Infrastructure.Persistence;

public class SvxlinkDbContext : IdentityDbContext<IdentityUser>
{
    public SvxlinkDbContext(DbContextOptions<SvxlinkDbContext> options) : base(options) { }

    public DbSet<SalonAggregate> Salons => Set<SalonAggregate>();
    public DbSet<SA818Aggregate> SA818 => Set<SA818Aggregate>();
    public DbSet<ReflectorAggregate> Reflectors => Set<ReflectorAggregate>();
    public DbSet<GeneralConfigurationAggregate> GeneralConfigurations => Set<GeneralConfigurationAggregate>();
    public DbSet<AudioConfigurationAggregate> AudioConfigurations => Set<AudioConfigurationAggregate>();

    /// <summary>Périodes passées sur un salon ou en mode autonome.</summary>
    public DbSet<SalonSession> SalonSessions => Set<SalonSession>();

    /// <summary>Événements ponctuels de l'historique d'activité (table en ajout seul).</summary>
    public DbSet<ActivityEvent> ActivityEvents => Set<ActivityEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<SalonAggregate>().Ignore(e => e.DomainEvents);
        modelBuilder.Entity<SA818Aggregate>().Ignore(e => e.DomainEvents);
        modelBuilder.Entity<ReflectorAggregate>().Ignore(e => e.DomainEvents);
        modelBuilder.Entity<GeneralConfigurationAggregate>().Ignore(e => e.DomainEvents);
        modelBuilder.Entity<AudioConfigurationAggregate>().Ignore(e => e.DomainEvents);

        modelBuilder.Entity<SalonAggregate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name);
            entity.Property(e => e.IsDefault);
            entity.Property(e => e.IsDeleted);
            entity.Property(e => e.DtmfCode);
            entity.Property(e => e.SalonType);
            entity.OwnsOne(e => e.Configuration, cfg => cfg.ToJson());
        });

        modelBuilder.Entity<SA818Aggregate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Volume);
            entity.Property(e => e.Squelch);
            entity.Property(e => e.Bandwidth);
            entity.Property(e => e.PreEmph);
            entity.Property(e => e.HighPass);
            entity.Property(e => e.LowPass);
        });

        modelBuilder.Entity<ReflectorAggregate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name);
            entity.Property(e => e.Config);
            entity.Property(e => e.IsDeleted);
        });

        modelBuilder.Entity<GeneralConfigurationAggregate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StartReflectorOnStartup);
            entity.Property(e => e.StartDefaultSalonOnStartup);
            entity.Property(e => e.DefaultRxFrequency);
            entity.Property(e => e.DefaultTxFrequency);
        });

        modelBuilder.Entity<AudioConfigurationAggregate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CaptureControl);
            entity.Property(e => e.CaptureLevel);
            entity.Property(e => e.PlaybackControl);
            entity.Property(e => e.PlaybackLevel);
        });

        modelBuilder.Entity<SalonSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SalonName).IsRequired();
            entity.Property(e => e.StartedAt).HasConversion(UtcInstantConverter);
            entity.Property(e => e.EndedAt).HasConversion(NullableUtcInstantConverter);

            // Toutes les lectures partent d'une borne de temps ; la session encore ouverte
            // est retrouvée par EndedAt null, que cet index couvre aussi.
            entity.HasIndex(e => e.EndedAt);
            entity.HasIndex(e => e.StartedAt);
        });

        modelBuilder.Entity<ActivityEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OccurredAt).HasConversion(UtcInstantConverter);

            // Table la plus volumineuse de la base : chaque agrégation filtre sur la date
            // puis regroupe par nature, d'où l'index composite.
            entity.HasIndex(e => new { e.OccurredAt, e.Type });
            entity.HasIndex(e => e.Callsign);
        });
    }

    /// <summary>
    /// Stocke un <see cref="DateTimeOffset"/> sous forme de <see cref="DateTime"/> UTC.
    ///
    /// Le fournisseur SQLite refuse tout <c>ORDER BY</c>, <c>MAX</c> ou comparaison sur un
    /// <see cref="DateTimeOffset"/> — « SQLite does not support expressions of type
    /// 'DateTimeOffset' » — alors que l'historique d'activité ne fait que trier et borner par
    /// date. La conversion règle le problème sans rien changer au modèle métier ; les
    /// horodatages y sont déjà normalisés en UTC, l'offset perdu est toujours zéro.
    /// </summary>
    private static readonly ValueConverter<DateTimeOffset, DateTime> UtcInstantConverter = new(
        value => value.UtcDateTime,
        value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)));

    /// <summary>Variante de <see cref="UtcInstantConverter"/> pour les dates facultatives.</summary>
    private static readonly ValueConverter<DateTimeOffset?, DateTime?> NullableUtcInstantConverter = new(
        value => value.HasValue ? value.Value.UtcDateTime : null,
        value => value.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc))
            : null);
}
