using Unity.Netcode;
using UnityEngine;

namespace BS
{
    /// <summary>
    /// Minimal IMGUI display for growth: experience and mana bars, the two skill slots, the skill choice panel
    /// with its countdown, and every survivor's level over their character.
    /// </summary>
    public class GrowthHud : MonoBehaviour
    {
        NetworkManager nm;
        Texture2D white;
        GUIStyle centered;
        GUIStyle small;
        GUIStyle levelLabel;

        void Start()
        {
            nm = NetworkManager.Singleton;
            white = Texture2D.whiteTexture;
        }

        void OnGUI()
        {
            if (nm == null || !nm.IsListening) return;

            EnsureStyles();
            DrawLevelsOverCharacters();

            if (nm.LocalClient == null || nm.LocalClient.PlayerObject == null) return;
            PlayerGrowth growth = nm.LocalClient.PlayerObject.GetComponent<PlayerGrowth>();
            PlayerAvatar avatar = nm.LocalClient.PlayerObject.GetComponent<PlayerAvatar>();
            if (growth == null || avatar == null || avatar.Role == RoleTable.BossRole) return;

            DrawBars(growth);
            DrawSkillSlots(growth, avatar.CharacterId);
            if (growth.OfferOpen) DrawOffer(growth);
        }

        void EnsureStyles()
        {
            if (centered != null) return;

            centered = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = 13 };
            small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = 11 };
            levelLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12, fontStyle = FontStyle.Bold };
        }

        // ---- Bars ----

        void DrawBars(PlayerGrowth growth)
        {
            const float width = 260f;
            float x = 10f;
            float manaY = Screen.height - 44f;
            float xpY = Screen.height - 22f;

            Fill(new Rect(x, manaY, width, 18f), new Color(0f, 0f, 0f, 0.6f));
            float mana = growth.MaxMana > 0 ? Mathf.Clamp01(growth.Mana / (float)growth.MaxMana) : 0f;
            Fill(new Rect(x + 2f, manaY + 2f, (width - 4f) * mana, 14f), new Color(0.25f, 0.5f, 1f));
            GUI.Label(new Rect(x + 6f, manaY, width, 18f), $"MP {growth.Mana} / {growth.MaxMana}");

            Fill(new Rect(x, xpY, width, 12f), new Color(0f, 0f, 0f, 0.6f));
            float xp = growth.XpNeeded > 0 ? Mathf.Clamp01(growth.Xp / (float)growth.XpNeeded) : 1f;
            Fill(new Rect(x + 1f, xpY + 1f, (width - 2f) * xp, 10f), new Color(0.95f, 0.8f, 0.2f));

            string level = growth.IsMaxLevel ? $"Lv {growth.Level} (최대)" : $"Lv {growth.Level}   {growth.Xp} / {growth.XpNeeded}";
            GUI.Label(new Rect(x + width + 8f, xpY - 6f, 220f, 22f), level);
        }

        // ---- Skill slots ----

        void DrawSkillSlots(PlayerGrowth growth, byte character)
        {
            const float size = 84f;
            const float gap = 12f;
            float startX = Screen.width * 0.5f - (size * SkillCatalog.SkillSlots + gap * (SkillCatalog.SkillSlots - 1)) * 0.5f;
            float y = Screen.height - size - 14f;

            for (int slot = 0; slot < SkillCatalog.SkillSlots; slot++)
            {
                SkillDef def = SkillCatalog.ForSlot(slot, character);
                Rect box = new Rect(startX + slot * (size + gap), y, size, size);
                int rank = def != null ? growth.RankOf(def.Id) : 0;
                float cooldown = growth.CooldownLeft(slot);

                bool learned = rank > 0;
                bool shortOfMana = learned && def != null && growth.Mana < def.ManaCost;

                Fill(box, learned ? new Color(0.12f, 0.14f, 0.2f, 0.9f) : new Color(0.08f, 0.08f, 0.08f, 0.8f));
                if (cooldown > 0f && def != null && def.Cooldown > 0f)
                {
                    float fraction = Mathf.Clamp01(cooldown / def.Cooldown);
                    Fill(new Rect(box.x, box.y + box.height * (1f - fraction), box.width, box.height * fraction), new Color(0f, 0f, 0f, 0.65f));
                }
                if (shortOfMana) Fill(new Rect(box.x, box.y, box.width, 4f), new Color(0.9f, 0.2f, 0.2f));

                string key = slot == 0 ? "Q" : "E";
                GUI.Label(new Rect(box.x + 4f, box.y + 2f, 24f, 20f), key);
                string name = def != null ? def.Name : "-";
                GUI.Label(new Rect(box.x, box.y + 20f, box.width, 22f), learned ? name : "미습득", small);
                if (learned) GUI.Label(new Rect(box.x, box.y + 40f, box.width, 20f), cooldown > 0f ? $"{cooldown:F1}s" : $"랭크 {rank}", small);
            }

            string notice = growth.Notice;
            if (!string.IsNullOrEmpty(notice))
                GUI.Label(new Rect(Screen.width * 0.5f - 160f, y - 28f, 320f, 24f), notice, centered);
        }

        // ---- Skill choice ----

        void DrawOffer(PlayerGrowth growth)
        {
            const float boxW = 190f;
            const float boxH = 110f;
            const float gap = 14f;
            int count = growth.OfferCount;
            float totalW = boxW * count + gap * (count - 1);
            float startX = Screen.width * 0.5f - totalW * 0.5f;
            float y = Screen.height * 0.28f;

            GUI.Label(new Rect(startX, y - 54f, totalW, 22f), "레벨 업! 스킬을 고르세요 (숫자 키 1, 2, 3)", centered);
            GUI.Label(new Rect(startX, y - 32f, totalW, 22f), $"남은 시간 {growth.OfferSecondsLeft:F1}초 - 시간이 지나면 무작위로 선택됩니다", small);

            for (int i = 0; i < count; i++)
            {
                SkillDef def = SkillCatalog.Get(growth.OfferChoice(i));
                Rect box = new Rect(startX + i * (boxW + gap), y, boxW, boxH);
                Fill(box, new Color(0.1f, 0.12f, 0.2f, 0.95f));
                Fill(new Rect(box.x, box.y, box.width, 3f), new Color(0.95f, 0.8f, 0.2f));

                if (def == null) continue;
                int rank = growth.RankOf(def.Id);
                GUI.Label(new Rect(box.x + 6f, box.y + 6f, box.width, 20f), $"[{i + 1}]  {def.Name}");
                GUI.Label(new Rect(box.x + 6f, box.y + 28f, box.width - 12f, 36f), def.Description, small);
                GUI.Label(new Rect(box.x + 6f, box.y + 70f, box.width - 12f, 36f),
                          rank > 0 ? $"랭크 {rank} -> {rank + 1} / {def.MaxRank}" : $"새로 배움 (최대 {def.MaxRank})", small);
            }
        }

        // ---- Levels over characters ----

        void DrawLevelsOverCharacters()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            for (int i = 0; i < PlayerGrowth.All.Count; i++)
            {
                PlayerGrowth g = PlayerGrowth.All[i];
                if (g == null) continue;
                PlayerAvatar a = g.GetComponent<PlayerAvatar>();
                if (a == null || a.Role == RoleTable.BossRole) continue;

                Vector3 screen = cam.WorldToScreenPoint(g.transform.position + new Vector3(0f, 1.15f, 0f));
                if (screen.z < 0f) continue;

                CharacterDef def = CharacterCatalog.Get(a.CharacterId);
                Rect label = new Rect(screen.x - 60f, Screen.height - screen.y - 10f, 120f, 20f);
                GUI.Label(label, def != null ? $"{def.Name}  Lv {g.Level}" : $"Lv {g.Level}", levelLabel);
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
