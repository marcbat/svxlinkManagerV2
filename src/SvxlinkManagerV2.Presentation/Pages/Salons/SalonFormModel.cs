using System.ComponentModel.DataAnnotations;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;

namespace SvxlinkManagerV2.Presentation.Pages.Salons;

/// <summary>
/// Modèle de formulaire pour la création/édition d'un salon
/// </summary>
public class SalonFormModel
{
    // Pattern de validation pour les indicatifs radioamateurs
    private const string CallsignPattern = @"^[A-Z]{1,2}\d[A-Z0-9]{1,4}(-[A-Z0-9]{1,2})?$";
    private const string CallsignErrorMessage = "Le format de l'indicatif est invalide (format radioamateur attendu, ex: F5ABC, F5ABC-L)";

    // Identifiant (pour édition)
    public Guid? Id { get; set; }

    // Section Informations générales
    [Required(ErrorMessage = "Le nom du salon est requis")]
    [MaxLength(100, ErrorMessage = "Le nom ne peut dépasser 100 caractères")]
    public string Name { get; set; } = string.Empty;

    public bool IsDefault { get; set; }
    public bool IsTemporized { get; set; }

    // Section Configuration Reflector (ReflectorLogic)
    [Required(ErrorMessage = "L'adresse host est requise")]
    public string Host { get; set; } = "rrf.f5nlg.ovh";

    [Range(1, 65535, ErrorMessage = "Le port doit être entre 1 et 65535")]
    public int Port { get; set; } = 5300;

    [Required(ErrorMessage = "Le callsign est requis")]
    public string Callsign { get; set; } = string.Empty;

    [Required(ErrorMessage = "L'AuthKey est requis")]
    [MinLength(8, ErrorMessage = "L'AuthKey doit contenir au moins 8 caractères")]
    public string AuthKey { get; set; } = string.Empty;

    // Section Configuration SimplexLogic
    private string _simplexCallsign = string.Empty;
    
    [Required(ErrorMessage = "Le SimplexCallsign est requis")]
    [RegularExpression(CallsignPattern, ErrorMessage = CallsignErrorMessage)]
    public string SimplexCallsign 
    { 
        get => _simplexCallsign;
        set => _simplexCallsign = value?.ToUpperInvariant() ?? string.Empty;
    }

    [Range(5, 3600, ErrorMessage = "L'intervalle doit être entre 5 et 3600 secondes")]
    public int ShortIdentInterval { get; set; } = 300;

    [Range(5, 3600, ErrorMessage = "L'intervalle doit être entre 5 et 3600 secondes")]
    public int LongIdentInterval { get; set; } = 3600;

    [MaxLength(10)]
    public string? ReportCtcss { get; set; }

    // Section Configuration Radio
    [Required(ErrorMessage = "La fréquence RX est requise")]
    [Range(30, 3000, ErrorMessage = "La fréquence doit être entre 30 et 3000 MHz")]
    public decimal RxFrequency { get; set; } = 145.450M;

    [Required(ErrorMessage = "La fréquence TX est requise")]
    [Range(30, 3000, ErrorMessage = "La fréquence doit être entre 30 et 3000 MHz")]
    public decimal TxFrequency { get; set; } = 145.450M;

    public decimal? RxCtcss { get; set; }
    public decimal? TxCtcss { get; set; }

    // Section DTMF
    [Required(ErrorMessage = "Le code DTMF est requis")]
    [RegularExpression(@"^\d{1,4}$", ErrorMessage = "Le code DTMF doit contenir 1 à 4 chiffres")]
    public string DtmfCode { get; set; } = "100";

    // Section Sons
    public Guid? SoundId { get; set; }

    // Valeurs figées en backend (non exposées dans le formulaire)
    // Ces valeurs seront utilisées pour construire SvxLinkConfiguration
    private const string FixedLogics = "ReflectorLogic,SimplexLogic";
    private const string FixedCfgDir = "svxlink.d";
    private const int FixedCardSampleRate = 48000;
    private const int FixedCardChannels = 1;
    private const string FixedAudioCodec = "OPUS";
    private const int FixedJitterBufferDelay = 0;
    private const string FixedModules = "ModuleParrot";
    private const string FixedEventHandler = "/usr/share/svxlink/events.tcl";
    private const string FixedDefaultLang = "fr_FR";
    private const int FixedRgrSoundDelay = 0;

    /// <summary>
    /// Construit un objet SvxLinkConfiguration à partir des données du formulaire
    /// </summary>
    public SvxLinkConfiguration ToConfiguration()
    {
        return new SvxLinkConfiguration(
            Id: this.Id ?? Guid.NewGuid(),
            // Section GLOBAL
            Logics: FixedLogics,
            CfgDir: FixedCfgDir,
            CardSampleRate: FixedCardSampleRate,
            CardChannels: FixedCardChannels,
            // Section ReflectorLogic
            Host: this.Host,
            Port: this.Port,
            Callsign: this.Callsign,
            AuthKey: this.AuthKey,
            AudioCodec: FixedAudioCodec,
            JitterBufferDelay: FixedJitterBufferDelay,
            // Section SimplexLogic
            SimplexCallsign: this.SimplexCallsign,
            Modules: FixedModules,
            ShortIdentInterval: this.ShortIdentInterval,
            LongIdentInterval: this.LongIdentInterval,
            ReportCtcss: this.ReportCtcss,
            EventHandler: FixedEventHandler,
            DefaultLang: FixedDefaultLang,
            RgrSoundDelay: FixedRgrSoundDelay,
            // Références
            SoundId: this.SoundId,
            // Configuration Radio
            RxFrequency: this.RxFrequency,
            TxFrequency: this.TxFrequency,
            RxCtcss: this.RxCtcss,
            TxCtcss: this.TxCtcss
        );
    }

    /// <summary>
    /// Remplit le modèle à partir d'un SalonAggregate existant (pour édition)
    /// </summary>
    public static SalonFormModel FromAggregate(Domain.Aggregates.Salon.SalonAggregate salon)
    {
        var config = salon.Configuration;
        return new SalonFormModel
        {
            Id = salon.Id,
            Name = salon.Name,
            IsDefault = salon.IsDefault,
            IsTemporized = salon.IsTemporized,
            // Reflector
            Host = config.Host,
            Port = config.Port,
            Callsign = config.Callsign,
            AuthKey = config.AuthKey,
            // SimplexLogic
            SimplexCallsign = config.SimplexCallsign,
            ShortIdentInterval = config.ShortIdentInterval,
            LongIdentInterval = config.LongIdentInterval,
            ReportCtcss = config.ReportCtcss,
            // Radio
            RxFrequency = config.RxFrequency,
            TxFrequency = config.TxFrequency,
            RxCtcss = config.RxCtcss,
            TxCtcss = config.TxCtcss,
            // DTMF (à extraire ou fixer)
            DtmfCode = "100", // TODO: Extraire du SimplexLogic.DTMF_CTRL_PTY si disponible
            // Sons
            SoundId = config.SoundId
        };
    }
}
