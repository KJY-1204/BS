using Unity.Netcode;
using UnityEngine;

namespace BS
{
    /// <summary>Minimal IMGUI heads-up display: the local survivor's health and the down/revive state.</summary>
    public class CombatHud : MonoBehaviour
    {
        NetworkManager nm;
        Texture2D white;

        void Start()
        {
            nm = NetworkManager.Singleton;
            white = Texture2D.whiteTexture;
        }

        void OnGUI()
        {
            if (nm == null || !nm.IsListening || nm.LocalClient == null || nm.LocalClient.PlayerObject == null) return;

            PlayerCombat combat = nm.LocalClient.PlayerObject.GetComponent<PlayerCombat>();
            PlayerAvatar avatar = nm.LocalClient.PlayerObject.GetComponent<PlayerAvatar>();
            if (combat == null || avatar == null || avatar.Role == RoleTable.BossRole) return;

            const float width = 260f;
            const float height = 22f;
            float x = 10f;
            float y = Screen.height - 70f;

            Draw(new Rect(x, y, width, height), new Color(0f, 0f, 0f, 0.6f));
            float fraction = Mathf.Clamp01(combat.Health / (float)combat.MaxHealth);
            Draw(new Rect(x + 2f, y + 2f, (width - 4f) * fraction, height - 4f), Color.Lerp(new Color(0.95f, 0.25f, 0.2f), new Color(0.3f, 0.9f, 0.35f), fraction));
            GUI.Label(new Rect(x + 6f, y + 1f, width, height), $"HP {combat.Health} / {combat.MaxHealth}");

            if (!combat.IsDead) return;

            GUI.Label(new Rect(x, y - 44f, 520f, 40f), "쓰러짐 - 동료가 근처에서 가만히 서 있으면 부활합니다");
            Draw(new Rect(x, y - 20f, width, 12f), new Color(0f, 0f, 0f, 0.6f));
            Draw(new Rect(x + 1f, y - 19f, (width - 2f) * combat.ReviveGauge, 10f), new Color(0.35f, 0.7f, 1f));
        }

        void Draw(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, white);
            GUI.color = previous;
        }
    }
}
