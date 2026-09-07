using SvxlinkManagerV2.Domain.Aggregates.GeneralConfiguration.Entities;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.GeneralConfiguration.Events;

/// <summary>
/// Événement émis lors de la mise à jour de la configuration générale.
/// </summary>
public record GeneralConfigurationUpdated : DomainEvent
{
    public bool StartReflectorOnStartup { get; init; }
    public bool StartDefaultSalonOnStartup { get; init; }
    public decimal DefaultRxFrequency { get; init; }
    public decimal DefaultTxFrequency { get; init; }

    /// <summary>Identité portée par le certificat X.509 du nœud (CERT_SUBJ_*).</summary>
    public CertificateSubject CertificateSubject { get; init; } = CertificateSubject.Empty;

    public GeneralConfigurationUpdated(
        bool startReflectorOnStartup,
        bool startDefaultSalonOnStartup,
        decimal defaultRxFrequency,
        decimal defaultTxFrequency,
        CertificateSubject certificateSubject)
    {
        StartReflectorOnStartup = startReflectorOnStartup;
        StartDefaultSalonOnStartup = startDefaultSalonOnStartup;
        DefaultRxFrequency = defaultRxFrequency;
        DefaultTxFrequency = defaultTxFrequency;
        CertificateSubject = certificateSubject;
    }
}
