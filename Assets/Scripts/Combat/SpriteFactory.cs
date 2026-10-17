using UnityEngine;

namespace BS
{
    /// <summary>Placeholder art: one white one-unit square that views tint and scale.</summary>
    public static class SpriteFactory
    {
        static Sprite square;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => square = null;

        public static Sprite Square
        {
            get
            {
                if (square == null)
                {
                    var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                    tex.SetPixel(0, 0, Color.white);
                    tex.Apply();
                    square = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                }
                return square;
            }
        }

        public static SpriteRenderer CreateRenderer(string name, Transform parent, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Square;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return sr;
        }
    }
}
