namespace SvxlinkManagerV2.Domain.Aggregates.GeneralConfiguration.Entities;

/// <summary>
/// Identité portée par le certificat X.509 du nœud (variables <c>CERT_SUBJ_*</c>).
/// </summary>
/// <param name="GivenName">Prénom du titulaire (<c>CERT_SUBJ_GN</c>).</param>
/// <param name="Surname">Nom de famille (<c>CERT_SUBJ_SN</c>).</param>
/// <param name="OrganizationalUnit">Unité organisationnelle (<c>CERT_SUBJ_OU</c>).</param>
/// <param name="Organization">Organisation, radio-club par exemple (<c>CERT_SUBJ_O</c>).</param>
/// <param name="Locality">Ville où se trouve le nœud (<c>CERT_SUBJ_L</c>).</param>
/// <param name="StateOrProvince">Canton, région ou département (<c>CERT_SUBJ_ST</c>).</param>
/// <param name="Country">Code pays ISO 3166 à deux lettres (<c>CERT_SUBJ_C</c>).</param>
/// <remarks>
/// <para>
/// Ces informations relèvent de l'identité du <b>nœud</b>, pas du salon : elles sont donc
/// portées par la configuration générale et reprises par tous les salons V3. Certains sysops
/// de réflecteur exigent de les voir pour accepter de signer une demande.
/// </para>
/// <para>
/// Les modifier change le sujet de la demande de signature : le certificat existant devient
/// caduc et doit être signé à nouveau. L'interface le dit avant d'enregistrer.
/// </para>
/// <para>
/// Chaque champ est facultatif ; ceux qui sont vides ne sont pas écrits dans la
/// configuration générée, ce qui laisse SVXLink construire un sujet réduit au Common Name.
/// </para>
/// </remarks>
public record CertificateSubject(
    string? GivenName = null,
    string? Surname = null,
    string? OrganizationalUnit = null,
    string? Organization = null,
    string? Locality = null,
    string? StateOrProvince = null,
    string? Country = null)
{
    /// <summary>Aucune information d'identité déclarée.</summary>
    public static readonly CertificateSubject Empty = new();

    /// <summary>Au moins un champ est renseigné.</summary>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(GivenName)
        && string.IsNullOrWhiteSpace(Surname)
        && string.IsNullOrWhiteSpace(OrganizationalUnit)
        && string.IsNullOrWhiteSpace(Organization)
        && string.IsNullOrWhiteSpace(Locality)
        && string.IsNullOrWhiteSpace(StateOrProvince)
        && string.IsNullOrWhiteSpace(Country);

    /// <summary>
    /// Variables <c>CERT_SUBJ_*</c> à écrire, dans l'ordre du sujet X.509, sans les champs
    /// laissés vides.
    /// </summary>
    public IReadOnlyList<(string Key, string Value)> ToConfigurationEntries()
    {
        (string Key, string? Value)[] candidates =
        [
            ("CERT_SUBJ_GN", GivenName),
            ("CERT_SUBJ_SN", Surname),
            ("CERT_SUBJ_OU", OrganizationalUnit),
            ("CERT_SUBJ_O", Organization),
            ("CERT_SUBJ_L", Locality),
            ("CERT_SUBJ_ST", StateOrProvince),
            ("CERT_SUBJ_C", Country)
        ];

        return candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Value))
            .Select(candidate => (candidate.Key, candidate.Value!.Trim()))
            .ToList();
    }
}
