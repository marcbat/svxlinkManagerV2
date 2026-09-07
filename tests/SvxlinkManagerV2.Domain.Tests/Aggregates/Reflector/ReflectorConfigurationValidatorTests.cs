using FluentAssertions;
using SvxlinkManagerV2.Domain.Aggregates.Reflector;

namespace SvxlinkManagerV2.Domain.Tests.Aggregates.Reflector;

/// <summary>
/// Tests de la validation syntaxique de svxreflector.conf.
/// </summary>
/// <remarks>
/// La ligne fautive du premier cas est celle qui a réellement mis le réflecteur de la stack
/// hors service le 07/09/2026 : un commentaire coupé en deux par un saut de ligne parasite.
/// Le démon répondait « Illegal value syntax on line 39 » et redémarrait en boucle.
/// </remarks>
public class ReflectorConfigurationValidatorTests
{
    private const string ValidConfig = """
        [GLOBAL]
        LISTEN_PORT=5300
        CODECS=OPUS

        [TG#240]
        ALLOW=.*
        """;

    [Fact]
    public void Validate_WithAValidConfiguration_ShouldReportNothing()
    {
        ReflectorConfigurationValidator.Validate(ValidConfig).Should().BeEmpty();
        ReflectorConfigurationValidator.IsValid(ValidConfig).Should().BeTrue();
    }

    [Fact]
    public void Validate_WithALineThatIsNeitherCommentSectionNorAssignment_ShouldReportAnError()
    {
        const string config = """
            [GLOBAL]
            LISTEN_PORT=5300
            " > /tmp/reflector_ctrl'
            """;

        var issues = ReflectorConfigurationValidator.Validate(config);

        issues.Should().ContainSingle()
            .Which.Should().Match<ReflectorConfigurationIssue>(issue =>
                issue.Line == 3 && issue.Severity == ReflectorConfigurationSeverity.Error);
        ReflectorConfigurationValidator.IsValid(config).Should().BeFalse();
    }

    [Fact]
    public void Validate_WithAnAssignmentBeforeAnySection_ShouldReportAnError()
    {
        // svxreflector ignorerait la ligne en silence : l'opérateur croirait l'avoir réglée.
        const string config = """
            LISTEN_PORT=5300
            [GLOBAL]
            CODECS=OPUS
            """;

        var issues = ReflectorConfigurationValidator.Validate(config);

        issues.Should().ContainSingle().Which.Line.Should().Be(1);
    }

    [Fact]
    public void Validate_WithAnUnclosedSection_ShouldReportAnError()
    {
        const string config = """
            [GLOBAL
            LISTEN_PORT=5300
            """;

        var issues = ReflectorConfigurationValidator.Validate(config);

        issues.Should().Contain(issue =>
            issue.Line == 1 && issue.Severity == ReflectorConfigurationSeverity.Error);
    }

    [Fact]
    public void Validate_WithoutAGlobalSection_ShouldReportAnError()
    {
        var issues = ReflectorConfigurationValidator.Validate("[TG#0]\nALLOW=.*");

        issues.Should().Contain(issue => issue.Message.Contains("[GLOBAL]"));
    }

    [Fact]
    public void Validate_WithAnEmptyVariableName_ShouldReportAnError()
    {
        var issues = ReflectorConfigurationValidator.Validate("[GLOBAL]\n=5300");

        issues.Should().ContainSingle().Which.Line.Should().Be(2);
    }

    /// <summary>
    /// SVXLink ajoute des sections d'une version à l'autre : refuser ce qu'on ne connaît pas
    /// empêcherait d'utiliser une nouveauté de l'amont.
    /// </summary>
    [Fact]
    public void Validate_WithAnUnknownSection_ShouldOnlyWarn()
    {
        var issues = ReflectorConfigurationValidator.Validate("[GLOBAL]\nCODECS=OPUS\n[NOUVEAUTE]\nX=1");

        issues.Should().ContainSingle()
            .Which.Severity.Should().Be(ReflectorConfigurationSeverity.Warning);
        ReflectorConfigurationValidator.IsValid("[GLOBAL]\nCODECS=OPUS\n[NOUVEAUTE]\nX=1")
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("TG#0")]
    [InlineData("TG#240")]
    [InlineData("TG#2409900")]
    [InlineData("USERS")]
    [InlineData("PASSWORDS")]
    [InlineData("ROOT_CA")]
    [InlineData("ISSUING_CA")]
    [InlineData("SERVER_CERT")]
    public void Validate_WithAKnownSection_ShouldNotWarn(string section)
    {
        var issues = ReflectorConfigurationValidator.Validate($"[GLOBAL]\nCODECS=OPUS\n[{section}]\nX=1");

        issues.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ShouldIgnoreCommentsAndBlankLines()
    {
        const string config = """
            # un commentaire
            ; un autre

            [GLOBAL]
            # ceci n'est pas une affectation
            CODECS=OPUS
            """;

        ReflectorConfigurationValidator.Validate(config).Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_WithAnEmptyConfiguration_ShouldReportAnError(string? config)
    {
        ReflectorConfigurationValidator.Validate(config).Should().ContainSingle();
        ReflectorConfigurationValidator.IsValid(config).Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldReportEveryFaultyLine()
    {
        const string config = """
            [GLOBAL]
            ligne cassée
            CODECS=OPUS
            autre ligne cassée
            """;

        ReflectorConfigurationValidator.Validate(config).Should().HaveCount(2);
    }
}
