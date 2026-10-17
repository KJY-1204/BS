using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace BS
{
    /// <summary>
    /// Starts, joins and leaves a room. Relay rooms go through Unity Multiplayer Services;
    /// direct (IP) mode needs no Unity Dashboard setup and is meant for local testing.
    /// </summary>
    public class SessionBootstrap : MonoBehaviour
    {
        public const int MaxPlayers = 5;
        const ushort DirectPort = 7777;

        public string Status { get; private set; } = "";
        public string JoinCode { get; private set; } = "";
        public bool Busy { get; private set; }

        public bool InRoom => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        ISession session;
        bool servicesReady;

        void Start()
        {
            var nm = NetworkManager.Singleton;
            nm.ConnectionApprovalCallback = Approve;
            nm.OnClientDisconnectCallback += OnClientDisconnected;

            if (DevArgs.Host) HostDirect(DevArgs.HostAsBoss);
            else if (DevArgs.HostRelay) _ = HostRelayAsync(DevArgs.HostAsBoss);
            else if (!string.IsNullOrEmpty(DevArgs.Join)) JoinDirect(DevArgs.Join);
            else if (!string.IsNullOrEmpty(DevArgs.JoinCode)) _ = JoinRelayAsync(DevArgs.JoinCode);
            if (DevArgs.QuitAfter > 0f) Invoke(nameof(QuitApp), DevArgs.QuitAfter);
        }

        void QuitApp()
        {
            Debug.Log("[BS] dev quit");
            Application.Quit();
        }

        void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false;
            int connected = NetworkManager.Singleton.ConnectedClientsIds.Count;
            bool playing = RoleTable.Instance != null && RoleTable.Instance.Phase == RoomPhase.Playing;
            response.Approved = connected < MaxPlayers && !playing;
            if (!response.Approved) response.Reason = playing ? "이미 게임이 진행 중입니다." : "방이 가득 찼습니다.";
            Debug.Log($"[BS] connection request from client {request.ClientNetworkId}: " +
                      $"{(response.Approved ? "approved" : playing ? "rejected (match running)" : "rejected (room full)")} ({connected}/{MaxPlayers})");
        }

        void OnClientDisconnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm.IsServer || clientId != nm.LocalClientId) return;

            string reason = nm.DisconnectReason;
            Debug.Log($"[BS] disconnected from room: {(string.IsNullOrEmpty(reason) ? "(no reason)" : reason)}");
            // Transport-level reasons are technical; only show a reason the server gave on purpose.
            bool serverGaveReason = !string.IsNullOrEmpty(reason) && !reason.StartsWith("[Disconnect Event]");
            _ = LeaveAsync(serverGaveReason ? reason : "호스트와의 연결이 끊겼습니다.");
        }

        public async Task HostRelayAsync(bool asBoss)
        {
            if (Busy || InRoom) return;
            Busy = true;
            try
            {
                await EnsureServicesAsync();
                RoleTable.HostWantsBoss = asBoss;
                Status = "방을 만드는 중...";

                var options = new SessionOptions { MaxPlayers = MaxPlayers }.WithRelayNetwork();
                var host = await MultiplayerService.Instance.CreateSessionAsync(options);
                session = host;
                JoinCode = host.Code;
                Status = "방이 열렸습니다.";
                if (DevArgs.HostRelay) Debug.Log($"[BS] relay room code {host.Code}");
            }
            catch (Exception e)
            {
                Fail("방 만들기 실패", e);
            }
            finally { Busy = false; }
        }

        public async Task JoinRelayAsync(string code)
        {
            if (Busy || InRoom) return;
            Busy = true;
            try
            {
                code = (code ?? "").Trim().ToUpperInvariant();
                if (code.Length == 0) { Status = "접속 코드를 입력하세요."; return; }

                await EnsureServicesAsync();
                Status = "접속하는 중...";
                session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                Status = "접속했습니다.";
            }
            catch (Exception e)
            {
                Fail("접속 실패", e);
            }
            finally { Busy = false; }
        }

        public void HostDirect(bool asBoss)
        {
            if (Busy || InRoom) return;
            RoleTable.HostWantsBoss = asBoss;
            ConfigureDirect("0.0.0.0");
            JoinCode = "(직접 접속: 127.0.0.1)";
            Status = NetworkManager.Singleton.StartHost() ? "직접 접속 방이 열렸습니다." : "호스트 시작 실패";
        }

        public void JoinDirect(string address)
        {
            if (Busy || InRoom) return;
            ConfigureDirect(address, "0.0.0.0");
            Status = NetworkManager.Singleton.StartClient() ? "접속하는 중..." : "접속 시작 실패";
        }

        public async Task LeaveAsync(string message = "")
        {
            var current = session;
            session = null;
            JoinCode = "";

            try
            {
                if (current != null)
                {
                    if (current is IHostSession host) await host.DeleteAsync();
                    else await current.LeaveAsync();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Leaving session failed: {e.Message}");
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();

            Status = message;
            Debug.Log($"[BS] left room (listening={InRoom}): {message}");
        }

        void ConfigureDirect(string address, string listen = null)
        {
            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            utp.SetConnectionData(address, DirectPort, listen ?? address);
        }

        async Task EnsureServicesAsync()
        {
            if (!servicesReady)
            {
                // Distinct profile per editor instance so virtual players do not share one account.
                var init = new InitializationOptions();
                init.SetProfile(Guid.NewGuid().ToString("N").Substring(0, 20));
                await UnityServices.InitializeAsync(init);
                servicesReady = true;
            }

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        void Fail(string prefix, Exception e)
        {
            Debug.LogError($"{prefix}: {e}");
            Status = $"{prefix}: {e.Message}";
            session = null;
            JoinCode = "";
        }
    }
}
