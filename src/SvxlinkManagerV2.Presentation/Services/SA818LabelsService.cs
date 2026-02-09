namespace SvxlinkManagerV2.Presentation.Services;

/// <summary>
/// Service helper fournissant les labels traduits pour les dropdowns du formulaire SA818
/// </summary>
public static class SA818LabelsService
{
    /// <summary>
    /// Retourne un dictionnaire Volume (1-8) → Label français
    /// </summary>
    public static Dictionary<int, string> GetVolumeLabels()
    {
        return new Dictionary<int, string>
        {
            { 1, "Très faible" },
            { 2, "Faible" },
            { 3, "Moyen-" },
            { 4, "Moyen" },
            { 5, "Moyen+" },
            { 6, "Fort" },
            { 7, "Très fort" },
            { 8, "Maximum" }
        };
    }

    /// <summary>
    /// Retourne un dictionnaire Squelch (0-8) → Label français
    /// </summary>
    public static Dictionary<int, string> GetSquelchLabels()
    {
        return new Dictionary<int, string>
        {
            { 0, "Désactivé" },
            { 1, "Très faible" },
            { 2, "Faible" },
            { 3, "Moyen-" },
            { 4, "Moyen" },
            { 5, "Moyen+" },
            { 6, "Fort" },
            { 7, "Très fort" },
            { 8, "Maximum" }
        };
    }
}
