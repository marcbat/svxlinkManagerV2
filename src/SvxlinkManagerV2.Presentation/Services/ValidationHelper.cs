using LanguageExt;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Presentation.Services;

/// <summary>
/// Classe statique d'helpers pour extraire les messages d'erreur depuis les objets Validation de LanguageExt
/// </summary>
public static class ValidationHelper
{
    /// <summary>
    /// Extrait tous les messages d'erreur depuis un Validation
    /// </summary>
    /// <typeparam name="T">Type du résultat en cas de succès</typeparam>
    /// <param name="validation">Objet Validation à analyser</param>
    /// <returns>Séquence de messages d'erreur</returns>
    public static IEnumerable<string> GetErrorMessages<T>(Validation<Error, T> validation)
    {
        return validation.Match(
            Succ: _ => Enumerable.Empty<string>(),
            Fail: errors => errors.Map(e => e.Message)
        );
    }

    /// <summary>
    /// Extrait le premier message d'erreur depuis un Validation
    /// </summary>
    /// <typeparam name="T">Type du résultat en cas de succès</typeparam>
    /// <param name="validation">Objet Validation à analyser</param>
    /// <returns>Premier message d'erreur ou null si aucune erreur</returns>
    public static string? GetFirstErrorMessage<T>(Validation<Error, T> validation)
    {
        if (validation.IsSuccess)
            return null;
            
        return validation.Match(
            Succ: _ => null as string,
            Fail: errors => errors.HeadOrNone().Match(
                Some: e => e.Message,
                None: () => null as string
            )
        );
    }

    /// <summary>
    /// Vérifie si un Validation est valide (succès)
    /// </summary>
    /// <typeparam name="T">Type du résultat en cas de succès</typeparam>
    /// <param name="validation">Objet Validation à vérifier</param>
    /// <returns>True si le Validation est en succès, False sinon</returns>
    public static bool IsValid<T>(Validation<Error, T> validation)
    {
        return validation.Match(
            Succ: _ => true,
            Fail: _ => false
        );
    }

    /// <summary>
    /// Extrait tous les codes d'erreur depuis un Validation
    /// </summary>
    /// <typeparam name="T">Type du résultat en cas de succès</typeparam>
    /// <param name="validation">Objet Validation à analyser</param>
    /// <returns>Séquence de codes d'erreur</returns>
    public static IEnumerable<string> GetErrorCodes<T>(Validation<Error, T> validation)
    {
        return validation.Match(
            Succ: _ => Enumerable.Empty<string>(),
            Fail: errors => errors.Map(e => e.Code)
        );
    }

    /// <summary>
    /// Extrait toutes les erreurs complètes (Code + Message) depuis un Validation
    /// </summary>
    /// <typeparam name="T">Type du résultat en cas de succès</typeparam>
    /// <param name="validation">Objet Validation à analyser</param>
    /// <returns>Séquence d'objets Error</returns>
    public static IEnumerable<Error> GetErrors<T>(Validation<Error, T> validation)
    {
        return validation.Match(
            Succ: _ => Enumerable.Empty<Error>(),
            Fail: errors => errors.AsEnumerable()
        );
    }
}
