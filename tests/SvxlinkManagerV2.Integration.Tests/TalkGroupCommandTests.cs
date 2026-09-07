using FluentAssertions;
using Xunit.Abstractions;

namespace SvxlinkManagerV2.Integration.Tests;

/// <summary>
/// Vérifie que les commandes talkgroup du protocole V3 sont réellement atteignables :
/// une séquence DTMF composée sur la logique simplex doit traverser Logic.tcl, le lien
/// déclaré par <c>CONNECT_LOGICS</c>, et aboutir dans <c>ReflectorLogic</c>.
/// </summary>
/// <remarks>
/// C'est le trou qu'a révélé #144 : la configuration générée déclarait
/// <c>CONNECT_LOGICS=SimplexLogic,ReflectorLogic</c>, sans le champ « commande ».
/// <c>LinkManager::addLogic</c> conditionne la création du <c>LinkCmd</c> à
/// <c>atoi(cmd) &gt; 0</c> : avec un champ vide, aucune commande talkgroup n'atteignait
/// <c>ReflectorLogic::remoteCmdReceived</c>, ni par radio ni par le PTY. Les tests
/// unitaires ne couvrent que la <em>génération</em> du fichier : seule la stack montre
/// que SVXLink honore le préfixe.
///
/// Retirer le <c>:35</c> de <c>svxlink-config-node3/svxlink.conf</c>, ou faire retourner 1
/// à Logic.tcl pour ces séquences, fait tomber ces tests.
/// </remarks>
[Collection(DockerComposeCollection.Name)]
[Trait("Category", "Integration")]
public class TalkGroupCommandTests
{
    private const string NodeV3 = "svxlink-node3";
    private const string NodeV3Callsign = "HB9GXP3-H";

    /// <summary>Pseudo-terminal DTMF du nœud, tel que déclaré par <c>DTMF_CTRL_PTY</c>.</summary>
    private const string DtmfPty = "/tmp/dtmf_uhf";

    /// <summary>
    /// TG surveillé par le nœud (<c>MONITOR_TGS</c>), distinct de son <c>DEFAULT_TG</c> :
    /// une sélection vers ce TG est donc un changement observable.
    /// </summary>
    private const int TargetTalkGroup = 240;

    private static readonly TimeSpan LinkTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(45);

    private readonly DockerComposeStack _stack;
    private readonly ITestOutputHelper _output;

    public TalkGroupCommandTests(DockerComposeStack stack, ITestOutputHelper output)
    {
        _stack = stack;
        _output = output;
    }

    [DockerComposeFact]
    public async Task NodeV3Configuration_ShouldDeclareACommandPrefixOnTheSimplexLogic()
    {
        var result = await _stack.ExecAsync(NodeV3, "grep CONNECT_LOGICS /etc/svxlink/svxlink.conf");

        result.ExitCode.Should().Be(0);
        result.Output.Should().Contain(
            "SimplexLogic:35",
            "sans champ « commande », LinkManager::addLogic ne crée aucun LinkCmd");
    }

    [DockerComposeFact]
    public async Task SelectTalkGroupCommand_ShouldReachReflectorLogic()
    {
        await WaitForNodeReadyAsync();

        // « 351240# » : préfixe 35, sous-commande 1 (sélection), talkgroup 240.
        await SendDtmfAsync($"351{TargetTalkGroup}#");

        var routed = await _stack.WaitForLogAsync(
            NodeV3, $"DTMF_CMD:351{TargetTalkGroup}", CommandTimeout);
        routed.Should().NotBeNull("Logic.tcl doit voir passer la séquence composée");

        var selected = await _stack.WaitForLogAsync(
            NodeV3, $"Selecting TG #{TargetTalkGroup}", CommandTimeout);
        selected.Should().NotBeNull(
            $"la sélection du talkgroup {TargetTalkGroup} doit aboutir dans ReflectorLogic");

        _output.WriteLine(selected);
    }

    [DockerComposeFact]
    public async Task StatusCommand_ShouldTriggerTheTalkGroupAnnouncement()
    {
        await WaitForNodeReadyAsync();

        // « 35*# » : le * fait partie de la commande, c'est le # qui la termine.
        await SendDtmfAsync("35*#");

        var routed = await _stack.WaitForLogAsync(NodeV3, "DTMF_CMD:35*", CommandTimeout);
        routed.Should().NotBeNull("Logic.tcl doit voir passer la séquence composée");

        var logs = await _stack.GetLogsAsync(NodeV3);
        logs.Should().NotContain(
            "invalid command name \"ReflectorLogic::report_tg_status\"",
            "EVENT_HANDLER doit pointer sur events.tcl : pointer events.d/local/Logic.tcl " +
            "directement prive l'interpréteur du namespace ReflectorLogic, et aucune " +
            "annonce de talkgroup n'est alors jouée");
    }

    /// <summary>
    /// Le préfixe seul ne doit rien déclencher : <c>LinkManager::cmdReceived</c>
    /// interpréterait la sous-commande vide comme une désactivation du lien, ce qui
    /// couperait l'audio vers le réflecteur sur une simple faute de frappe.
    /// </summary>
    [DockerComposeFact]
    public async Task PrefixAlone_ShouldNotDeactivateTheLink()
    {
        await WaitForNodeReadyAsync();

        await SendDtmfAsync("35#");
        await Task.Delay(TimeSpan.FromSeconds(5));

        var logs = await _stack.GetLogsAsync(NodeV3);
        logs.Should().NotContain(
            "Deactivating link LinkToReflector",
            "Logic.tcl ne rend à SVXLink que les séquences suivies d'une sous-commande");
    }

    private async Task WaitForNodeReadyAsync()
    {
        var login = await _stack.WaitForLogAsync(
            "svxreflector", $"{NodeV3Callsign}: Login OK", LinkTimeout);

        login.Should().NotBeNull("le nœud doit être relié avant d'éprouver ses commandes");
    }

    private async Task SendDtmfAsync(string sequence)
    {
        // printf plutôt qu'echo : pas de saut de ligne parasite dans le flux de digits.
        var result = await _stack.ExecAsync(NodeV3, $"printf '{sequence}' > {DtmfPty}");

        result.ExitCode.Should().Be(0,
            $"l'écriture dans {DtmfPty} doit réussir — le PTY n'existe que si " +
            $"DTMF_CTRL_PTY est déclaré sur la logique simplex. Sortie : {result.Output}");
    }
}
