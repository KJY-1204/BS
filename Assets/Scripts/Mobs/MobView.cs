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
        MobServer server;
        MobClient client;
        NetworkManager nm;

        readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();
        // Per pool slot: where the mob was last frame and whether it faces right, so art that looks one way can be flipped.
        readonly List<float> lastX = new List<float>();
        readonly List<bool> facingRight = new List<bool>();
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

                // The type byte holds the origin and the kind: the art tells them apart, and without art the color does and the size is the kind's body.
                byte type = source.TypeAt(i);
                SpriteAnimSet art = SpriteLibrary.Get(MobKinds.SpriteIdOf(type));
                bool device = MobKinds.IsDeviceType(type);
                if (art != null)
                {
                    SpriteAnimSet.Clip clip = device ? art.Find("idle") : art.FindAny("move", "idle");
                    sr.sprite = SpriteAnimSet.SpriteAt(clip, Time.time + i * 0.137f, true);
                    sr.color = Color.white;
                    float scale = device ? 1f : MobKinds.Get(MobKinds.KindOf(type)).RadiusMultiple;
                    sr.transform.localScale = new Vector3(scale, scale, 1f);
                    sr.transform.localRotation = Quaternion.identity;

                    float dx = p.x - lastX[i];
                    if (Mathf.Abs(dx) > 0.002f) facingRight[i] = dx > 0f;
                    sr.flipX = art.facesRight ? !facingRight[i] : facingRight[i];
                }
                else
                {
                    sr.sprite = square;
                    sr.flipX = false;
                    sr.color = MobKinds.ColorOf(type);
                    float size = MobConfig.Radius * 2f * MobKinds.Get(MobKinds.KindOf(type)).RadiusMultiple;
                    sr.transform.localScale = new Vector3(size, size, 1f);
                    // A device is drawn as a diamond so it is told apart from every mob at a glance.
                    sr.transform.localRotation = device ? Quaternion.Euler(0f, 0f, 45f) : Quaternion.identity;
                }
                lastX[i] = p.x;
            }

            for (int i = count; i < active; i++)
                pool[i].gameObject.SetActive(false);

            active = count;
        }

        /// <summary>The mobs being drawn right now (null when no room is running).</summary>
        public IMobSource Source => CurrentSource();

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
                lastX.Add(0f);
                facingRight.Add(true);
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
