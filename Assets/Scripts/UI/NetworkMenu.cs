using Unity.Netcode;
using UnityEngine;

namespace BS
{
    /// <summary>Minimal IMGUI test menu: host, join by code, direct local test, and room status.</summary>
    public class NetworkMenu : MonoBehaviour
    {
        [SerializeField] SessionBootstrap bootstrap;

        bool hostAsBoss;
        string codeInput = "";
        string directAddress = "127.0.0.1";

        void OnGUI()
        {
            const float w = 300f;
            GUILayout.BeginArea(new Rect(10, 10, w, 420), GUI.skin.box);

            if (bootstrap.InRoom)
                DrawRoom();
            else
                DrawMenu();

            if (!string.IsNullOrEmpty(bootstrap.Status))
                GUILayout.Label(bootstrap.Status);

            GUILayout.EndArea();
        }

        void DrawMenu()
        {
            GUI.enabled = !bootstrap.Busy;

            GUILayout.Label("방 만들기 (Relay)");
            hostAsBoss = GUILayout.Toggle(hostAsBoss, "보스로 시작");
            if (GUILayout.Button("Host"))
                _ = bootstrap.HostRelayAsync(hostAsBoss);

            GUILayout.Space(8);
            GUILayout.Label("접속 코드로 참가");
            codeInput = GUILayout.TextField(codeInput, 12);
            if (GUILayout.Button("Join"))
                _ = bootstrap.JoinRelayAsync(codeInput);

            GUILayout.Space(8);
            GUILayout.Label("직접 접속 (로컬 테스트)");
            directAddress = GUILayout.TextField(directAddress, 40);
            if (GUILayout.Button("Host (직접)"))
                bootstrap.HostDirect(hostAsBoss);
            if (GUILayout.Button("Join (직접)"))
                bootstrap.JoinDirect(directAddress);

            GUI.enabled = true;
        }

        void DrawRoom()
        {
            var nm = NetworkManager.Singleton;
            GUILayout.Label(nm.IsServer ? "호스트" : "클라이언트");
            if (!string.IsNullOrEmpty(bootstrap.JoinCode))
                GUILayout.Label($"접속 코드: {bootstrap.JoinCode}");
            GUILayout.Label($"접속자: {nm.ConnectedClientsIds.Count}/{SessionBootstrap.MaxPlayers}");

            if (GUILayout.Button("나가기"))
                _ = bootstrap.LeaveAsync();
        }
    }
}
