using System.Globalization;
using SvxlinkManagerV2.Infrastructure.Common;

namespace SvxlinkManagerV2.Infrastructure.Reflector;

/// <summary>
/// Lecture du fichier de configuration réellement chargé par le démon <c>svxreflector</c>.
/// </summary>
/// <remarks>
/// C'est ce fichier qui fait autorité, et non l'agrégat en base : il est écrit à l'activation
/// du réflecteur, et un opérateur peut l'avoir modifié depuis. Il est relu à chaque
/// interrogation pour qu'un changement prenne effet sans redémarrer l'application.
/// </remarks>
public class ReflectorConfigurationFile
{
    /// <summary>Chemin écrit par <c>ActivateReflectorCommand</c>.</summary>
    public const string DefaultPath = "/etc/svxlink/svxreflector.conf";

    /// <summary>Port du serveur HTTP de statut.</summary>
    public const string HttpPortKey = "HTTP_SRV_PORT";

    /// <summary>Pseudo-terminal de commandes runtime.</summary>
    public const string CommandPtyKey = "COMMAND_PTY";

    /// <summary>Répertoire PKI du réflecteur, où vivent les demandes en attente.</summary>
    public const string PkiDirectoryKey = "CERT_PKI_DIR";

    /// <summary>Sous-répertoire des demandes de signature en attente, relatif au répertoire PKI.</summary>
    /// <remarks>
    /// Valeur par défaut de <c>CERT_CA_PENDING_CSRS_DIR</c>. Elle n'est pas configurable
    /// depuis l'application : la surcharger dans svxreflector.conf sortirait les demandes du
    /// champ de vision de cette page, ce que la page dit alors ne pas savoir faire.
    /// </remarks>
    public const string PendingCsrsDirectoryName = "pending_csrs";

    private readonly string _path;

    public ReflectorConfigurationFile(string? path = null) => _path = path ?? DefaultPath;

    /// <summary>Valeur brute d'une clé de la section <c>[GLOBAL]</c>, ou <c>null</c> si absente.</summary>
    public string? Read(string key)
    {
        if (!File.Exists(_path))
            return null;

        var value = IniFile.Parse(_path)["GLOBAL"][key];
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// Port TCP déclaré par une clé, ou <c>null</c> si la clé est absente, vide ou hors plage —
    /// trois façons de dire que le service correspondant n'est pas activé.
    /// </summary>
    public int? ReadPort(string key)
    {
        var value = Read(key);

        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port)
               && port is > 0 and <= 65535
            ? port
            : null;
    }

    /// <summary>
    /// Répertoire des demandes de signature en attente, ou <c>null</c> si le répertoire PKI
    /// n'est pas déclaré.
    /// </summary>
    public string? ReadPendingCsrsDirectory()
    {
        var pkiDirectory = Read(PkiDirectoryKey);

        return pkiDirectory is null
            ? null
            : $"{pkiDirectory.TrimEnd('/')}/{PendingCsrsDirectoryName}";
    }
}
