namespace SvxlinkManagerV2.Integration.Tests;

/// <summary>
/// Marque un test qui a besoin de la stack Docker.
/// </summary>
/// <remarks>
/// Le test est ignoré tant que la variable d'environnement
/// <c>SVXLINK_INTEGRATION_TESTS</c> ne vaut pas <c>1</c>. C'est ce qui permet à
/// <c>dotnet test SvxlinkManagerV2.sln</c> de rester exécutable sans Docker et sans
/// filtre : un contributeur qui lance la suite complète n'attend pas la construction
/// de SVXLink depuis les sources.
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class DockerComposeFactAttribute : FactAttribute
{
    /// <summary>Variable d'environnement qui autorise l'exécution.</summary>
    public const string EnvironmentVariableName = "SVXLINK_INTEGRATION_TESTS";

    public DockerComposeFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(EnvironmentVariableName) != "1")
        {
            Skip = $"Test d'intégration Docker ignoré : positionner {EnvironmentVariableName}=1 pour l'exécuter.";
        }
    }
}
