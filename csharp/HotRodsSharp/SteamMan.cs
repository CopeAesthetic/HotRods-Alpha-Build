using Steamworks;
using Steamworks.Data;

namespace HotRodsSharp;

/// <summary>
/// Steamworks integration scaffold (Facepunch.Steamworks 2.3.3). Everything
/// degrades gracefully: when Steam is not running, or the native steam_api
/// library is missing (macOS/Linux builds take the natives from the official
/// Steamworks SDK), Init() leaves Ready=false and the game plays offline
/// exactly as before. Lobby + P2P plumbing is wired for the multiplayer
/// milestone.
/// </summary>
public static class SteamMan
{
    private const uint DevAppId = 480;   // Spacewar; swap for the real HotRods AppID

    public static bool Ready { get; private set; }
    public static Lobby? CurrentLobby { get; private set; }

    public static void Init()
    {
        try
        {
            SteamClient.Init(DevAppId);
            Ready = SteamClient.IsValid;
        }
        catch (DllNotFoundException)
        {
            Ready = false;   // steam_api native missing - offline build
        }
        if (!Ready)
        {
            Console.WriteLine("[steam] SteamAPI unavailable - running offline");
            return;
        }
        SteamNetworking.OnP2PSessionRequest += AcceptP2P;
        SetPresence("menu");
        Console.WriteLine("[steam] SteamAPI ready - " + SteamClient.Name);
    }

    public static void Shutdown()
    {
        if (!Ready)
            return;
        LeaveLobby();
        SteamClient.Shutdown();
        Ready = false;
    }

    /// <summary>Pump Steam callbacks once per frame.</summary>
    public static void RunCallbacks()
    {
        if (Ready)
            SteamClient.RunCallbacks();
    }

    /// <summary>Rich presence: "menu", "fishing at Bongwater Pond", ...</summary>
    public static void SetPresence(string where)
    {
        if (Ready)
            SteamFriends.SetRichPresence("status", where);
    }

    // ---------------------------------------------------------------- lobby --
    public static void CreateLobby()
    {
        if (!Ready)
            return;
        SteamMatchmaking.CreateLobbyAsync(8).ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully && t.Result is { } lobby)
            {
                CurrentLobby = lobby;
                SetPresence("lobby");
                Console.WriteLine("[steam] lobby created, max members " + lobby.MaxMembers);
            }
        }, TaskScheduler.Default);
    }

    public static void JoinLobby(SteamId id)
    {
        if (!Ready || id == default)
            return;
        SteamMatchmaking.JoinLobbyAsync(id).ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully && t.Result is { } lobby)
            {
                CurrentLobby = lobby;
                SetPresence("lobby");
            }
        }, TaskScheduler.Default);
    }

    public static void LeaveLobby()
    {
        if (!Ready || CurrentLobby is not { } lobby)
            return;
        lobby.Leave();
        CurrentLobby = null;
    }

    private static void AcceptP2P(SteamId id) =>
        SteamNetworking.AcceptP2PSessionWithUser(id);

    // ------------------------------------------------------------------- p2p --
    /// <summary>Reliable broadcast to every other lobby member (stub for
    /// the host-authoritative sync milestone).</summary>
    public static void Send(byte[] data)
    {
        if (!Ready || CurrentLobby is not { } lobby)
            return;
        foreach (var member in lobby.Members)
        {
            if (member.Id != SteamClient.SteamId)
                SteamNetworking.SendP2PPacket(member.Id, data, data.Length, 0,
                                              P2PSend.Reliable);
        }
    }

    /// <summary>Pop one pending packet, if any.</summary>
    public static bool Receive(out byte[] data, out SteamId from)
    {
        data = [];
        from = default;
        if (!Ready)
            return false;
        if (!SteamNetworking.IsP2PPacketAvailable(0))
            return false;
        var buf = new byte[32 * 1024];   // scaffold cap; real sync frames are tiny
        uint size = 0;
        if (!SteamNetworking.ReadP2PPacket(buf, ref size, ref from, 0))
            return false;
        data = buf[..(int)Math.Min(size, buf.Length)];
        return true;
    }
}
