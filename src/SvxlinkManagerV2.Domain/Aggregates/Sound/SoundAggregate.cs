using LanguageExt;
using SvxlinkManagerV2.Domain.Aggregates.Sound.Events;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Domain.Aggregates.Sound;

/// <summary>
/// Aggregate représentant un fichier audio WAV pour annonces vocales.
/// Utilisé par les Salons pour les annonces lors de connexion au Reflector.
/// Stream Marten : sound-{guid}
/// </summary>
public class SoundAggregate : AggregateRoot
{
    /// <summary>
    /// Nom du fichier audio (sans extension)
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Contenu du fichier audio (.wav)
    /// </summary>
    public byte[] FileContent { get; private set; } = System.Array.Empty<byte>();

    /// <summary>
    /// Durée du fichier audio
    /// </summary>
    public TimeSpan Duration { get; private set; }

    /// <summary>
    /// Sample rate en Hz (recommandé : 16000)
    /// </summary>
    public int SampleRate { get; private set; }

    /// <summary>
    /// Nombre de canaux (recommandé : 1 = mono)
    /// </summary>
    public int Channels { get; private set; }

    /// <summary>
    /// Date de création
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Date de dernière mise à jour
    /// </summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// Indique si le sound est supprimé (soft delete)
    /// </summary>
    public bool IsDeleted { get; private set; }

    /// <summary>
    /// Constructeur par défaut requis pour Marten (rehydratation)
    /// </summary>
    public SoundAggregate()
    {
    }

    /// <summary>
    /// Factory method pour créer un nouveau Sound avec validations métier.
    /// Retourne un Validation&lt;Error, SoundAggregate&gt; selon le Result Pattern.
    /// </summary>
    /// <param name="id">Identifiant unique du sound</param>
    /// <param name="name">Nom du fichier</param>
    /// <param name="fileContent">Contenu du fichier WAV</param>
    /// <returns>Validation contenant l'aggregate ou les erreurs de validation</returns>
    public static Validation<Error, SoundAggregate> Create(
        Guid id,
        string name,
        byte[] fileContent)
    {
        // Validation de l'identifiant
        var idValidation = id.ValidateNotEmpty("Id");

        // Validation du nom
        var nameValidation = ValidateName(name);

        // Validation du contenu fichier
        var fileValidation = ValidateFileContent(fileContent);

        // Validation du format WAV et extraction métadonnées
        var wavMetadataValidation = fileValidation.Bind(content => ExtractWavMetadata(content));

        // Combinaison de toutes les validations
        return (idValidation, nameValidation, wavMetadataValidation)
            .Apply((validId, validName, metadata) =>
            {
                var aggregate = new SoundAggregate();
                var @event = new SoundCreatedEvent(
                    validId,
                    validName,
                    fileContent,
                    metadata.Duration,
                    metadata.SampleRate,
                    metadata.Channels);

                aggregate.Apply(@event);
                aggregate.AddDomainEvent(@event);

                return aggregate;
            });
    }

    /// <summary>
    /// Mise à jour du sound
    /// </summary>
    /// <param name="name">Nouveau nom (optionnel)</param>
    /// <param name="fileContent">Nouveau contenu (optionnel)</param>
    /// <returns>Validation du résultat</returns>
    public Validation<Error, Unit> Update(
        string? name = null,
        byte[]? fileContent = null)
    {
        if (IsDeleted)
            return Error.Validation("SOUND_DELETED", "Le sound est supprimé")
                .ToFailure<Unit>();

        // Validation du nom si fourni
        var nameValidation = name != null
            ? ValidateName(name)
            : Success<Error, string>(Name);

        // Validation du contenu et métadonnées si fourni
        // Si fileContent n'est pas fourni, on retourne les valeurs actuelles
        Validation<Error, (byte[] Content, WavMetadata Metadata)> fileValidation =
            fileContent != null
                ? ValidateFileContent(fileContent)
                    .Bind(content => ExtractWavMetadata(content)
                        .Map(metadata => (content, metadata)))
                : Success<Error, (byte[] Content, WavMetadata Metadata)>(
                    (FileContent, new WavMetadata(Duration, SampleRate, Channels, 16)));

        return (nameValidation, fileValidation)
            .Apply((validName, validFile) =>
            {
                var @event = new SoundUpdatedEvent(
                    Id,
                    name,
                    fileContent,
                    fileContent != null ? validFile.Metadata.Duration : null,
                    fileContent != null ? validFile.Metadata.SampleRate : null,
                    fileContent != null ? validFile.Metadata.Channels : null);

                Apply(@event);
                AddDomainEvent(@event);

                return unit;
            });
    }

