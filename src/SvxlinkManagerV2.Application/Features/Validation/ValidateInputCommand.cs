using LanguageExt;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Features.Validation;

/// <summary>
/// Command pour valider une entrée utilisateur
/// Exemple d'utilisation du Result Pattern avec Validation
/// </summary>
/// <param name="Input">Entrée à valider</param>
public record ValidateInputCommand(string Input);

/// <summary>
/// Handler pour la validation d'entrée
/// Démontre l'utilisation de Validation avec LanguageExt
/// </summary>
public static class ValidateInputCommandHandler
{
    /// <summary>
    /// Valide l'entrée selon les règles métier
    /// </summary>
    /// <param name="command">Command contenant l'entrée à valider</param>
    /// <returns>Validation contenant l'entrée en majuscules si valide, ou des erreurs si invalide</returns>
    /// <remarks>
    /// Règles de validation :
    /// - L'entrée ne doit pas être vide
    /// - L'entrée doit contenir au moins 3 caractères
    /// - L'entrée ne doit pas dépasser 50 caractères
    /// </remarks>
    public static Validation<Error, string> Handle(ValidateInputCommand command)
    {
        // Validation 1 : Non vide
        var notEmptyValidation = command.Input.ValidateNotEmpty(
            "EMPTY_INPUT",
            "Input cannot be empty");

        // Validation 2 : Longueur minimale
        var minLengthValidation = command.Input.ValidateThat(
            input => input.Length >= 3,
            "INPUT_TOO_SHORT",
            "Input must be at least 3 characters");

        // Validation 3 : Longueur maximale
        var maxLengthValidation = command.Input.ValidateThat(
            input => input.Length <= 50,
            "INPUT_TOO_LONG",
            "Input must not exceed 50 characters");

        // Combinaison des validations
        // Si toutes réussissent, retourne l'entrée en majuscules
        // Si au moins une échoue, retourne toutes les erreurs accumulées
        return (notEmptyValidation, minLengthValidation, maxLengthValidation)
            .Apply((_, _, _) => command.Input.ToUpper());
    }

    /// <summary>
    /// Exemple de validation avec composition fonctionnelle
    /// </summary>
    /// <param name="input">Entrée à valider</param>
    /// <returns>Validation du résultat transformé</returns>
    public static Validation<Error, int> ValidateAndGetLength(string input)
    {
        return input
            .ValidateNotEmpty("EMPTY_INPUT", "Input cannot be empty")
            .Bind(value => value.ValidateThat(
                v => v.Length >= 3 && v.Length <= 50,
                "INVALID_LENGTH",
                "Input must be between 3 and 50 characters"))
            .Map(value => value.Length);
    }
}
