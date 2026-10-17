using UnityEngine;

namespace BS
{
    /// <summary>Placeholder art: one white one-unit square that views tint and scale.</summary>
    public static class SpriteFactory
    {
        static Sprite square;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { square = null; ring = null; }

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

        static Sprite ring;

        /// <summary>A thin circle one unit across, for skill effects. Scale it to the radius wanted.</summary>
        public static Sprite Ring
        {
            get
            {
                if (ring == null)
                {
                    const int size = 128;
                    var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                    var pixels = new Color32[size * size];
                    float center = (size - 1) * 0.5f;
                    float outer = size * 0.5f - 1f;
                    float inner = outer - 4f;
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                            pixels[y * size + x] = d <= outer && d >= inner ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                        }
                    }
                    tex.SetPixels32(pixels);
                    tex.Apply();
                    ring = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                }
                return ring;
            }
        }

        public static SpriteRenderer CreateRingRenderer(string name, Transform parent, Color color, int sortingOrder)
        {
            SpriteRenderer sr = CreateRenderer(name, parent, color, sortingOrder);
            sr.sprite = Ring;
            return sr;
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
