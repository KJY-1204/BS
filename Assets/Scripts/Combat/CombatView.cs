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
        // The latest health percentage a hit told for each mob (read for devices, whose bar stays up).
        readonly Dictionary<ushort, byte> deviceHealth = new Dictionary<ushort, byte>();
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
            /// <summary>The ring's art, played once over its life (one unit across); null draws the plain ring.</summary>
            public SpriteAnimSet.Clip Art;
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
            /// <summary>The beam's art (its left end at the origin, one unit long); null draws the plain bar.</summary>
            public SpriteAnimSet.Clip Art;
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
        /// <summary>The animation drawn as a slot's aura, or null for the plain ring.</summary>
        readonly SpriteAnimSet.Clip[] auraArt = new SpriteAnimSet.Clip[5];
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
            SpriteAnimSet blastArt = SpriteLibrary.Get("fx_blast");
            foreach (ExplosionEffect blast in combat.DueExplosions)
            {
                if (blastArt != null) AddRing(blast.Position, blast.Radius, Color.white, 0.35f, 0.2f, blastArt.clips[0]);
                else AddRing(blast.Position, blast.Radius, new Color(1f, 0.85f, 0.2f), 0.35f, 0.2f);
            }
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

            FindBySlot(effect.Slot)?.GetComponent<PlayerAvatar>()?.PlaySkill(def.Slot);

            switch (def.Effect)
            {
                case SkillEffectKind.AreaDamage:
                {
                    float radius = SkillEffectSize.Radius(def, effect.Rank, effect.RangeScale);
                    SpriteAnimSet.Clip art = AreaArt(def);
                    if (art != null) AddRing(effect.Position, radius, Color.white, RingSeconds, def.Slot == SkillCatalog.UltimateSlot ? 0.5f : 0.6f, art);
                    else AddRing(effect.Position, radius, new Color(1f, 0.6f, 0.2f), RingSeconds);
                    break;
                }
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
                    AddBeam(effect, SkillEffectSize.Length(def, effect.Rank, effect.RangeScale), SkillEffectSize.Width(def, effect.Rank, effect.RangeScale), BeamArt(def));
                    break;
                case SkillEffectKind.AttackSpeed:
                    SetAura(effect.Slot, Time.time + SkillEffectSize.Seconds(def, effect.Rank, effect.DurationScale), new Color(1f, 0.9f, 0.3f), StardustArt(def));
                    break;
                case SkillEffectKind.Barrier:
                    SetAura(effect.Slot, Time.time + SkillEffectSize.Seconds(def, effect.Rank, effect.DurationScale), new Color(0.7f, 0.9f, 1f), BarrierArt(def));
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

        void AddRing(Vector2 center, float radius, Color color, float seconds, float startScale = 0.3f, SpriteAnimSet.Clip art = null)
        {
            rings[nextRing] = new Ring { Center = center, Radius = radius, Start = Time.time, Color = color, Seconds = seconds, StartScale = startScale, Art = art };
            nextRing = (nextRing + 1) % MaxRings;
        }

        void SetAura(int slot, float endsAt, Color color, SpriteAnimSet.Clip art)
        {
            if (slot < 0 || slot >= auraEndsAt.Length) return;
            auraEndsAt[slot] = endsAt;
            auraColors[slot] = color;
            auraArt[slot] = art;
        }

        void AddBeam(SkillEffect effect, float length, float width, SpriteAnimSet.Clip art = null)
        {
            float radians = AimMath.DequantizeAngle(effect.Angle);
            Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            beams[nextBeam] = new Beam
            {
                Origin = effect.Position, Degrees = radians * Mathf.Rad2Deg,
                Length = ClipToArena(effect.Position, direction, length), Width = width, Start = Time.time,
                Seconds = BeamSeconds, Color = new Color(1f, 1f, 0.8f), Art = art,
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
                Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                sr.enabled = true;
                sr.transform.rotation = Quaternion.Euler(0f, 0f, beams[i].Degrees);

                SpriteAnimSet.Clip art = beams[i].Art;
                if (art != null)
                {
                    // Art: from the caster outward, stretched to the beam's length; its height is a little more than the beam is wide.
                    Sprite frame = SpriteAnimSet.SpriteAt(art, age, true);
                    float aspect = frame.rect.height / frame.rect.width;
                    sr.sprite = frame;
                    sr.transform.position = new Vector3(beams[i].Origin.x, beams[i].Origin.y, 0f);
                    sr.transform.localScale = new Vector3(beams[i].Length, beams[i].Width * 1.7f * Mathf.Lerp(1f, 0.7f, t) / aspect, 1f);
                    sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01((1f - t) / 0.35f));
                    continue;
                }

                Vector2 middle = beams[i].Origin + direction * (beams[i].Length * 0.5f);
                sr.sprite = SpriteFactory.Square;
                sr.transform.position = new Vector3(middle.x, middle.y, 0f);
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
                sr.sprite = rings[i].Art != null ? SpriteAnimSet.SpriteAt(rings[i].Art, t * rings[i].Art.TotalSeconds, false) : SpriteFactory.Ring;
                sr.transform.position = new Vector3(rings[i].Center.x, rings[i].Center.y, 0f);
                sr.transform.localScale = Vector3.one * (rings[i].Radius * 2f * Mathf.Lerp(rings[i].StartScale, 1f, t));
                Color c = rings[i].Color;
                // Art keeps its own colors and shows fully for most of its life before it fades.
                c.a = rings[i].Art != null ? Mathf.Clamp01((1f - t) / 0.4f) : 1f - t;
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
                SpriteAnimSet.Clip art = auraArt[slot];
                if (art != null)
                {
                    // The art's ring fills about three quarters of its picture.
                    sr.sprite = SpriteAnimSet.SpriteAt(art, Time.time, true);
                    sr.color = Color.white;
                    sr.transform.localScale = Vector3.one * (pulse * 1.4f);
                }
                else
                {
                    sr.sprite = SpriteFactory.Ring;
                    sr.color = auraColors[slot];
                    sr.transform.localScale = Vector3.one * pulse;
                }
                sr.transform.position = owner.transform.position;
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
                SpriteAnimSet art = SpriteLibrary.Get(ProjectileArtKey(slot, character));
                if (art != null && art.clips.Length > 0)
                {
                    // Art: sized by its own pixels, pointing along the path (or turning, for a star).
                    sr.sprite = art.clips[0].frames[0];
                    sr.color = Color.white;
                    sr.transform.localScale = Vector3.one;
                    Vector2 dir = replay.DirectionAt(i);
                    float angle = art.spins ? Time.time * 540f + i * 47f : Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    sr.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                    continue;
                }

                sr.sprite = SpriteFactory.Square;
                sr.transform.localRotation = Quaternion.identity;
                if (slot == RoleTable.BossRole) sr.color = BossCatalog.Current.Color;
                else if (slot == CombatServer.MobShooterSlot) sr.color = MobKinds.Get(MobKinds.Ranged).Color;
                else sr.color = character != null ? character.Color : SlotColors[Mathf.Min(slot, SlotColors.Length - 1)];
                float size = Mathf.Max(0.15f, replay.ShapeAt(i).Radius * 2f);
                sr.transform.localScale = new Vector3(size, size, 1f);
            }

            for (int i = count; i < projectilesShown; i++) projectileSprites[i].enabled = false;
            projectilesShown = count;
        }

        /// <summary>The art of an area skill: Reimu's seal bursts where it lands, her ultimate spreads a ring. Null keeps the plain ring.</summary>
        static SpriteAnimSet.Clip AreaArt(SkillDef def)
        {
            if (def.Character != CharacterIds.Reimu) return null;
            SpriteAnimSet set = SpriteLibrary.Get(def.Slot == SkillCatalog.UltimateSlot ? "fx_reimu_r_ring" : "fx_reimu_q_burst");
            return set != null ? set.Find("fx") : null;
        }

        /// <summary>Marisa's sparks, both the Master and the Final one, are the same beam picture at their own width.</summary>
        static SpriteAnimSet.Clip BeamArt(SkillDef def)
        {
            if (def.Character != CharacterIds.Marisa) return null;
            SpriteAnimSet set = SpriteLibrary.Get("fx_marisa_beam");
            return set != null ? set.Find("fx") : null;
        }

        static SpriteAnimSet.Clip StardustArt(SkillDef def)
        {
            if (def.Character != CharacterIds.Marisa) return null;
            SpriteAnimSet set = SpriteLibrary.Get("fx_marisa_stardust");
            return set != null ? set.Find("fx") : null;
        }

        static SpriteAnimSet.Clip BarrierArt(SkillDef def)
        {
            if (def.Character != CharacterIds.Reimu) return null;
            SpriteAnimSet set = SpriteLibrary.Get("fx_reimu_barrier");
            return set != null ? set.Find("fx") : null;
        }

        /// <summary>The sprite set key of a shot: its character's, its boss kind's, or none for a mob's (which keeps its placeholder).</summary>
        static string ProjectileArtKey(byte slot, CharacterDef character)
        {
            if (slot == RoleTable.BossRole) return "shot_" + BossCatalog.Current.SpriteId;
            if (slot == CombatServer.MobShooterSlot) return "shot_" + MobKinds.Get(MobKinds.Ranged).SpriteId;
            return character != null ? "shot_" + character.SpriteId : null;
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

            // A device keeps its bar for as long as it stands, so its health is kept apart from the short-lived mob bars.
            if (effect.Killed) { bars.Remove(effect.MobId); deviceHealth.Remove(effect.MobId); }
            else
            {
                bars[effect.MobId] = new KeyValuePair<float, byte>(Time.time + CombatConfig.MobHealthBarSeconds, effect.HealthPercent);
                deviceHealth[effect.MobId] = effect.HealthPercent;
            }
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

            // A standing device always shows its bar (full until the first hit on it tells the client its health).
            for (int i = 0; mobs != null && i < mobs.Count; i++)
            {
                if (!MobKinds.IsDeviceType(mobs.TypeAt(i))) continue;

                EnsureBar(shown);
                Vector2 p = mobs.PositionAt(i);
                float width = BarWidth * Mathf.Clamp01(DeviceBar.Percent(deviceHealth, mobs.IdAt(i)) / 100f);
                float y = p.y + DeviceBar.Offset;

                barBack[shown].enabled = true;
                barBack[shown].transform.position = new Vector3(p.x, y, 0f);
                barFill[shown].enabled = true;
                barFill[shown].transform.position = new Vector3(p.x - BarWidth * 0.5f + width * 0.5f, y, 0f);
                barFill[shown].transform.localScale = new Vector3(Mathf.Max(width, 0.0001f), BarHeight, 1f);
                shown++;
            }

            if (mobs != null && bars.Count > 0)
            {
                expiredBars.Clear();
                foreach (KeyValuePair<ushort, KeyValuePair<float, byte>> pair in bars)
                    if (Time.time > pair.Value.Key) expiredBars.Add(pair.Key);
                foreach (ushort id in expiredBars) bars.Remove(id);

                for (int i = 0; i < mobs.Count && bars.Count > 0; i++)
                {
                    if (MobKinds.IsDeviceType(mobs.TypeAt(i))) continue; // drawn above
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
                deviceHealth.Clear();
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