    /// <summary>
    /// Suppression logique du sound
    /// </summary>
    /// <returns>Validation du résultat</returns>
    public Validation<Error, Unit> Delete()
    {
        if (IsDeleted)
            return Error.Validation("SOUND_ALREADY_DELETED", "Le sound est déjà supprimé")
                .ToFailure<Unit>();

        var @event = new SoundDeletedEvent(Id);
        Apply(@event);
        AddDomainEvent(@event);

        return unit.ToSuccess();
    }

    /// <summary>
    /// Applique l'événement SoundCreatedEvent (Event Sourcing)
    /// </summary>
    public void Apply(SoundCreatedEvent @event)
    {
        Id = @event.Id;
        Name = @event.Name;
        FileContent = @event.FileContent;
        Duration = @event.Duration;
        SampleRate = @event.SampleRate;
        Channels = @event.Channels;
        CreatedAt = @event.OccurredOn;
        UpdatedAt = @event.OccurredOn;
        IsDeleted = false;
    }

    /// <summary>
    /// Applique l'événement SoundUpdatedEvent (Event Sourcing)
    /// </summary>
    public void Apply(SoundUpdatedEvent @event)
    {
        if (@event.Name != null)
            Name = @event.Name;

        if (@event.FileContent != null)
        {
            FileContent = @event.FileContent;
            Duration = @event.Duration!.Value;
            SampleRate = @event.SampleRate!.Value;
            Channels = @event.Channels!.Value;
        }

        UpdatedAt = @event.OccurredOn;
    }

    /// <summary>
    /// Applique l'événement SoundDeletedEvent (Event Sourcing)
    /// </summary>
    public void Apply(SoundDeletedEvent @event)
    {
        IsDeleted = true;
    }

    /// <summary>
    /// Valide le nom du fichier
    /// </summary>
    private static Validation<Error, string> ValidateName(string name)
    {
        // Nom obligatoire
        if (string.IsNullOrWhiteSpace(name))
            return Error.Validation("SOUND_NAME_REQUIRED", "Le nom du fichier est obligatoire")
                .ToFailure<string>();

        // Caractères invalides pour un nom de fichier
        var invalidChars = System.IO.Path.GetInvalidFileNameChars();
        if (name.Any(c => invalidChars.Contains(c)))
            return Error.Validation(
                "SOUND_NAME_INVALID_CHARS",
                $"Le nom contient des caractères invalides : {string.Join(", ", invalidChars)}")
                .ToFailure<string>();

        // Longueur raisonnable
        if (name.Length > 100)
            return Error.Validation(
                "SOUND_NAME_TOO_LONG",
                "Le nom du fichier ne doit pas dépasser 100 caractères")
                .ToFailure<string>();

        return Success<Error, string>(name);
    }

    /// <summary>
    /// Valide le contenu du fichier
    /// </summary>
    private static Validation<Error, byte[]> ValidateFileContent(byte[] content)
    {
        if (content == null || content.Length == 0)
            return Error.Validation("SOUND_CONTENT_EMPTY", "Le contenu du fichier est vide")
                .ToFailure<byte[]>();

        // Taille maximale raisonnable (10 MB)
        if (content.Length > 10 * 1024 * 1024)
            return Error.Validation(
                "SOUND_CONTENT_TOO_LARGE",
                "Le fichier ne doit pas dépasser 10 MB")
                .ToFailure<byte[]>();

        return Success<Error, byte[]>(content);
    }

