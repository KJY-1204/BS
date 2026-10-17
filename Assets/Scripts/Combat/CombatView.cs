using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace BS
{
    /// <summary>
    /// Draws shots and their effects as pooled placeholder shapes: projectiles, hit flashes, the small health
    /// bar over a wounded mob, and the owner's muzzle flash. It only reads; it never changes the simulation.
    /// </summary>
    public class CombatView : MonoBehaviour
    {
        static readonly Color[] SlotColors =
        {
            new Color(0.95f, 0.4f, 0.4f),
            new Color(0.55f, 0.8f, 1f),
            new Color(0.6f, 1f, 0.65f),
            new Color(1f, 0.95f, 0.5f),
            new Color(0.85f, 0.65f, 1f),
        };

        const int MaxFlashes = 64;
        const float FlashSeconds = 0.18f;
        const float BarWidth = 0.9f;
        const float BarHeight = 0.1f;

        struct Flash
        {
            public Vector2 Position;
            public float Start;
            public bool Killed;
        }

        MobView mobView;
        NetworkManager nm;

        readonly List<SpriteRenderer> projectileSprites = new List<SpriteRenderer>();
        int projectilesShown;

        readonly SpriteRenderer[] flashSprites = new SpriteRenderer[MaxFlashes];
        readonly Flash[] flashes = new Flash[MaxFlashes];
        int nextFlash;

        readonly Dictionary<ushort, KeyValuePair<float, byte>> bars = new Dictionary<ushort, KeyValuePair<float, byte>>();
        readonly List<ushort> expiredBars = new List<ushort>();
        readonly List<SpriteRenderer> barBack = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> barFill = new List<SpriteRenderer>();
        int barsShown;

        readonly List<HitEffect> effects = new List<HitEffect>();
        SpriteRenderer muzzle;

        // Skill effects: expanding rings for Burst, one aura per survivor slot for Rapid Fire
        const int MaxRings = 16;
        /// <summary>How long a Burst ring lasts on screen (a field so a test can stretch it).</summary>
        public float RingSeconds = 0.4f;
        struct Ring
        {
            public Vector2 Center;
            public float Radius;
            public float Start;
        }
        readonly SpriteRenderer[] ringSprites = new SpriteRenderer[MaxRings];
        readonly Ring[] rings = new Ring[MaxRings];
        int nextRing;
        readonly SpriteRenderer[] auraSprites = new SpriteRenderer[5];
        readonly float[] auraEndsAt = new float[5];
        readonly List<SkillEffect> skillEffects = new List<SkillEffect>();

        /// <summary>Mob health bars currently on screen (for tests and logs).</summary>
        public int VisibleBars => barsShown;

        /// <summary>Burst rings and Rapid Fire auras currently drawn (for tests and logs).</summary>
        public int VisibleSkillEffects
        {
            get
            {
                int n = 0;
                foreach (SpriteRenderer r in ringSprites) if (r != null && r.enabled) n++;
                foreach (SpriteRenderer r in auraSprites) if (r != null && r.enabled) n++;
                return n;
            }
        }
        public int VisibleProjectiles => projectilesShown;

        void Awake()
        {
            mobView = GetComponent<MobView>();
            muzzle = SpriteFactory.CreateRenderer("MuzzleFlash", transform, new Color(1f, 0.95f, 0.6f), 22);
            muzzle.transform.localScale = new Vector3(0.45f, 0.45f, 1f);
            muzzle.enabled = false;

            for (int i = 0; i < MaxFlashes; i++)
            {
                flashSprites[i] = SpriteFactory.CreateRenderer("HitFlash", transform, Color.white, 21);
                flashSprites[i].enabled = false;
            }

            for (int i = 0; i < MaxRings; i++)
            {
                ringSprites[i] = SpriteFactory.CreateRingRenderer("SkillRing", transform, new Color(1f, 0.6f, 0.2f), 23);
                ringSprites[i].enabled = false;
            }

            for (int i = 0; i < auraSprites.Length; i++)
            {
                auraSprites[i] = SpriteFactory.CreateRingRenderer("RapidFireAura", transform, new Color(1f, 0.9f, 0.3f), 24);
                auraSprites[i].enabled = false;
            }
        }

        void Start()
        {
            nm = NetworkManager.Singleton;
        }

        void Update()
        {
            CombatClient combat = CombatClient.Instance;
            if (combat == null) return;

            DrawProjectiles(combat.Replay);

            effects.Clear();
            effects.AddRange(combat.DueEffects);
            combat.DueEffects.Clear();
            foreach (HitEffect effect in effects) OnHit(effect);

            skillEffects.Clear();
            skillEffects.AddRange(combat.DueSkillEffects);
            combat.DueSkillEffects.Clear();
            foreach (SkillEffect effect in skillEffects) OnSkill(effect);

            DrawFlashes();
            DrawMobBars();
            DrawMuzzleFlash();
            DrawRings();
            DrawAuras();
        }

        // ---- Skill effects ----

        void OnSkill(SkillEffect effect)
        {
            SkillDef def = SkillCatalog.Get(effect.SkillId);
            if (def == null || effect.Rank < 1 || effect.Rank > def.MaxRank) return;

            if (effect.SkillId == SkillIds.Burst)
            {
                rings[nextRing] = new Ring { Center = effect.Position, Radius = def.Radius[effect.Rank - 1], Start = Time.time };
                nextRing = (nextRing + 1) % MaxRings;
            }
            else if (effect.SkillId == SkillIds.RapidFire && effect.Slot < auraEndsAt.Length)
            {
                auraEndsAt[effect.Slot] = Time.time + def.Duration[effect.Rank - 1];
            }
        }

        void DrawRings()
        {
            for (int i = 0; i < MaxRings; i++)
            {
                SpriteRenderer sr = ringSprites[i];
                float age = Time.time - rings[i].Start;
                if (rings[i].Radius <= 0f || age > RingSeconds)
                {
                    if (sr.enabled) sr.enabled = false;
                    continue;
                }

                float t = age / RingSeconds;
                sr.enabled = true;
                sr.transform.position = new Vector3(rings[i].Center.x, rings[i].Center.y, 0f);
                sr.transform.localScale = Vector3.one * (rings[i].Radius * 2f * Mathf.Lerp(0.3f, 1f, t));
                Color c = new Color(1f, 0.6f, 0.2f);
                c.a = 1f - t;
                sr.color = c;
            }
        }

        void DrawAuras()
        {
            for (int slot = 0; slot < auraSprites.Length; slot++)
            {
                SpriteRenderer sr = auraSprites[slot];
                PlayerGrowth owner = FindBySlot(slot);

                bool active = Time.time < auraEndsAt[slot] && owner != null && owner.GetComponent<PlayerAvatar>().IsAlive;
                sr.enabled = active;
                if (!active) continue;

                float pulse = 1.5f + 0.15f * Mathf.Sin(Time.time * 10f);
                sr.transform.position = owner.transform.position;
                sr.transform.localScale = Vector3.one * pulse;
            }
        }

        static PlayerGrowth FindBySlot(int slot)
        {
            for (int i = 0; i < PlayerGrowth.All.Count; i++)
            {
                PlayerGrowth g = PlayerGrowth.All[i];
                PlayerAvatar a = g != null ? g.GetComponent<PlayerAvatar>() : null;
                if (a != null && a.Role == slot) return g;
            }
            return null;
        }

        // ---- Projectiles ----

        void DrawProjectiles(ProjectileReplay replay)
        {
            int count = replay.Count;
            while (projectileSprites.Count < count)
            {
                SpriteRenderer sr = SpriteFactory.CreateRenderer("Projectile", transform, Color.white, 15);
                sr.transform.localScale = new Vector3(0.3f, 0.3f, 1f);
                projectileSprites.Add(sr);
            }

            for (int i = 0; i < count; i++)
            {
                SpriteRenderer sr = projectileSprites[i];
                sr.enabled = true;
                Vector2 p = replay.PositionAt(i);
                sr.transform.position = new Vector3(p.x, p.y, 0f);
                sr.color = SlotColors[Mathf.Min(replay.SlotAt(i), SlotColors.Length - 1)];
            }

            for (int i = count; i < projectilesShown; i++) projectileSprites[i].enabled = false;
            projectilesShown = count;
        }

        // ---- Hits ----

        void OnHit(HitEffect effect)
        {
            flashes[nextFlash] = new Flash { Position = effect.Position, Start = Time.time, Killed = effect.Killed };
            nextFlash = (nextFlash + 1) % MaxFlashes;

            if (!effect.Killed)
                bars[effect.MobId] = new KeyValuePair<float, byte>(Time.time + CombatConfig.MobHealthBarSeconds, effect.HealthPercent);
            else
                bars.Remove(effect.MobId);
        }

        void DrawFlashes()
        {
            for (int i = 0; i < MaxFlashes; i++)
            {
                SpriteRenderer sr = flashSprites[i];
                float age = Time.time - flashes[i].Start;
                if (flashes[i].Start <= 0f || age > FlashSeconds)
                {
                    if (sr.enabled) sr.enabled = false;
                    continue;
                }

                float t = age / FlashSeconds;
                sr.enabled = true;
                sr.transform.position = new Vector3(flashes[i].Position.x, flashes[i].Position.y, 0f);
                sr.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, flashes[i].Killed ? 1.1f : 0.7f, t);
                Color c = flashes[i].Killed ? new Color(1f, 0.6f, 0.2f) : Color.white;
                c.a = 1f - t;
                sr.color = c;
            }
        }

        // ---- Mob health bars ----

        void DrawMobBars()
        {
            IMobSource mobs = mobView != null ? mobView.Source : null;
            int shown = 0;

            if (mobs != null && bars.Count > 0)
            {
                expiredBars.Clear();
                foreach (KeyValuePair<ushort, KeyValuePair<float, byte>> pair in bars)
                    if (Time.time > pair.Value.Key) expiredBars.Add(pair.Key);
                foreach (ushort id in expiredBars) bars.Remove(id);

                for (int i = 0; i < mobs.Count && bars.Count > 0; i++)
                {
                    if (!bars.TryGetValue(mobs.IdAt(i), out KeyValuePair<float, byte> bar)) continue;

                    EnsureBar(shown);
                    Vector2 p = mobs.PositionAt(i);
                    float fraction = Mathf.Clamp01(bar.Value / 100f);
                    float width = BarWidth * fraction;

                    barBack[shown].enabled = true;
                    barBack[shown].transform.position = new Vector3(p.x, p.y + 0.62f, 0f);
                    barFill[shown].enabled = true;
                    barFill[shown].transform.position = new Vector3(p.x - BarWidth * 0.5f + width * 0.5f, p.y + 0.62f, 0f);
                    barFill[shown].transform.localScale = new Vector3(Mathf.Max(width, 0.0001f), BarHeight, 1f);
                    shown++;
                }
            }
            else if (mobs == null)
            {
                bars.Clear();
            }

            for (int i = shown; i < barsShown; i++)
            {
                barBack[i].enabled = false;
                barFill[i].enabled = false;
            }
            barsShown = shown;
        }

        void EnsureBar(int index)
        {
            while (barBack.Count <= index)
            {
                SpriteRenderer back = SpriteFactory.CreateRenderer("MobBarBack", transform, new Color(0f, 0f, 0f, 0.7f), 40);
                back.transform.localScale = new Vector3(BarWidth + 0.05f, BarHeight + 0.05f, 1f);
                SpriteRenderer fill = SpriteFactory.CreateRenderer("MobBarFill", transform, new Color(0.95f, 0.3f, 0.25f), 41);
                barBack.Add(back);
                barFill.Add(fill);
            }
        }

        // ---- Muzzle flash (owner only, cosmetic) ----

        void DrawMuzzleFlash()
        {
            muzzle.enabled = false;
            if (nm == null || !nm.IsListening || nm.LocalClient == null || nm.LocalClient.PlayerObject == null) return;

            PlayerCombat combat = nm.LocalClient.PlayerObject.GetComponent<PlayerCombat>();
            if (combat == null || combat.IsDead || Time.time > combat.MuzzleFlashUntil) return;

            Vector2 direction = AimMath.Direction(combat.AimAngle);
            Vector3 origin = nm.LocalClient.PlayerObject.transform.position;
            muzzle.transform.position = origin + (Vector3)(direction * 0.75f);
            muzzle.enabled = true;
        }
    }
}
