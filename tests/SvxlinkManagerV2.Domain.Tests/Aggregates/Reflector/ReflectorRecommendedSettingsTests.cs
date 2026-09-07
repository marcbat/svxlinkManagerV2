using FluentAssertions;
using SvxlinkManagerV2.Domain.Aggregates.Reflector;

namespace SvxlinkManagerV2.Domain.Tests.Aggregates.Reflector;

/// <summary>
/// Tests du diagnostic et de la complétion d'une configuration de réflecteur existante.
/// </summary>
/// <remarks>
/// Le seeder ne crée le réflecteur que s'il n'en existe aucun : ces deux mécanismes sont la
/// seule voie par laquelle une installation déjà en service reçoit les clés ajoutées au
/// modèle après coup.
/// </remarks>
public class ReflectorRecommendedSettingsTests
{
    /// <summary>Configuration telle que la livraient les versions antérieures au lot 3.</summary>
    private const string LegacyConfig = """
        [GLOBAL]
        TIMESTAMP_FORMAT="%c"
        LISTEN_PORT=5300
        ACCEPT_CALLSIGN=.*
        CODECS=OPUS
        CERT_PKI_DIR=/var/lib/svxlink/pki

        [TG#0]
        AUTO_QSY_AFTER=0
        ALLOW=.*
        """;

    #region Diagnostic

    [Fact]
    public void MissingFrom_ALegacyConfiguration_ShouldListEveryRecommendedSetting()
    {
        var missing = ReflectorRecommendedSettings.MissingFrom(LegacyConfig);

        missing.Should().BeEquivalentTo(ReflectorRecommendedSettings.All);
    }

    [Fact]
    public void MissingFrom_AConfigurationThatDeclaresThemAll_ShouldListNothing()
    {
        var complete = ReflectorConfigurationMerger.Add(
            LegacyConfig, ReflectorRecommendedSettings.All);

        ReflectorRecommendedSettings.MissingFrom(complete).Should().BeEmpty();
    }

    [Fact]
    public void MissingFrom_ShouldNotCountACommentedKeyAsDeclared()
    {
        // Un opérateur qui a délibérément mis un dièse devant n'a pas déclaré la clé, et le
        // démon ne la lira pas davantage.
        const string config = """
            [GLOBAL]
            CODECS=OPUS
            #TG_FOR_V1_CLIENTS=240
            """;

        ReflectorRecommendedSettings.MissingFrom(config)
            .Should().Contain(setting => setting.Key == "TG_FOR_V1_CLIENTS");
    }

    [Fact]
    public void MissingFrom_ShouldOnlyLookInsideTheRightSection()
    {
        // La même clé dans une autre section ne compte pas : le démon ne la lirait pas.
        const string config = """
            [GLOBAL]
            CODECS=OPUS

            [TG#0]
            TG_FOR_V1_CLIENTS=240
            """;

        ReflectorRecommendedSettings.MissingFrom(config)
            .Should().Contain(setting => setting.Key == "TG_FOR_V1_CLIENTS");
    }

    [Fact]
    public void MissingFrom_AnEmptyConfiguration_ShouldListEverything()
    {
        ReflectorRecommendedSettings.MissingFrom("")
            .Should().BeEquivalentTo(ReflectorRecommendedSettings.All);
    }

    [Fact]
    public void All_ShouldExplainWhyEachSettingMatters()
    {
        // La justification est affichée telle quelle à l'opérateur avant qu'il n'accepte.
        ReflectorRecommendedSettings.All.Should().OnlyContain(
            setting => !string.IsNullOrWhiteSpace(setting.Rationale));
    }

    #endregion

    #region Complétion

    [Fact]
    public void Add_ShouldDeclareTheMissingSettings()
    {
        var merged = ReflectorConfigurationMerger.Add(LegacyConfig, ReflectorRecommendedSettings.All);

        merged.Should().Contain("TG_FOR_V1_CLIENTS=240");
        merged.Should().Contain("RANDOM_QSY_RANGE=");
        merged.Should().Contain("COMMAND_PTY=");
    }

    /// <summary>
    /// C'est l'objet même de la fusion textuelle : passer par IniFile réécrirait le fichier
    /// et effacerait toutes les notes de l'opérateur.
    /// </summary>
    [Fact]
    public void Add_ShouldPreserveExistingCommentsAndValues()
    {
        const string config = """
            [GLOBAL]
            # Port choisi pour éviter le pare-feu du club
            LISTEN_PORT=5301
            CODECS=OPUS

            [TG#0]
            # Ne pas toucher : réglé avec F5XYZ
            ALLOW=^HB9.*$
            """;

        var merged = ReflectorConfigurationMerger.Add(config, ReflectorRecommendedSettings.All);

        merged.Should().Contain("# Port choisi pour éviter le pare-feu du club");
        merged.Should().Contain("LISTEN_PORT=5301");
        merged.Should().Contain("# Ne pas toucher : réglé avec F5XYZ");
        merged.Should().Contain("ALLOW=^HB9.*$");
    }

    [Fact]
    public void Add_ShouldNeverOverwriteAValueAlreadyChosen()
    {
        const string config = """
            [GLOBAL]
            CODECS=OPUS
            TG_FOR_V1_CLIENTS=91
            """;

        var merged = ReflectorConfigurationMerger.Add(config, ReflectorRecommendedSettings.All);

        merged.Should().Contain("TG_FOR_V1_CLIENTS=91");
        merged.Should().NotContain("TG_FOR_V1_CLIENTS=240");
    }

    [Fact]
    public void Add_ShouldPlaceTheSettingsInTheirOwnSection()
    {
        var merged = ReflectorConfigurationMerger.Add(LegacyConfig, ReflectorRecommendedSettings.All);

        var globalStart = merged.IndexOf("[GLOBAL]", StringComparison.Ordinal);
        var nextSection = merged.IndexOf("[TG#0]", StringComparison.Ordinal);
        var inserted = merged.IndexOf("TG_FOR_V1_CLIENTS=240", StringComparison.Ordinal);

        inserted.Should().BeGreaterThan(globalStart);
        inserted.Should().BeLessThan(nextSection, "la clé appartient à [GLOBAL]");
    }

    [Fact]
    public void Add_ShouldCommentEachSettingWithItsRationale()
    {
        var merged = ReflectorConfigurationMerger.Add(LegacyConfig, ReflectorRecommendedSettings.All);

        merged.Should().Contain("# Sans elle, un nœud en protocole V2");
    }

    [Fact]
    public void Add_ShouldProduceAConfigurationThatStillValidates()
    {
        var merged = ReflectorConfigurationMerger.Add(LegacyConfig, ReflectorRecommendedSettings.All);

        ReflectorConfigurationValidator.Validate(merged).Should().BeEmpty();
    }

    [Fact]
    public void Add_WithNothingMissing_ShouldReturnTheConfigurationUnchanged()
    {
        var complete = ReflectorConfigurationMerger.Add(LegacyConfig, ReflectorRecommendedSettings.All);

        ReflectorConfigurationMerger.Add(complete, ReflectorRecommendedSettings.All)
            .Should().Be(complete);
    }

    [Fact]
    public void Add_WhenTheSectionIsAbsent_ShouldCreateIt()
    {
        var merged = ReflectorConfigurationMerger.Add(
            "[TG#0]\nALLOW=.*", ReflectorRecommendedSettings.All);

        merged.Should().Contain("[GLOBAL]");
        merged.Should().Contain("TG_FOR_V1_CLIENTS=240");
    }

    #endregion
}