    /// <summary>
    /// Extrait les métadonnées d'un fichier WAV
    /// Format WAV : RIFF header + fmt chunk + data chunk
    /// </summary>
    private static Validation<Error, WavMetadata> ExtractWavMetadata(byte[] content)
    {
        try
        {
            // Vérifier la taille minimale d'un header WAV (44 bytes minimum)
            if (content.Length < 44)
                return Error.Validation(
                    "SOUND_INVALID_WAV",
                    "Le fichier est trop petit pour être un WAV valide")
                    .ToFailure<WavMetadata>();

            // Vérifier le magic "RIFF" (bytes 0-3)
            var riff = System.Text.Encoding.ASCII.GetString(content, 0, 4);
            if (riff != "RIFF")
                return Error.Validation(
                    "SOUND_INVALID_WAV_RIFF",
                    "Le fichier n'est pas un WAV valide (header RIFF manquant)")
                    .ToFailure<WavMetadata>();

            // Vérifier le format "WAVE" (bytes 8-11)
            var wave = System.Text.Encoding.ASCII.GetString(content, 8, 4);
            if (wave != "WAVE")
                return Error.Validation(
                    "SOUND_INVALID_WAV_WAVE",
                    "Le fichier n'est pas un WAV valide (format WAVE manquant)")
                    .ToFailure<WavMetadata>();

            // Vérifier le chunk "fmt " (bytes 12-15)
            var fmt = System.Text.Encoding.ASCII.GetString(content, 12, 4);
            if (fmt != "fmt ")
                return Error.Validation(
                    "SOUND_INVALID_WAV_FMT",
                    "Le fichier n'est pas un WAV valide (chunk fmt manquant)")
                    .ToFailure<WavMetadata>();

            // Extraire les métadonnées du chunk fmt
            // Sample rate (bytes 24-27, little-endian)
            int sampleRate = BitConverter.ToInt32(content, 24);

            // Channels (bytes 22-23, little-endian)
            short channels = BitConverter.ToInt16(content, 22);

            // Byte rate (bytes 28-31, little-endian)
            int byteRate = BitConverter.ToInt32(content, 28);

            // Bits per sample (bytes 34-35, little-endian)
            short bitsPerSample = BitConverter.ToInt16(content, 34);

            // Trouver le chunk "data" pour calculer la durée
            int dataChunkPos = 36;
            while (dataChunkPos < content.Length - 8)
            {
                var chunkId = System.Text.Encoding.ASCII.GetString(content, dataChunkPos, 4);
                var chunkSize = BitConverter.ToInt32(content, dataChunkPos + 4);

                if (chunkId == "data")
                {
                    // Calculer la durée : data size / byte rate
                    var durationSeconds = (double)chunkSize / byteRate;
                    var duration = TimeSpan.FromSeconds(durationSeconds);

                    var metadata = new WavMetadata(
                        duration,
                        sampleRate,
                        channels,
                        bitsPerSample);

                    // Validation recommandée : 16kHz mono
                    var warnings = new List<string>();
                    if (sampleRate != 16000)
                        warnings.Add($"Sample rate recommandé : 16000 Hz (actuel : {sampleRate} Hz)");
                    if (channels != 1)
                        warnings.Add($"Mono recommandé (actuel : {channels} canaux)");

                    // Pour l'instant, on accepte tous les formats WAV valides
                    // Les warnings peuvent être loggés ou renvoyés dans un contexte différent

                    return Success<Error, WavMetadata>(metadata);
                }

                dataChunkPos += 8 + chunkSize;
            }

            return Error.Validation(
                "SOUND_INVALID_WAV_DATA",
                "Le fichier WAV ne contient pas de chunk data")
                .ToFailure<WavMetadata>();
        }
        catch (Exception ex)
        {
            return Error.Validation(
                "SOUND_WAV_PARSE_ERROR",
                $"Erreur lors de l'analyse du fichier WAV : {ex.Message}")
                .ToFailure<WavMetadata>();
        }
    }

    /// <summary>
    /// Record contenant les métadonnées extraites d'un fichier WAV
    /// </summary>
    private record WavMetadata(
        TimeSpan Duration,
        int SampleRate,
        int Channels,
        int BitsPerSample);
}
