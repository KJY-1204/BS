using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace BS
{
    /// <summary>
    /// Minimal IMGUI display of the match: the remaining time and the boss's health at the top for everyone, a summary of
    /// the survivors for the boss player, and the result screen once the match has ended.
    /// </summary>
    public class MatchHud : MonoBehaviour
    {
        NetworkManager nm;
        Texture2D white;
        GUIStyle big;
        GUIStyle center;
        GUIStyle small;

        void Start()
        {
            nm = NetworkManager.Singleton;
            white = Texture2D.whiteTexture;
        }

        void OnGUI()
        {
            if (nm == null || !nm.IsListening) return;
            RoleTable table = RoleTable.Instance;
            if (table == null || table.Phase == RoomPhase.Waiting) return;

            EnsureStyles();

            // Practice matches have no boss: nothing here, as before.
            if (table.MatchHasBoss)
            {
                DrawTopBar(table);
                if (IsLocalBoss()) DrawSurvivorSummary();
            }

            if (table.Phase == RoomPhase.Finished) DrawResult(table, nm);
        }

        void EnsureStyles()
        {
            if (big != null) return;

            big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 28, fontStyle = FontStyle.Bold };
            center = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 15 };
            small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontSize = 12 };
        }

        bool IsLocalBoss() =>
            nm.LocalClient != null && nm.LocalClient.PlayerObject != null &&
            nm.LocalClient.PlayerObject.GetComponent<PlayerAvatar>() is PlayerAvatar a && a.Role == RoleTable.BossRole;

        static PlayerAvatar FindBoss()
        {
            for (int i = 0; i < PlayerAvatar.All.Count; i++)
                if (PlayerAvatar.All[i] != null && PlayerAvatar.All[i].Role == RoleTable.BossRole) return PlayerAvatar.All[i];
            return null;
        }

        // ---- Time and the boss's health, for everyone ----

        void DrawTopBar(RoleTable table)
        {
            const float width = 420f;
            float x = Screen.width * 0.5f - width * 0.5f;
            float y = 8f;

            int seconds = table.SecondsLeft;
            string clock = $"{seconds / 60}:{seconds % 60:00}";
            GUI.Label(new Rect(x, y, width, 34f), clock, big);

            PlayerAvatar boss = FindBoss();
            if (boss == null || boss.Combat == null) return;

            float fraction = boss.Combat.MaxHealth > 0 ? Mathf.Clamp01(boss.Combat.Health / (float)boss.Combat.MaxHealth) : 0f;
            Fill(new Rect(x, y + 38f, width, 16f), new Color(0f, 0f, 0f, 0.65f));
            Fill(new Rect(x + 2f, y + 40f, (width - 4f) * fraction, 12f), Color.Lerp(new Color(0.95f, 0.25f, 0.2f), new Color(0.95f, 0.6f, 0.2f), fraction));
            GUI.Label(new Rect(x, y + 55f, width, 20f), $"{BossCatalog.Default.Name}  {boss.Combat.Health} / {boss.Combat.MaxHealth}", center);
        }

        // ---- The boss player sees every survivor ----

        void DrawSurvivorSummary()
        {
            const float width = 230f;
            const float rowHeight = 30f;
            float x = Screen.width - width - 12f;
            float y = Screen.height * 0.5f - 80f;

            GUI.Label(new Rect(x, y - 24f, width, 22f), "생존자", small);

            int row = 0;
            for (int i = 0; i < PlayerAvatar.All.Count; i++)
            {
                PlayerAvatar a = PlayerAvatar.All[i];
                if (a == null || a.Role == RoleTable.BossRole || a.Combat == null) continue;

                CharacterDef def = CharacterCatalog.Get(a.CharacterId);
                float top = y + row * rowHeight;
                bool fallen = a.Combat.IsDead;
                float fraction = a.Combat.MaxHealth > 0 ? Mathf.Clamp01(a.Combat.Health / (float)a.Combat.MaxHealth) : 0f;

                Fill(new Rect(x, top, width, rowHeight - 4f), new Color(0f, 0f, 0f, 0.55f));
                Fill(new Rect(x + 2f, top + 2f, (width - 4f) * (fallen ? 0f : fraction), rowHeight - 8f),
                     fallen ? new Color(0.3f, 0.3f, 0.3f) : Color.Lerp(new Color(0.95f, 0.25f, 0.2f), new Color(0.3f, 0.9f, 0.35f), fraction));

                Color previous = GUI.contentColor;
                GUI.contentColor = def != null ? def.Color : Color.white;
                string label = $"{(def != null ? def.Name : "?")}  {(fallen ? "쓰러짐" : a.Combat.Health + " / " + a.Combat.MaxHealth)}";
                GUI.Label(new Rect(x + 6f, top, width - 8f, rowHeight - 4f), label, small);
                GUI.contentColor = previous;
                row++;
            }
        }

        // ---- The result ----

        void DrawResult(RoleTable table, NetworkManager net)
        {
            const float width = 460f;
            const float height = 190f;
            var box = new Rect(Screen.width * 0.5f - width * 0.5f, Screen.height * 0.5f - height * 0.5f - 40f, width, height);

            Fill(box, new Color(0.04f, 0.04f, 0.07f, 0.88f));
            Fill(new Rect(box.x, box.y, box.width, 4f), table.Winner == MatchWinner.Survivors ? new Color(0.3f, 0.9f, 0.4f) : new Color(0.95f, 0.3f, 0.25f));

            string headline = table.Winner == MatchWinner.Survivors ? "생존자 승리" : table.Winner == MatchWinner.Boss ? "보스 승리" : "게임 종료";
            GUI.Label(new Rect(box.x, box.y + 14f, width, 44f), headline, big);
            GUI.Label(new Rect(box.x, box.y + 62f, width, 26f), ReasonText(table.Reason), center);

            if (table.MatchHasBoss)
            {
                int seconds = table.SecondsLeft;
                GUI.Label(new Rect(box.x, box.y + 90f, width, 26f), $"남은 시간 {seconds / 60}:{seconds % 60:00}", center);
            }

            if (net.IsServer)
            {
                if (GUI.Button(new Rect(box.x + width * 0.5f - 110f, box.y + 130f, 220f, 36f), "대기방으로 돌아가기"))
                    table.RequestEndMatch();
            }
            else
            {
                GUI.Label(new Rect(box.x, box.y + 134f, width, 28f), "호스트가 대기방으로 돌아가기를 기다리는 중...", center);
            }
        }

        public static string ReasonText(MatchReason reason)
        {
            switch (reason)
            {
                case MatchReason.BossDefeated: return "보스를 쓰러뜨렸습니다";
                case MatchReason.BossLeft: return "보스가 나갔습니다";
                case MatchReason.AllSurvivorsFallen: return "생존자가 모두 쓰러졌습니다";
                case MatchReason.TimeUp: return "제한 시간이 끝났습니다";
                default: return "";
            }
        }

        void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, white);
            GUI.color = previous;
        }
    }
}
