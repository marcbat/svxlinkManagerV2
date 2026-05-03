using LanguageExt;
using MediatR;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Features.Validation;

/// <summary>
/// Command pour valider une entrée utilisateur
/// </summary>
/// <param name="Input">Entrée à valider</param>
public record ValidateInputCommand(string Input) : IRequest<Validation<Error, string>>;

/// <summary>
/// Handler pour la validation d'entrée. Démontre l'utilisation de Validation avec LanguageExt.
/// </summary>
public class ValidateInputCommandHandler : IRequestHandler<ValidateInputCommand, Validation<Error, string>>
{
    public Task<Validation<Error, string>> Handle(ValidateInputCommand command, CancellationToken cancellationToken)
    {
        var notEmptyValidation = command.Input.ValidateNotEmpty(
            "EMPTY_INPUT",
            "Input cannot be empty");

        var minLengthValidation = command.Input.ValidateThat(
            input => input.Length >= 3,
            "INPUT_TOO_SHORT",
            "Input must be at least 3 characters");

        var maxLengthValidation = command.Input.ValidateThat(
            input => input.Length <= 50,
            "INPUT_TOO_LONG",
            "Input must not exceed 50 characters");

        var result = (notEmptyValidation, minLengthValidation, maxLengthValidation)
            .Apply((_, _, _) => command.Input.ToUpper());

        return Task.FromResult(result);
    }

    /// <summary>
    /// Exemple de validation avec composition fonctionnelle
    /// </summary>
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
