using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Godot;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Networking.Client;

namespace OpenFantasy.Client.Networking;

/// <summary>Explicit remote play. Only the disposable loopback probe accepts a pinned test certificate.</summary>
public partial class NativePlayerEntry : Node
{
    public override async void _Ready()
    {
        string[] args = OS.GetCmdlineUserArgs();
        FarmInputMap.InstallDefaults();
        if (args is ["--join", var invitation])
        {
            AddChild(new FarmGame { PlayerInvitationPath = invitation });
            return;
        }
        if (args is [var rosterMode, var rosterInvitation, var rosterCertificate, var mailbox]
            && rosterMode is "--roster-probe" or "--roster-expedition-probe")
        {
            GetWindow().Unfocusable = true;
            var rosterGame = new FarmGame
            {
                NetworkFixtureOpening = OpenFixture(rosterInvitation, rosterCertificate),
                StartPaused = true, BindWindowFocus = false
            };
            AddChild(rosterGame);
            AddChild(new NativeRosterPlayProbe(rosterGame, mailbox, rosterMode == "--roster-expedition-probe"));
            return;
        }
        bool background = args is ["--player-probe", _, _, _, "--background-render"];
        if (background)
        {
            args = args[..^1];
            GetWindow().Unfocusable = true;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        if (args is [var mode, var probeInvitation, var publicCertificate, var report]
            && mode is "--player-probe" or "--player-focus-probe")
        {
            bool focusProbe = mode == "--player-focus-probe";
            var game = new FarmGame
            {
                NetworkFixtureOpening = OpenFixture(probeInvitation, publicCertificate),
                StartPaused = !focusProbe, BindWindowFocus = focusProbe || !background
            };
            AddChild(game);
            AddChild(new NativePlayerPlayProbe(game, report, focusProbe));
            return;
        }
        GD.PushError("Player entry requires --join INVITATION or an explicit disposable player probe.");
        GetTree().Quit(1);
    }

    private static async Task<FarmClientSession> OpenFixture(string invitationPath, string certificatePath)
    {
        byte[] bytes = await File.ReadAllBytesAsync(invitationPath).ConfigureAwait(false);
        PlayerConnectionInvitation invitation = PlayerInvitationCodec.Decode(bytes);
        if (!invitation.Endpoint.IsLoopback) throw new InvalidDataException("The certificate fixture is loopback only.");
        using var certificate = new X509Certificate2(await File.ReadAllBytesAsync(certificatePath).ConfigureAwait(false));
        string fingerprint = certificate.GetCertHashString(HashAlgorithmName.SHA256);
        var socket = new ClientWebSocket();
        socket.Options.AddSubProtocol(PlayerWebSocketConnector.Subprotocol);
        socket.Options.RemoteCertificateValidationCallback = (_, actual, _, errors) => actual is not null
            && (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) == SslPolicyErrors.None
            && actual.GetCertHashString(HashAlgorithmName.SHA256) == fingerprint;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            await socket.ConnectAsync(invitation.Endpoint, deadline.Token).ConfigureAwait(false);
            if (socket.SubProtocol != PlayerWebSocketConnector.Subprotocol)
                throw new FormatException("The fixture server did not select the player protocol.");
            var client = await PlayerProtocolClient.ConnectAsync(new WebSocketPlayerTransport(socket), invitation, deadline.Token)
                .ConfigureAwait(false);
            return await FarmClientSession.AttachNetworkAsync(client, deadline.Token).ConfigureAwait(false);
        }
        catch { socket.Dispose(); throw; }
    }
}
