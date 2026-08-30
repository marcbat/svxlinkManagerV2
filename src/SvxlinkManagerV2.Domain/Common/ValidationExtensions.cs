using LanguageExt;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Domain.Common;

/// <summary>
/// Extensions pour faciliter l'utilisation de Validation avec le type Error du domaine
/// </summary>
public static class ValidationExtensions
{
    /// <summary>
    /// Crée un succès de validation
    /// </summary>
    /// <typeparam name="T">Type du résultat</typeparam>
    /// <param name="value">Valeur de succès</param>
    /// <returns>Validation en succès</returns>
    public static Validation<Error, T> ToSuccess<T>(this T value)
        => Success<Error, T>(value);

    /// <summary>
    /// Crée un échec de validation avec une seule erreur
    /// </summary>
    /// <typeparam name="T">Type du résultat attendu</typeparam>
    /// <param name="error">Erreur</param>
    /// <returns>Validation en échec</returns>
    public static Validation<Error, T> ToFailure<T>(this Error error)
        => Fail<Error, T>(error);

    /// <summary>
    /// Crée un échec de validation avec plusieurs erreurs
    /// </summary>
    /// <typeparam name="T">Type du résultat attendu</typeparam>
    /// <param name="errors">Liste d'erreurs</param>
    /// <returns>Validation en échec</returns>
    public static Validation<Error, T> ToFailure<T>(this IEnumerable<Error> errors)
        => Fail<Error, T>(Seq(errors));

    /// <summary>
    /// Retourne la valeur d'une validation en succès, ou null si elle est en échec.
    ///
    /// À préférer systématiquement à <c>Match(Succ: v =&gt; v, Fail: _ =&gt; null)</c> : LanguageExt
    /// passe le résultat de <c>Match</c> par <c>Check.NullReturn</c> et lève
    /// <c>ResultIsNullException</c> dès qu'une branche rend null.
    /// </summary>
    /// <typeparam name="T">Type du résultat</typeparam>
    /// <param name="validation">Validation à dénouer</param>
    /// <returns>La valeur en cas de succès, null en cas d'échec</returns>
    public static T? SuccessOrNull<T>(this Validation<Error, T> validation) where T : class
        => validation.IsSuccess
            ? validation.Match(
                Succ: value => value,
                Fail: _ => throw new InvalidOperationException("Succès déjà établi."))
            : null;

    /// <summary>
    /// Combine plusieurs validations en une seule
    /// </summary>
    /// <typeparam name="T">Type du résultat</typeparam>
    /// <param name="validations">Liste de validations à combiner</param>
    /// <returns>Validation combinée</returns>
    public static Validation<Error, Seq<T>> Sequence<T>(
        this IEnumerable<Validation<Error, T>> validations)
        => validations.Traverse(x => x).Map(Seq);

    /// <summary>
    /// Valide qu'une chaîne n'est pas vide ou null
    /// </summary>
    /// <param name="value">Valeur à valider</param>
    /// <param name="code">Code d'erreur</param>
    /// <param name="message">Message d'erreur</param>
    /// <returns>Validation avec la valeur si non vide, erreur sinon</returns>
    public static Validation<Error, string> ValidateNotEmpty(
        this string? value,
        string code,
        string message)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Error.Validation(code, message).ToFailure<string>()
            : value.ToSuccess();
    }

    /// <summary>
    /// Valide qu'une valeur satisfait un prédicat
    /// </summary>
    /// <typeparam name="T">Type de la valeur</typeparam>
    /// <param name="value">Valeur à valider</param>
    /// <param name="predicate">Prédicat à satisfaire</param>
    /// <param name="code">Code d'erreur si le prédicat échoue</param>
    /// <param name="message">Message d'erreur si le prédicat échoue</param>
    /// <returns>Validation avec la valeur si le prédicat est satisfait, erreur sinon</returns>
    public static Validation<Error, T> ValidateThat<T>(
        this T value,
        Func<T, bool> predicate,
        string code,
        string message)
    {
        return predicate(value)
            ? value.ToSuccess()
            : Error.Validation(code, message).ToFailure<T>();
    }

    /// <summary>
    /// Valide qu'un Guid n'est pas vide
    /// </summary>
    /// <param name="value">Guid à valider</param>
    /// <param name="paramName">Nom du paramètre (pour le message)</param>
    /// <returns>Validation avec le Guid si non vide, erreur sinon</returns>
    public static Validation<Error, Guid> ValidateNotEmpty(
        this Guid value,
        string paramName)
    {
        return value == Guid.Empty
            ? Error.Validation($"EMPTY_{paramName.ToUpper()}", $"{paramName} cannot be empty")
                .ToFailure<Guid>()
            : value.ToSuccess();
    }
}
