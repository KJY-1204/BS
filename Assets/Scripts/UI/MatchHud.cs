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
        GUIStyle tableLine;

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
                TrackPhase(table);
                DrawTopBar(table);
                DrawPhaseBanner();
                DrawRescuePoint(table);
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
            tableLine = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13 };
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

        // ---- The phase of the match: a banner when it changes, the current one by the boss's bar ----

        /// <summary>How long the banner of a new phase stays on the screen.</summary>
        const float BannerSeconds = 4f;

        int shownPhase = BossPhases.First;
        float bannerUntil;

        /// <summary>The boss bar's text: the kind's name, its health and the phase.</summary>
        public static string BossBarText(string name, int health, int maxHealth, int phase) => $"{name}  {health} / {maxHealth}   국면 {phase}";

        /// <summary>The banner when the match enters a phase.</summary>
        public static string PhaseBannerText(int phase) => $"국면 {phase}";

        void TrackPhase(RoleTable table)
        {
            int phase = table.BossPhase;
            if (phase == shownPhase) return;
            if (phase > shownPhase && phase > BossPhases.First) bannerUntil = Time.unscaledTime + BannerSeconds;
            shownPhase = phase; // a lower number is a new match
        }

        void DrawPhaseBanner()
        {
            if (Time.unscaledTime >= bannerUntil || shownPhase <= BossPhases.First) return;
            GUI.Label(new Rect(Screen.width * 0.5f - 200f, Screen.height * 0.2f, 400f, 44f), PhaseBannerText(shownPhase), big);
        }

        // ---- The rescue point, for everyone ----

        /// <summary>The rescue point bar's text: the progress in percent and the seconds left before it closes.</summary>
        public static string RescueBarText(float progress, int secondsLeft) => $"구호 지점  {Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f)}%  ({secondsLeft}초)";

        void DrawRescuePoint(RoleTable table)
        {
            if (!table.RescueOpen) return;

            const float width = 300f;
            float x = Screen.width * 0.5f - width * 0.5f;
            float y = 92f;
            float fraction = Mathf.Clamp01(table.RescueProgress);
            Fill(new Rect(x, y, width, 16f), new Color(0f, 0f, 0f, 0.65f));
            Fill(new Rect(x + 2f, y + 2f, (width - 4f) * fraction, 12f), new Color(0.3f, 0.85f, 0.9f));
            GUI.Label(new Rect(x, y + 14f, width, 20f), RescueBarText(fraction, table.RescueSecondsLeft), center);

            // The circle in the world: dots along its outline, placed from the camera's view of the centre and the radius.
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 c = cam.WorldToScreenPoint(table.RescueCenter);
            Vector3 ex = cam.WorldToScreenPoint(table.RescueCenter + new Vector2(RescueConfig.Radius, 0f));
            Vector3 ey = cam.WorldToScreenPoint(table.RescueCenter + new Vector2(0f, RescueConfig.Radius));
            float rx = Mathf.Abs(ex.x - c.x);
            float ry = Mathf.Abs(ey.y - c.y);
            const int dots = 48;
            for (int i = 0; i < dots; i++)
            {
                float angle = i * Mathf.PI * 2f / dots;
                float px = c.x + Mathf.Cos(angle) * rx;
                float py = Screen.height - (c.y + Mathf.Sin(angle) * ry);
                Fill(new Rect(px - 2f, py - 2f, 4f, 4f), new Color(0.3f, 0.85f, 0.9f, 0.9f));
            }
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
            GUI.Label(new Rect(x, y + 55f, width, 20f), BossBarText(BossCatalog.Current.Name, boss.Combat.Health, boss.Combat.MaxHealth, table.BossPhase), center);
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
            MatchSummary summary = table.MatchHasBoss ? table.Summary : null;
            int lines = summary != null ? summary.Survivors.Count + 1 : 0;
            float tableHeight = lines > 0 ? 12f + lines * 24f : 0f;
            const float width = 520f;
            float height = 190f + tableHeight;
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

            if (summary != null)
            {
                float lineTop = box.y + 124f;
                for (int i = 0; i < summary.Survivors.Count; i++)
                    GUI.Label(new Rect(box.x + 16f, lineTop + i * 24f, width - 32f, 24f), SurvivorLine(summary.Survivors[i]), tableLine);
                GUI.Label(new Rect(box.x + 16f, lineTop + summary.Survivors.Count * 24f, width - 32f, 24f), MatchLine(summary), tableLine);
            }

            float below = 130f + tableHeight;
            if (net.IsServer)
            {
                if (GUI.Button(new Rect(box.x + width * 0.5f - 110f, box.y + below, 220f, 36f), "대기방으로 돌아가기"))
                    table.RequestEndMatch();
            }
            else
            {
                GUI.Label(new Rect(box.x, box.y + below + 4f, width, 28f), "호스트가 대기방으로 돌아가기를 기다리는 중...", center);
            }
        }

        /// <summary>m:ss for a length in seconds.</summary>
        public static string FormatLength(int seconds) => $"{seconds / 60}:{seconds % 60:00}";

        /// <summary>The boss's health left as a percent: zero only when it has none, otherwise at least one.</summary>
        public static int BossPercent(int health, int maxHealth)
        {
            if (health <= 0 || maxHealth <= 0) return 0;
            return Mathf.Clamp((int)((long)health * 100 / maxHealth), 1, 100);
        }

        public static string SurvivorLine(SurvivorSummary v)
        {
            CharacterDef def = CharacterCatalog.Get(v.Character);
            return $"{def.Name} (생존자 {v.Role})  Lv {v.Level}  쓰러짐 {v.Falls}  부활 {v.Revives}  {(v.Alive ? "서 있음" : "쓰러짐")}";
        }

        public static string MatchLine(MatchSummary s) =>
            $"판 길이 {FormatLength(s.LengthSeconds)}   보스 남은 체력 {BossPercent(s.BossHealth, s.BossMaxHealth)}%   장치 설치 {s.DevicesPlaced} / 파괴 {s.DevicesDestroyed}";

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
