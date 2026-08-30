using FluentAssertions;
using SvxlinkManagerV2.Application.Features.Diagnostics;

namespace SvxlinkManagerV2.Application.Tests.Features.Diagnostics;

/// <summary>
/// Tests unitaires de l'expurgation des secrets, garde-fou de l'archive de diagnostic :
/// aucune clé d'authentification réflecteur ni mot de passe ne doit survivre à l'export.
/// </summary>
public class DiagnosticSecretRedactorTests
{
    [Fact]
    public void Redact_ShouldRemoveAuthKeyValue_ButKeepTheKeyName()
    {
        const string configuration = """
            [ReflectorLogic]
            HOST=reflector.example.org
            CALLSIGN=F4ABC
            AUTH_KEY=Magnifique123456789!
            """;

        var result = DiagnosticSecretRedactor.Redact(configuration);

        result.Should().NotContain("Magnifique123456789!");
        result.Should().Contain($"AUTH_KEY={DiagnosticSecretRedactor.RedactedValue}");
    }

    [Theory]
    [InlineData("AUTH_KEY=secret-value")]
    [InlineData("PASSWORD=secret-value")]
    [InlineData("REFLECTOR_PASSWD=secret-value")]
    [InlineData("CLIENT_SECRET=secret-value")]
    [InlineData("GitHubToken=secret-value")]
    [InlineData("WIFI_PSK=secret-value")]
    [InlineData("PRIVATE_KEY=secret-value")]
    public void Redact_ShouldRemoveEveryRecognizedSecretAssignment(string line)
    {
        var result = DiagnosticSecretRedactor.Redact(line);

        result.Should().NotContain("secret-value");
        result.Should().EndWith(DiagnosticSecretRedactor.RedactedValue);
    }

    [Fact]
    public void Redact_ShouldRemoveEveryValueOfThePasswordsSection()
    {
        const string configuration = """
            [ReflectorLogic]
            CALLSIGN=F4ABC

            [PASSWORDS]
            F4ABC=motdepasse
            F4XYZ=autremotdepasse

            [GLOBAL]
            LOGICS=SimplexLogic
            """;

        var result = DiagnosticSecretRedactor.Redact(configuration);

        result.Should().NotContain("motdepasse");
        result.Should().NotContain("autremotdepasse");
        result.Should().Contain($"F4ABC={DiagnosticSecretRedactor.RedactedValue}");
        result.Should().Contain($"F4XYZ={DiagnosticSecretRedactor.RedactedValue}");

        // La sortie de la section rétablit le comportement nominal.
        result.Should().Contain("LOGICS=SimplexLogic");
    }

    [Fact]
    public void Redact_ShouldPreserveNonSecretAssignments()
    {
        const string configuration = """
            [ReflectorLogic]
            HOST=reflector.example.org
            PORT=5300
            CALLSIGN=F4ABC
            EVENT_HANDLER=/opt/svxlink-modern/share/svxlink/events.d/local/Logic.tcl
            """;

        var result = DiagnosticSecretRedactor.Redact(configuration);

        result.Should().Be(configuration);
    }

    [Fact]
    public void Redact_ShouldAlsoCoverSecretsAppearingInLogLines()
    {
        const string logLine = "2026-08-30 14:32:11.120 [INFO  ] ReflectorLogic: AUTH_KEY=Magnifique123456789!";

        var result = DiagnosticSecretRedactor.Redact(logLine);

        result.Should().NotContain("Magnifique123456789!");
        result.Should().StartWith("2026-08-30 14:32:11.120 [INFO  ] ReflectorLogic: AUTH_KEY=");
    }

    [Fact]
    public void Redact_ShouldPreserveLineEndings()
    {
        const string configuration = "HOST=reflector\r\nAUTH_KEY=secret\r\nPORT=5300\r\n";

        var result = DiagnosticSecretRedactor.Redact(configuration);

        result.Should().Be($"HOST=reflector\r\nAUTH_KEY={DiagnosticSecretRedactor.RedactedValue}\r\nPORT=5300\r\n");
    }

    [Fact]
    public void Redact_ShouldReturnEmptyString_WhenContentIsNull()
        => DiagnosticSecretRedactor.Redact(null).Should().BeEmpty();
}
