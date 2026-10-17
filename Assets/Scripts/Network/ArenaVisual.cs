using UnityEngine;

namespace BS
{
    /// <summary>Draws the arena floor and border as flat placeholder rectangles.</summary>
    public class ArenaVisual : MonoBehaviour
    {
        [SerializeField] Color floorColor = new Color(0.16f, 0.17f, 0.2f);
        [SerializeField] Color borderColor = new Color(0.5f, 0.5f, 0.55f);
        [SerializeField] float borderThickness = 0.25f;

        void Awake()
        {
            Sprite square = CreateSquareSprite();
            CreateRect("Floor", square, floorColor, Vector2.zero, new Vector2(ArenaConfig.Width, ArenaConfig.Height), -20);

            float hw = ArenaConfig.Width * 0.5f;
            float hh = ArenaConfig.Height * 0.5f;
            CreateRect("BorderTop", square, borderColor, new Vector2(0, hh), new Vector2(ArenaConfig.Width + borderThickness, borderThickness), -19);
            CreateRect("BorderBottom", square, borderColor, new Vector2(0, -hh), new Vector2(ArenaConfig.Width + borderThickness, borderThickness), -19);
            CreateRect("BorderLeft", square, borderColor, new Vector2(-hw, 0), new Vector2(borderThickness, ArenaConfig.Height + borderThickness), -19);
            CreateRect("BorderRight", square, borderColor, new Vector2(hw, 0), new Vector2(borderThickness, ArenaConfig.Height + borderThickness), -19);
        }

        void CreateRect(string objectName, Sprite sprite, Color color, Vector2 center, Vector2 size, int order)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = center;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
        }

        static Sprite CreateSquareSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
