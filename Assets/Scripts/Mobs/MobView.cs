using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace BS
{
    /// <summary>
    /// Draws mobs as pooled placeholder squares. The host reads the simulation directly, other players
    /// read the interpolator. The view never changes the simulation.
    /// </summary>
    public class MobView : MonoBehaviour
    {
        static readonly Color[] TypeColors =
        {
            new Color(0.95f, 0.55f, 0.15f),
            new Color(0.8f, 0.2f, 0.6f),
            new Color(0.2f, 0.75f, 0.8f),
        };

        MobServer server;
        MobClient client;
        NetworkManager nm;

        readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();
        Sprite square;
        int active;

        /// <summary>How many mob squares are currently on screen.</summary>
        public int VisibleCount => active;

        void Awake()
        {
            server = GetComponent<MobServer>();
            client = GetComponent<MobClient>();
            square = CreateSquareSprite();
        }

        void Start()
        {
            nm = NetworkManager.Singleton;
            Grow(MobConfig.DefaultTarget);
            for (int i = 0; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
        }

        void Update()
        {
            IMobSource source = CurrentSource();
            int count = source != null ? source.Count : 0;

            if (count > pool.Count) Grow(count);

            for (int i = 0; i < count; i++)
            {
                SpriteRenderer sr = pool[i];
                if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);

                Vector2 p = source.PositionAt(i);
                sr.transform.localPosition = new Vector3(p.x, p.y, 0f);
                sr.color = TypeColors[Mathf.Min(source.TypeAt(i), TypeColors.Length - 1)];
            }

            for (int i = count; i < active; i++)
                pool[i].gameObject.SetActive(false);

            active = count;
        }

        IMobSource CurrentSource()
        {
            if (nm == null || !nm.IsListening) return null;
            if (nm.IsServer) return server != null ? server.Sim : null;
            if (nm.IsClient) return client != null ? client.Interpolator : null;
            return null;
        }

        void Grow(int target)
        {
            while (pool.Count < target)
            {
                var go = new GameObject("Mob");
                go.transform.SetParent(transform, false);
                go.transform.localScale = new Vector3(MobConfig.Radius * 2f, MobConfig.Radius * 2f, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = square;
                sr.sortingOrder = 5;
                pool.Add(sr);
            }
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
