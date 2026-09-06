using FluentAssertions;
using LanguageExt.UnitTesting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SvxlinkManagerV2.Application.Features.ApplicationUpdate;
using SvxlinkManagerV2.Infrastructure.Network.Apt;

namespace SvxlinkManagerV2.Infrastructure.Tests.Network.Apt;

public class AptSourceManagerTests : IDisposable
{
    private readonly string _directory;
    private readonly AptUpdateOptions _options;
    private readonly AptSourceManager _manager;

    public AptSourceManagerTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"apt-source-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);

        _options = new AptUpdateOptions
        {
            SourceListPath = Path.Combine(_directory, "svxlinkmanager.list"),
            KeyringPath = Path.Combine(_directory, "svxlinkmanager.gpg"),
            RepositoryUrl = "https://example.test/repo",
            Architecture = "armhf",
            Component = "main"
        };

        _manager = new AptSourceManager(NullLogger<AptSourceManager>.Instance, Options.Create(_options));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);

        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData(ApplicationUpdateChannel.Stable, "stable")]
    [InlineData(ApplicationUpdateChannel.Beta, "beta")]
    [InlineData(ApplicationUpdateChannel.Development, "development")]
    public void WriteChannel_ecrit_la_suite_correspondante(ApplicationUpdateChannel channel, string expectedSuite)
    {
        _manager.WriteChannel(channel).ShouldBeSuccess();

        var content = File.ReadAllText(_options.SourceListPath);
        content.Should().Contain($" {expectedSuite} main");
        content.Should().Contain("arch=armhf");
        content.Should().Contain($"signed-by={_options.KeyringPath}");
        content.Should().Contain("https://example.test/repo");
    }

    [Fact]
    public void ReadChannel_relit_ce_que_WriteChannel_a_ecrit()
    {
        _manager.WriteChannel(ApplicationUpdateChannel.Beta).ShouldBeSuccess();

        _manager.ReadChannel().Should().Be(ApplicationUpdateChannel.Beta);
    }

    [Fact]
    public void ReadChannel_retourne_null_quand_le_fichier_est_absent()
    {
        _manager.ReadChannel().Should().BeNull();
    }

    [Fact]
    public void WriteChannel_echoue_proprement_quand_le_chemin_est_inaccessible()
    {
        var options = new AptUpdateOptions
        {
            // Un fichier existant utilisé comme répertoire parent : la création échoue.
            SourceListPath = Path.Combine(_options.KeyringPath, "impossible", "source.list")
        };
        File.WriteAllText(_options.KeyringPath, "clé");

        var manager = new AptSourceManager(NullLogger<AptSourceManager>.Instance, Options.Create(options));

        manager.WriteChannel(ApplicationUpdateChannel.Stable).ShouldBeFail();
    }

    [Fact]
    public void IsConfigured_exige_la_source_et_le_trousseau()
    {
        _manager.IsConfigured().Should().BeFalse();

        _manager.WriteChannel(ApplicationUpdateChannel.Stable).ShouldBeSuccess();
        _manager.IsConfigured().Should().BeFalse("le trousseau manque encore");

        File.WriteAllText(_options.KeyringPath, "clé");
        _manager.IsConfigured().Should().BeTrue();
    }

    [Theory]
    [InlineData("deb [arch=armhf signed-by=/etc/apt/keyrings/k.gpg] https://example.test/repo beta main",
        ApplicationUpdateChannel.Beta)]
    [InlineData("deb https://example.test/repo development main", ApplicationUpdateChannel.Development)]
    [InlineData("deb [trusted=yes] https://example.test/repo stable main", ApplicationUpdateChannel.Stable)]
    public void ParseChannel_reconnait_la_suite_avec_ou_sans_options(string line, ApplicationUpdateChannel expected)
    {
        AptSourceManager.ParseChannel(line).Should().Be(expected);
    }

    [Theory]
    [InlineData("# deb https://example.test/repo stable main")]
    [InlineData("")]
    [InlineData("deb-src https://example.test/repo stable main")]
    [InlineData("deb https://example.test/repo bookworm main")]
    public void ParseChannel_ignore_les_lignes_hors_sujet(string line)
    {
        AptSourceManager.ParseChannel(line).Should().BeNull();
    }

    [Fact]
    public void ReadChannel_ignore_les_commentaires_avant_la_ligne_utile()
    {
        File.WriteAllLines(_options.SourceListPath,
        [
            "# Fichier géré par SvxlinkManagerV2",
            "# ne pas modifier",
            "deb [arch=armhf] https://example.test/repo development main"
        ]);

        _manager.ReadChannel().Should().Be(ApplicationUpdateChannel.Development);
    }
}
