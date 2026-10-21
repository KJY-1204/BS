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

        // Skill effects: expanding rings (burst, time stop, miracle, wind veil), a beam, and one aura per survivor
        // slot for the skills that last (stardust, barrier)
        const int MaxRings = 16;
        const int MaxBeams = 8;
        /// <summary>How long a Burst ring lasts on screen (a field so a test can stretch it).</summary>
        public float RingSeconds = 0.4f;
        /// <summary>How long the beam, and the rings of the other skills, last on screen. A field so a test can stretch them.</summary>
        public float BeamSeconds = 0.35f;
        public float StopRingSeconds = 0.9f;
        public float HealRingSeconds = 0.6f;
        public float PushRingSeconds = 0.5f;
        struct Ring
        {
            public Vector2 Center;
            public float Radius;
            public float Start;
            public Color Color;
            public float Seconds;
            /// <summary>The fraction of the full size the ring starts at.</summary>
            public float StartScale;
        }
        struct Beam
        {
            public Vector2 Origin;
            public float Degrees;
            public float Length;
            public float Width;
            public float Start;
            /// <summary>How long it is shown; zero means the usual <see cref="CombatView.BeamSeconds"/>.</summary>
            public float Seconds;
            /// <summary>Its color; an unset (transparent) color means the survivors' pale yellow.</summary>
            public Color Color;
            /// <summary>A steady line (a warning) keeps its width and strength until it ends instead of fading.</summary>
            public bool Steady;
        }
        readonly SpriteRenderer[] beamSprites = new SpriteRenderer[MaxBeams];
        readonly Beam[] beams = new Beam[MaxBeams];
        int nextBeam;
        readonly Color[] auraColors = new Color[5];
        readonly SpriteRenderer[] ringSprites = new SpriteRenderer[MaxRings];
        readonly Ring[] rings = new Ring[MaxRings];
        int nextRing;
        readonly SpriteRenderer[] auraSprites = new SpriteRenderer[5];
        readonly float[] auraEndsAt = new float[5];
        readonly List<SkillEffect> skillEffects = new List<SkillEffect>();

        /// <summary>Mob health bars currently on screen (for tests and logs).</summary>
        public int VisibleBars => barsShown;

        /// <summary>Skill rings, beams and auras currently drawn (for tests and logs).</summary>
        public int VisibleSkillEffects
        {
            get
            {
                int n = 0;
                foreach (SpriteRenderer r in beamSprites) if (r != null && r.enabled) n++;
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
                auraSprites[i] = SpriteFactory.CreateRingRenderer("SkillAura", transform, new Color(1f, 0.9f, 0.3f), 24);
                auraSprites[i].enabled = false;
            }

            for (int i = 0; i < MaxBeams; i++)
            {
                beamSprites[i] = SpriteFactory.CreateRenderer("Beam", transform, new Color(1f, 1f, 0.8f), 20);
                beamSprites[i].enabled = false;
            }
        }

        void Start()
        {
            nm = NetworkManager.Singleton;
            RoleTable.PhaseChanged += OnPhaseChanged;
        }

        void OnDestroy()
        {
            RoleTable.PhaseChanged -= OnPhaseChanged;
        }

        /// <summary>A match ended: no ring, beam or aura of it stays on screen for the next one.</summary>
        void OnPhaseChanged(RoomPhase phase)
        {
            if (phase != RoomPhase.Waiting) return;
            for (int i = 0; i < MaxRings; i++) rings[i] = default;
            for (int i = 0; i < MaxBeams; i++) beams[i] = default;
            for (int i = 0; i < auraEndsAt.Length; i++) auraEndsAt[i] = 0f;
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

            // A mob's blast is a short ring of its radius (a placeholder shape).
            foreach (ExplosionEffect blast in combat.DueExplosions) AddRing(blast.Position, blast.Radius, new Color(1f, 0.85f, 0.2f), 0.35f, 0.2f);
            combat.DueExplosions.Clear();

            DrawFlashes();
            DrawMobBars();
            DrawMuzzleFlash();
            DrawRings();
            DrawBeams();
            DrawAuras();
        }

        // ---- Skill effects ----

        void OnSkill(SkillEffect effect)
        {
            // The boss announces a skill before it takes effect: a ring shows where and how big it will be. These skill numbers are
            // above every survivor skill, so they never reach the survivor skill table.
            if (BossSkills.IsWarningId(effect.SkillId))
            {
                AddBossWarning(effect);
                return;
            }

            SkillDef def = SkillCatalog.Get(effect.SkillId);
            if (def == null || effect.Rank < 1 || effect.Rank > def.MaxRank) return;

            switch (def.Effect)
            {
                case SkillEffectKind.AreaDamage:
                    AddRing(effect.Position, SkillEffectSize.Radius(def, effect.Rank, effect.RangeScale), new Color(1f, 0.6f, 0.2f), RingSeconds);
                    break;
                case SkillEffectKind.TimeStop:
                    // The whole arena: a pale blue ring that sweeps out far past the screen edge.
                    AddRing(effect.Position, Mathf.Sqrt(ArenaConfig.Width * ArenaConfig.Width + ArenaConfig.Height * ArenaConfig.Height), new Color(0.6f, 0.8f, 1f), StopRingSeconds, 0.04f);
                    break;
                case SkillEffectKind.Heal:
                    AddRing(effect.Position, SkillEffectSize.Radius(def, effect.Rank, effect.RangeScale), new Color(0.4f, 1f, 0.5f), HealRingSeconds);
                    break;
                case SkillEffectKind.Push:
                    AddRing(effect.Position, SkillEffectSize.Radius(def, effect.Rank, effect.RangeScale), new Color(1f, 1f, 1f), PushRingSeconds);
                    break;
                case SkillEffectKind.Beam:
                    AddBeam(effect, SkillEffectSize.Length(def, effect.Rank, effect.RangeScale), SkillEffectSize.Width(def, effect.Rank, effect.RangeScale));
                    break;
                case SkillEffectKind.AttackSpeed:
                    SetAura(effect.Slot, Time.time + SkillEffectSize.Seconds(def, effect.Rank, effect.DurationScale), new Color(1f, 0.9f, 0.3f));
                    break;
                case SkillEffectKind.Barrier:
                    SetAura(effect.Slot, Time.time + SkillEffectSize.Seconds(def, effect.Rank, effect.DurationScale), new Color(0.7f, 0.9f, 1f));
                    break;
            }
        }

        /// <summary>
        /// The warning of a boss skill, drawn from the skill event and the kind being played: a ring for the shot ring, the shockwave and
        /// the roar, and for the beam a thin steady line along the aim with a flash along it when the warning ends.
        /// </summary>
        void AddBossWarning(SkillEffect effect)
        {
            BossSkillEffect kind = BossSkills.EffectOfWarning(effect.SkillId);
            BossSkillDef def = BossCatalog.Current.SkillWithEffect(kind) ?? BossCatalog.Default.SkillWithEffect(kind);
            if (def == null) return;

            Color color = BossSkills.ColorOf(kind);
            switch (kind)
            {
                case BossSkillEffect.Shockwave:
                    AddRing(effect.Position, def.Radius, color, def.WarningSeconds, 0.1f);
                    break;
                case BossSkillEffect.ShotRing:
                    AddRing(effect.Position, BossSkills.BarrageWarningRadius, color, def.WarningSeconds, 0.1f);
                    break;
                case BossSkillEffect.Roar:
                    AddRing(effect.Position, BossSkillSim.RoarDistance, color, def.WarningSeconds, 0.1f);
                    break;
                case BossSkillEffect.Beam:
                    AddBossBeam(effect, def, color);
                    break;
            }
        }

        void AddBossBeam(SkillEffect effect, BossSkillDef def, Color color)
        {
            float radians = AimMath.DequantizeAngle(effect.Angle);
            Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            float length = ClipToArena(effect.Position, direction, def.Length);
            float degrees = radians * Mathf.Rad2Deg;

            Color faint = color;
            faint.a = 0.55f;
            beams[nextBeam] = new Beam
            {
                Origin = effect.Position, Degrees = degrees, Length = length, Width = 0.12f, Start = Time.time,
                Seconds = def.WarningSeconds, Color = faint, Steady = true,
            };
            nextBeam = (nextBeam + 1) % MaxBeams;

            // The flash starts when the warning ends, which is when the server lets the beam take effect.
            beams[nextBeam] = new Beam
            {
                Origin = effect.Position, Degrees = degrees, Length = length, Width = def.HalfWidth * 2f,
                Start = Time.time + def.WarningSeconds, Seconds = BossBeamFlashSeconds, Color = color,
            };
            nextBeam = (nextBeam + 1) % MaxBeams;
        }

        const float BossBeamFlashSeconds = 0.25f;

        void AddRing(Vector2 center, float radius, Color color, float seconds, float startScale = 0.3f)
        {
            rings[nextRing] = new Ring { Center = center, Radius = radius, Start = Time.time, Color = color, Seconds = seconds, StartScale = startScale };
            nextRing = (nextRing + 1) % MaxRings;
        }

        void SetAura(int slot, float endsAt, Color color)
        {
            if (slot < 0 || slot >= auraEndsAt.Length) return;
            auraEndsAt[slot] = endsAt;
            auraColors[slot] = color;
        }

        void AddBeam(SkillEffect effect, float length, float width)
        {
            float radians = AimMath.DequantizeAngle(effect.Angle);
            Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            beams[nextBeam] = new Beam
            {
                Origin = effect.Position, Degrees = radians * Mathf.Rad2Deg,
                Length = ClipToArena(effect.Position, direction, length), Width = width, Start = Time.time,
                Seconds = BeamSeconds, Color = new Color(1f, 1f, 0.8f),
            };
            nextBeam = (nextBeam + 1) % MaxBeams;
        }

        /// <summary>How far a ray from a point stays inside the arena, at most <paramref name="length"/>.</summary>
        static float ClipToArena(Vector2 origin, Vector2 direction, float length)
        {
            float hx = ArenaConfig.Width * 0.5f;
            float hy = ArenaConfig.Height * 0.5f;
            float limit = length;
            if (direction.x > 1e-6f) limit = Mathf.Min(limit, (hx - origin.x) / direction.x);
            else if (direction.x < -1e-6f) limit = Mathf.Min(limit, (-hx - origin.x) / direction.x);
            if (direction.y > 1e-6f) limit = Mathf.Min(limit, (hy - origin.y) / direction.y);
            else if (direction.y < -1e-6f) limit = Mathf.Min(limit, (-hy - origin.y) / direction.y);
            return Mathf.Max(0f, limit);
        }

        void DrawBeams()
        {
            for (int i = 0; i < MaxBeams; i++)
            {
                SpriteRenderer sr = beamSprites[i];
                float age = Time.time - beams[i].Start;
                float seconds = beams[i].Seconds > 0f ? beams[i].Seconds : BeamSeconds;
                if (beams[i].Length <= 0f || age < 0f || age > seconds)
                {
                    if (sr.enabled) sr.enabled = false;
                    continue;
                }

                float t = beams[i].Steady ? 0f : age / seconds;
                float radians = beams[i].Degrees * Mathf.Deg2Rad;
                Vector2 middle = beams[i].Origin + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * (beams[i].Length * 0.5f);
                sr.enabled = true;
                sr.transform.position = new Vector3(middle.x, middle.y, 0f);
                sr.transform.rotation = Quaternion.Euler(0f, 0f, beams[i].Degrees);
                sr.transform.localScale = new Vector3(beams[i].Length, beams[i].Width * Mathf.Lerp(1f, 0.4f, t), 1f);
                Color c = beams[i].Color.a > 0f ? beams[i].Color : new Color(1f, 1f, 0.8f);
                c.a = beams[i].Steady ? c.a : c.a * (1f - t);
                sr.color = c;
            }
        }

        void DrawRings()
        {
            for (int i = 0; i < MaxRings; i++)
            {
                SpriteRenderer sr = ringSprites[i];
                float age = Time.time - rings[i].Start;
                if (rings[i].Radius <= 0f || age > rings[i].Seconds)
                {
                    if (sr.enabled) sr.enabled = false;
                    continue;
                }

                float t = age / rings[i].Seconds;
                sr.enabled = true;
                sr.transform.position = new Vector3(rings[i].Center.x, rings[i].Center.y, 0f);
                sr.transform.localScale = Vector3.one * (rings[i].Radius * 2f * Mathf.Lerp(rings[i].StartScale, 1f, t));
                Color c = rings[i].Color;
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
                sr.color = auraColors[slot];
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
                projectileSprites.Add(sr);
            }

            for (int i = 0; i < count; i++)
            {
                SpriteRenderer sr = projectileSprites[i];
                sr.enabled = true;
                Vector2 p = replay.PositionAt(i);
                sr.transform.position = new Vector3(p.x, p.y, 0f);

                // A shot looks like its shooter's character: that character's color, and as wide as it really is.
                byte slot = replay.SlotAt(i);
                RoleTable table = RoleTable.Instance;
                CharacterDef character = slot != RoleTable.BossRole && slot != CombatServer.MobShooterSlot && table != null ? CharacterCatalog.Get(table.CharacterOfRole(slot)) : null;
                if (slot == RoleTable.BossRole) sr.color = BossCatalog.Current.Color;
                else if (slot == CombatServer.MobShooterSlot) sr.color = MobKinds.Get(MobKinds.Ranged).Color;
                else sr.color = character != null ? character.Color : SlotColors[Mathf.Min(slot, SlotColors.Length - 1)];
                float size = Mathf.Max(0.15f, replay.ShapeAt(i).Radius * 2f);
                sr.transform.localScale = new Vector3(size, size, 1f);
            }

            for (int i = count; i < projectilesShown; i++) projectileSprites[i].enabled = false;
            projectilesShown = count;
        }

        // ---- Hits ----

        void OnHit(HitEffect effect)
        {
            flashes[nextFlash] = new Flash { Position = effect.Position, Start = Time.time, Killed = effect.Killed };
            nextFlash = (nextFlash + 1) % MaxFlashes;

            // A hit on a survivor shows its flash only: the survivor's own bar is drawn from its replicated health.
            if (effect.Survivor) return;

            // A hit on the boss shows its flash only: its bar is drawn from its replicated health over its character.
            if (effect.MobId == BossSim.MobId) return;

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
