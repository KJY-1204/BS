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

        // A device is told apart by its id: clients only see its position, so its state is read from how long it has stood.
        sealed class DeviceTrack
        {
            public float Born;
            public Vector2 Position;
            public bool Seen;
        }

        struct DeathPlay
        {
            public SpriteRenderer Renderer;
            public float Start;
        }

        readonly Dictionary<ushort, DeviceTrack> devices = new Dictionary<ushort, DeviceTrack>();
        readonly List<ushort> goneScratch = new List<ushort>();
        readonly List<DeathPlay> deathPlays = new List<DeathPlay>();
        const float DeathFadeSeconds = 0.25f;
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
            if (source == null) devices.Clear();

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
                    SpriteAnimSet.Clip clip;
                    if (device)
                    {
                        if (!devices.TryGetValue(source.IdAt(i), out DeviceTrack track)) devices[source.IdAt(i)] = track = new DeviceTrack { Born = Time.time };
                        track.Position = p;
                        track.Seen = true;
                        clip = DeviceClip(art, Time.time - track.Born);
                    }
                    else clip = art.FindAny("move", "idle");
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

            PlayDeviceDeaths();

            for (int i = count; i < active; i++)
                pool[i].gameObject.SetActive(false);

            active = count;
        }

        /// <summary>
        /// The clip a standing device shows: it gets ready, makes mobs and shows them every production interval (counted from when it was
        /// placed), and stands idle between.
        /// </summary>
        static SpriteAnimSet.Clip DeviceClip(SpriteAnimSet art, float age)
        {
            float interval = DeviceConfig.Default.ProductionIntervalSeconds;
            float since = age % interval;
            float untilNext = interval - since;

            string name = "idle";
            if (untilNext < 0.3f) name = "summoning";
            else if (untilNext < 0.9f) name = "ready";
            else if (age >= interval && since < 0.6f) name = "summoned";
            return art.Find(name) ?? art.Find("idle");
        }

        /// <summary>A device that is no longer there (destroyed or expired) breaks apart where it stood.</summary>
        void PlayDeviceDeaths()
        {
            goneScratch.Clear();
            foreach (KeyValuePair<ushort, DeviceTrack> pair in devices)
            {
                if (pair.Value.Seen) pair.Value.Seen = false;
                else goneScratch.Add(pair.Key);
            }

            SpriteAnimSet art = SpriteLibrary.Get(MobKinds.Get(MobKinds.Device).SpriteId);
            for (int i = 0; i < goneScratch.Count; i++)
            {
                DeviceTrack track = devices[goneScratch[i]];
                devices.Remove(goneScratch[i]);
                if (art == null || art.Find("destroy") == null) continue;

                SpriteRenderer sr = null;
                for (int k = 0; k < deathPlays.Count; k++)
                    if (!deathPlays[k].Renderer.gameObject.activeSelf) { sr = deathPlays[k].Renderer; deathPlays.RemoveAt(k); break; }
                if (sr == null)
                {
                    var go = new GameObject("DeviceDeath");
                    go.transform.SetParent(transform, false);
                    sr = go.AddComponent<SpriteRenderer>();
                    sr.sortingOrder = 6;
                }
                sr.gameObject.SetActive(true);
                sr.transform.localPosition = new Vector3(track.Position.x, track.Position.y, 0f);
                deathPlays.Add(new DeathPlay { Renderer = sr, Start = Time.time });
            }

            SpriteAnimSet.Clip destroy = art != null ? art.Find("destroy") : null;
            for (int i = 0; i < deathPlays.Count; i++)
            {
                float age = Time.time - deathPlays[i].Start;
                SpriteRenderer sr = deathPlays[i].Renderer;
                float total = destroy != null ? destroy.TotalSeconds : 0f;
                if (destroy == null || age > total + DeathFadeSeconds)
                {
                    sr.gameObject.SetActive(false);
                    continue;
                }
                sr.sprite = SpriteAnimSet.SpriteAt(destroy, age, false);
                sr.color = new Color(1f, 1f, 1f, age <= total ? 1f : 1f - (age - total) / DeathFadeSeconds);
            }
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
