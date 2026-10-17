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
            GUILayout.BeginArea(new Rect(10, 10, w, bootstrap.InRoom ? 560f : 420f), GUI.skin.box);

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

            RoleTable table = RoleTable.Instance;
            if (table != null)
            {
                if (table.Phase == RoomPhase.Waiting) DrawLobby(nm, table);
                else DrawPlaying(nm, table);
            }

            GUILayout.Space(6);
            if (GUILayout.Button("나가기"))
                _ = bootstrap.LeaveAsync();
        }

        void DrawLobby(NetworkManager nm, RoleTable table)
        {
            ulong me = nm.LocalClientId;
            table.TryGetSlot(me, out PlayerSlot mine);

            GUILayout.Space(4);
            GUILayout.Label("-- 대기방 --");
            for (int i = 0; i < table.Slots.Count; i++)
            {
                PlayerSlot slot = table.Slots[i];
                CharacterDef def = CharacterCatalog.Get(slot.Character);
                string who = slot.ClientId == me ? "나" : $"플레이어 {slot.ClientId}";
                string role = slot.Role == RoleTable.BossRole ? "보스" : $"생존자 {slot.Role}";
                string pick = slot.Role == RoleTable.BossRole ? "" : def != null ? def.Name : "선택 안 함";

                Color previous = GUI.contentColor;
                if (def != null) GUI.contentColor = def.Color;
                GUILayout.Label($"{who}  {role}  {pick}");
                GUI.contentColor = previous;
            }

            GUILayout.Space(4);
            if (mine.Role == RoleTable.BossRole)
            {
                GUILayout.Label("보스는 캐릭터를 고르지 않습니다.");
            }
            else
            {
                GUILayout.Label("캐릭터 선택");
                foreach (CharacterDef def in CharacterCatalog.All)
                {
                    bool held = TryFindHolder(table, def.Id, out ulong holder);
                    bool isMine = held && holder == me;
                    string label = def.Name + (isMine ? "  (선택함)" : held ? $"  (플레이어 {holder})" : "");

                    GUI.enabled = !held || isMine;
                    Color previous = GUI.contentColor;
                    GUI.contentColor = def.Color;
                    if (GUILayout.Button(label)) table.RequestCharacter(def.Id);
                    GUI.contentColor = previous;
                    GUI.enabled = true;
                }

                GUI.enabled = mine.Character != CharacterIds.None;
                if (GUILayout.Button("선택 해제")) table.RequestCharacter(CharacterIds.None);
                GUI.enabled = true;
            }

            string notice = table.Notice;
            if (!string.IsNullOrEmpty(notice)) GUILayout.Label(notice);

            GUILayout.Space(4);
            if (nm.IsServer)
            {
                GUI.enabled = table.CanStart;
                if (GUILayout.Button("시작")) table.RequestStart();
                GUI.enabled = true;
                if (!table.CanStart) GUILayout.Label(StartBlockedReason(table));
            }
            else
            {
                GUILayout.Label("호스트가 시작하기를 기다리는 중...");
            }
        }

        void DrawPlaying(NetworkManager nm, RoleTable table)
        {
            GUILayout.Space(4);
            GUILayout.Label("-- 진행 중 --");
            if (nm.IsServer && table.Phase == RoomPhase.Playing && GUILayout.Button("대기방으로 돌아가기"))
                table.RequestEndMatch();
            if (table.Phase == RoomPhase.Finished) GUILayout.Label("판이 끝났습니다 (결과 화면)");
        }

        static bool TryFindHolder(RoleTable table, byte character, out ulong holder)
        {
            for (int i = 0; i < table.Slots.Count; i++)
            {
                if (table.Slots[i].Character == character)
                {
                    holder = table.Slots[i].ClientId;
                    return true;
                }
            }
            holder = 0;
            return false;
        }

        static string StartBlockedReason(RoleTable table)
        {
            for (int i = 0; i < table.Slots.Count; i++)
                if (table.Slots[i].Role != RoleTable.BossRole) return "모든 생존자가 캐릭터를 골라야 합니다.";
            return "생존자가 한 명 이상 필요합니다.";
        }
    }
}
