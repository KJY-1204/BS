using Unity.Netcode;
using UnityEngine;

namespace BS
{
    /// <summary>
    /// Minimal IMGUI display of the boss's summoning, shown to the boss player only: the summon energy, the key hints, a notice when a
    /// summon is refused, the standing order as text, and a marker on the ordered survivor or at the ordered point.
    /// </summary>
    public class SummonHud : MonoBehaviour
    {
        NetworkManager nm;
        Texture2D white;
        GUIStyle center;
        GUIStyle small;

        static readonly Color EnergyColor = new Color(0.65f, 0.35f, 0.95f);
        static readonly Color MarkerColor = new Color(1f, 0.85f, 0.2f);

        void Start()
        {
            nm = NetworkManager.Singleton;
            white = Texture2D.whiteTexture;
        }

        void OnGUI()
        {
            if (nm == null || !nm.IsListening) return;
            RoleTable table = RoleTable.Instance;
            if (table == null || table.Phase != RoomPhase.Playing || !table.MatchHasBoss) return;

            // Every survivor has a PlayerSummon too, but only the boss player's copy means anything: survivors see none of this.
            PlayerSummon summon = PlayerSummon.Local;
            if (summon == null || !summon.IsSpawned) return;
            PlayerAvatar owner = summon.GetComponent<PlayerAvatar>();
            if (owner == null || owner.Role != RoleTable.BossRole) return;

            EnsureStyles();
            DrawEnergy(summon);
            DrawKinds(summon);
            DrawOrderText(summon);
            DrawMarker(summon);
        }

        void EnsureStyles()
        {
            if (center != null) return;
            center = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
            small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12 };
        }

        // ---- Energy and hints ----

        void DrawEnergy(PlayerSummon summon)
        {
            const float width = 320f;
            float x = Screen.width * 0.5f - width * 0.5f;
            float y = Screen.height - 74f;

            float fraction = summon.MaxEnergy > 0f ? Mathf.Clamp01(summon.Energy / summon.MaxEnergy) : 0f;
            Fill(new Rect(x, y, width, 18f), new Color(0f, 0f, 0f, 0.65f));
            Fill(new Rect(x + 2f, y + 2f, (width - 4f) * fraction, 14f), EnergyColor);
            GUI.Label(new Rect(x, y - 1f, width, 20f), $"소환 기운  {Mathf.FloorToInt(summon.Energy)} / {Mathf.FloorToInt(summon.MaxEnergy)}", center);
            GUI.Label(new Rect(x - 40f, y + 20f, width + 80f, 20f), "왼쪽 클릭 발사   Space 소환   1~5 종류   오른쪽 클릭 목표 지정   C 지정 해제", small);

            string notice = summon.Notice;
            if (!string.IsNullOrEmpty(notice))
            {
                Color previous = GUI.contentColor;
                GUI.contentColor = new Color(1f, 0.55f, 0.45f);
                GUI.Label(new Rect(x, y - 26f, width, 22f), notice, center);
                GUI.contentColor = previous;
            }
        }

        // ---- The kinds the boss can summon ----

        void DrawKinds(PlayerSummon summon)
        {
            const float cell = 104f;
            const float height = 22f;
            float x = Screen.width * 0.5f - cell * MobKinds.Count * 0.5f;
            float y = Screen.height - 124f;

            for (int k = 0; k < MobKinds.Count; k++)
            {
                MobKindDef def = MobKinds.Get((byte)k);
                Rect rect = new Rect(x + k * cell, y, cell - 4f, height);
                bool chosen = summon.SelectedKind == k;

                Fill(rect, chosen ? new Color(1f, 1f, 1f, 0.28f) : new Color(0f, 0f, 0f, 0.5f));
                Fill(new Rect(rect.x, rect.y, 5f, rect.height), def.Color);
                if (chosen)
                {
                    Fill(new Rect(rect.x, rect.y + rect.height - 2f, rect.width, 2f), MarkerColor);
                }

                // The cost shown is the cost of one mob at the default summon settings.
                int cost = Mathf.RoundToInt(SummonConfig.Default.CostPerMob * def.CostMultiple);
                GUI.Label(new Rect(rect.x + 6f, rect.y, rect.width - 6f, rect.height), $"{k + 1} {def.Name} ({cost})", small);
            }
        }

        // ---- The standing order ----

        void DrawOrderText(PlayerSummon summon)
        {
            string text;
            switch (summon.Order)
            {
                case OrderKind.Player:
                    PlayerAvatar target = FindAvatar(summon.OrderClient);
                    CharacterDef def = target != null ? CharacterCatalog.Get(target.CharacterId) : null;
                    text = $"명령: {(def != null ? def.Name : "생존자")} 추적";
                    break;
                case OrderKind.Point:
                    text = $"명령: ({summon.OrderPoint.x:F0}, {summon.OrderPoint.y:F0}) 지점으로 이동";
                    break;
                default:
                    text = "명령 없음 (가까운 생존자를 쫓음)";
                    break;
            }

            const float width = 320f;
            GUI.Label(new Rect(Screen.width * 0.5f - width * 0.5f, Screen.height - 98f, width, 22f), text, center);
        }

        void DrawMarker(PlayerSummon summon)
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 world;
            if (summon.Order == OrderKind.Player)
            {
                PlayerAvatar target = FindAvatar(summon.OrderClient);
                if (target == null) return;
                world = target.transform.position;
            }
            else if (summon.Order == OrderKind.Point)
            {
                world = summon.OrderPoint;
            }
            else return;

            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z < 0f) return;

            float x = screen.x;
            float y = Screen.height - screen.y;
            const float half = 22f;
            const float thick = 3f;
            Fill(new Rect(x - half, y - half, half * 2f, thick), MarkerColor);
            Fill(new Rect(x - half, y + half - thick, half * 2f, thick), MarkerColor);
            Fill(new Rect(x - half, y - half, thick, half * 2f), MarkerColor);
            Fill(new Rect(x + half - thick, y - half, thick, half * 2f), MarkerColor);
        }

        static PlayerAvatar FindAvatar(ulong clientId)
        {
            for (int i = 0; i < PlayerAvatar.All.Count; i++)
            {
                PlayerAvatar a = PlayerAvatar.All[i];
                if (a != null && a.Role != RoleTable.BossRole && a.OwnerClientId == clientId) return a;
            }
            return null;
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
