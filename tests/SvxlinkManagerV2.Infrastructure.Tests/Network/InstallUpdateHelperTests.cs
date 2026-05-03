using FluentAssertions;

namespace SvxlinkManagerV2.Infrastructure.Tests.Network;

/// <summary>
/// Vérifie la présence des éléments clés du helper Linux de mise à jour.
/// </summary>
public class InstallUpdateHelperTests
{
    [Fact]
    public void HelperScript_ShouldExistAndContainDetachedRunMode()
    {
        var scriptPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "deploy", "linux", "install-update.sh"));

        File.Exists(scriptPath).Should().BeTrue();

        var content = File.ReadAllText(scriptPath);
        content.Should().Contain("nohup \"$0\" --run");
        content.Should().Contain("apt-get install -y");
        content.Should().Contain("systemctl restart");
    }
}