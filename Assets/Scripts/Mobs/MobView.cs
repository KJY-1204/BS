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

        // A device is told apart by its id; its state follows the beats the server sends (getting ready, making mobs).
        sealed class DeviceTrack
        {
            public float ReadyAt = float.NegativeInfinity;
            public float ProducedAt = float.NegativeInfinity;
            public Vector2 Position;
            public bool Seen;
            /// <summary>It has been drawn at least once; a beat that comes before its device is shown must not make a death.</summary>
            public bool Shown;
        }

        const float ReadySeconds = 0.6f;
        const float SummonedSeconds = 0.6f;
        readonly List<MobEvent> beatScratch = new List<MobEvent>();

        struct DeathPlay
        {
            public SpriteRenderer Renderer;
            public float Start;
        }

        readonly Dictionary<ushort, DeviceTrack> devices = new Dictionary<ushort, DeviceTrack>();
        readonly List<ushort> goneScratch = new List<ushort>();
        readonly List<DeathPlay> deathPlays = new List<DeathPlay>();
        readonly List<DeathPlay> puffs = new List<DeathPlay>();
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
            ApplyDeviceBeats(source);

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
                        if (!devices.TryGetValue(source.IdAt(i), out DeviceTrack track)) devices[source.IdAt(i)] = track = new DeviceTrack();
                        track.Position = p;
                        track.Seen = track.Shown = true;
                        clip = DeviceClip(art, track);
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
            DrawPuffs();

            for (int i = count; i < active; i++)
                pool[i].gameObject.SetActive(false);

            active = count;
        }

        void ApplyDeviceBeats(IMobSource source)
        {
            beatScratch.Clear();
            (source as IDeviceBeatSource)?.DrainDeviceBeats(beatScratch);
            for (int i = 0; i < beatScratch.Count; i++)
            {
                MobEvent beat = beatScratch[i];
                if (!devices.TryGetValue(beat.Id, out DeviceTrack track)) devices[beat.Id] = track = new DeviceTrack { Position = beat.Position, Seen = true };
                if (beat.Device == MobEvent.DeviceReady) track.ReadyAt = Time.time;
                else { track.ProducedAt = Time.time; AddPuff(beat.Position); }
            }
        }

        /// <summary>
        /// The clip a standing device shows: ready when the server says it is about to make mobs, summoning until they appear,
        /// the mobs shown on it for a moment after, and idle in between.
        /// </summary>
        static SpriteAnimSet.Clip DeviceClip(SpriteAnimSet art, DeviceTrack track)
        {
            float now = Time.time;
            string name = "idle";
            if (now - track.ProducedAt < SummonedSeconds) name = "summoned";
            else if (track.ReadyAt > track.ProducedAt)
                name = now - track.ReadyAt < ReadySeconds ? "ready" : "summoning";
            return art.Find(name) ?? art.Find("idle");
        }

        /// <summary>The smoke ring a device puffs out when it makes mobs.</summary>
        void AddPuff(Vector2 position)
        {
            if (SpriteLibrary.Get("fx_puff") == null) return;
            SpriteRenderer sr = null;
            for (int k = 0; k < puffs.Count; k++)
                if (!puffs[k].Renderer.gameObject.activeSelf) { sr = puffs[k].Renderer; puffs.RemoveAt(k); break; }
            if (sr == null)
            {
                var go = new GameObject("DevicePuff");
                go.transform.SetParent(transform, false);
                sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 4;
            }
            sr.gameObject.SetActive(true);
            sr.transform.localPosition = new Vector3(position.x, position.y - 0.3f, 0f);
            sr.transform.localScale = Vector3.one * 2.6f;
            puffs.Add(new DeathPlay { Renderer = sr, Start = Time.time });
        }

        void DrawPuffs()
        {
            SpriteAnimSet.Clip clip = SpriteLibrary.Get("fx_puff")?.Find("fx");
            for (int i = 0; i < puffs.Count; i++)
            {
                float age = Time.time - puffs[i].Start;
                SpriteRenderer sr = puffs[i].Renderer;
                if (clip == null || age > clip.TotalSeconds) { sr.gameObject.SetActive(false); continue; }
                sr.sprite = SpriteAnimSet.SpriteAt(clip, age, false);
            }
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
                if (!track.Shown) continue;
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
