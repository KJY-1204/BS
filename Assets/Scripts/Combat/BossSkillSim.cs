using System;
using System.Collections.Generic;
using UnityEngine;

namespace BS
{
    /// <summary>
    /// The boss's two skills as data (Q the shot ring, E the shockwave). All values are starting assumptions, tuned by playtest.
    /// A skill is announced first and takes effect after <see cref="WarningSeconds"/>, so survivors have time to move.
    /// </summary>
    public static class BossSkills
    {
        /// <summary>The base skills, on Q and E.</summary>
        public const int Count = 2;
        public const int Barrage = 0;
        public const int Shockwave = 1;
        /// <summary>The slot of the third skill (the F key) that phase 2 unlocks, and how many slots there are with it.</summary>
        public const int PhaseSlot = 2;
        public const int SlotCount = 3;

        public static readonly string[] Names = { "탄막", "충격파" };
        public static readonly string[] Keys = { "Q", "E" };

        /// <summary>How long a skill is announced before it takes effect.</summary>
        public const float WarningSeconds = 0.8f;

        // Shot ring
        public const int BarrageShots = 12;
        public const int BarrageDamage = 10;
        public const float BarrageCooldownSeconds = 10f;
        /// <summary>The ring a client draws around the boss while the shot ring is announced.</summary>
        public const float BarrageWarningRadius = 2f;

        // Shockwave
        public const int ShockwaveDamage = 30;
        public const float ShockwaveRadius = 4f;
        public const float ShockwaveCooldownSeconds = 8f;

        /// <summary>
        /// The skill numbers the announcement carries in a skill event. They are above every survivor skill number, so a client can
        /// tell them apart before looking the number up in the survivor skill table.
        /// </summary>
        public const byte WarningIdBarrage = 200;
        public const byte WarningIdShockwave = 201;
        public const byte WarningIdBeam = 202;
        public const byte WarningIdRoar = 203;

        public static bool IsWarningId(byte skillId) => skillId >= WarningIdBarrage && skillId <= WarningIdRoar;

        /// <summary>The warning number of a standard-kind slot (the shot ring on the first, the shockwave on the second).</summary>
        public static byte WarningIdOf(int slot) => slot == Barrage ? WarningIdBarrage : WarningIdShockwave;

        /// <summary>The warning number of an effect: one number for each, in the order of <see cref="BossSkillEffect"/>.</summary>
        public static byte WarningIdOf(BossSkillEffect effect) => (byte)(WarningIdBarrage + (int)effect);

        /// <summary>The color a skill of this effect is shown in (its warning and its slot on the boss player's screen).</summary>
        public static Color ColorOf(BossSkillEffect effect)
        {
            switch (effect)
            {
                case BossSkillEffect.ShotRing: return new Color(1f, 0.6f, 0.2f);
                case BossSkillEffect.Shockwave: return new Color(1f, 0.25f, 0.2f);
                case BossSkillEffect.Beam: return new Color(0.85f, 0.4f, 1f);
                default: return new Color(0.5f, 1f, 0.5f);
            }
        }

        /// <summary>The effect a warning number stands for.</summary>
        public static BossSkillEffect EffectOfWarning(byte skillId) => (BossSkillEffect)(skillId - WarningIdBarrage);

        public static float CooldownOf(int slot) => slot == Barrage ? BarrageCooldownSeconds : ShockwaveCooldownSeconds;

        public static int BaseDamageOf(int slot) => slot == Barrage ? BarrageDamage : ShockwaveDamage;
    }

    public enum BossSkillResult
    {
        /// <summary>The skill was announced and its cooldown started.</summary>
        Used = 0,
        Cooldown = 1,
        /// <summary>The match is not running or the boss has fallen.</summary>
        Unavailable = 2,
        /// <summary>The request did not come from the boss.</summary>
        Ignored = 3,
        UnknownSlot = 4,
        /// <summary>The skill is not unlocked yet (the third skill before phase 2).</summary>
        Locked = 5,
    }

    /// <summary>What the skills need to know about the boss at the moment one is used.</summary>
    public struct BossSkillState
    {
        public bool MatchRunning;
        public bool BossStanding;
        public Vector2 BossPosition;
        /// <summary>The boss's aim as a 16-bit angle.</summary>
        public ushort Aim;
        /// <summary>The boss's health from 0 to 1; the strengthening of its shots applies to its skills too.</summary>
        public float HealthFraction;
    }

    /// <summary>
    /// The boss's skills as plain data: a cooldown for each, the announcement, and what each does when it takes effect. The shot ring
    /// fires a ring of the boss's own hostile projectiles; the shockwave hurts the survivors around the place it was used. A skill
    /// keeps the place, the aim and the strengthened damage of the moment it was used, even if the boss moves or weakens meanwhile.
    /// </summary>
    public sealed class BossSkillSim
    {
        struct Pending
        {
            public int Slot;
            public BossSkillDef Def;
            public float Remaining;
            public Vector2 Position;
            public ushort Aim;
            public int Damage;
            /// <summary>When above zero, how many mobs a roar makes instead of its data's count (the burst of a phase change).</summary>
            public int Mobs;
            public bool IsBurst;
        }

        readonly float[] cooldown = new float[BossSkills.SlotCount];
        readonly List<Pending> pending = new List<Pending>();

        /// <summary>Survivors hurt by beams and mobs made by roars since the last reset (for the development log).</summary>
        public int BeamHits { get; private set; }
        public int RoarMobs { get; private set; }
        /// <summary>Mobs made by the bursts of phase changes since the last reset (for the development log and tests).</summary>
        public int BurstMobs { get; private set; }

        /// <summary>The phase of the match, 1 to 3; it only goes up and is 1 after a reset.</summary>
        public int Phase { get; private set; } = BossPhases.First;

        /// <summary>The kind whose skills these are; the standard kind until a match says otherwise.</summary>
        public BossDef Kind { get; private set; } = BossCatalog.Default;

        /// <summary>When above zero it replaces the base damage of both skills (a development override); the strengthening still applies.</summary>
        public int BaseDamageOverride;

        /// <summary>Skills that are announced and have not taken effect yet.</summary>
        public int PendingCount => pending.Count;

        /// <summary>Seconds until the skill can be used again; zero when it is ready.</summary>
        public float CooldownLeft(int slot) => slot >= 0 && slot < cooldown.Length ? cooldown[slot] : 0f;

        /// <summary>A new match: both skills ready, nothing announced.</summary>
        public void Reset()
        {
            for (int i = 0; i < cooldown.Length; i++) cooldown[i] = 0f;
            pending.Clear();
            BeamHits = 0;
            RoarMobs = 0;
            BurstMobs = 0;
            Phase = BossPhases.First;
        }

        /// <summary>A new match with a boss of this kind.</summary>
        public void Reset(BossDef kind)
        {
            Kind = kind ?? BossCatalog.Default;
            Reset();
        }

        /// <summary>The skill of a slot for the playing kind: Q and E are the kind's skills, the third is its phase skill; null when there is none.</summary>
        BossSkillDef SkillOf(int slot)
        {
            if (slot < 0 || slot >= BossSkills.SlotCount) return null;
            if (slot == BossSkills.PhaseSlot) return Kind.PhaseSkill;
            return slot < Kind.Skills.Length ? Kind.Skills[slot] : null;
        }

        /// <summary>
        /// Moves the phase up when the boss's health says so and, when it does and the match is running, announces the burst of mobs of the new
        /// phase (a roar that takes effect after its warning; the announcement is added to <paramref name="events"/>). With <paramref name="enabled"/>
        /// false the phase stays 1. Returns true when the phase went up.
        /// </summary>
        public bool UpdatePhase(float healthFraction, bool enabled, bool matchRunning, Vector2 bossPosition, ulong owner, List<CombatEvent> events)
        {
            if (!enabled) return false;
            int next = Math.Max(Phase, BossPhases.PhaseOf(healthFraction));
            if (next == Phase) return false;

            Phase = next;
            BossSkillDef burst = Kind.PhaseBurst;
            if (matchRunning && burst != null)
            {
                pending.Add(new Pending
                {
                    Slot = -1, Def = burst, Remaining = burst.WarningSeconds, Position = bossPosition, Aim = 0, Damage = 0,
                    Mobs = BossPhases.BurstMobsOf(burst.Count, Phase), IsBurst = true,
                });
                events.Add(new CombatEvent
                {
                    Kind = CombatEventKind.Skill, SkillId = BossSkills.WarningIdOf(burst.Effect), Owner = owner, Position = bossPosition,
                    Rank = 1, Angle = 0,
                    RangeScale = SkillCatalog.NormalScalePercent, DurationScale = SkillCatalog.NormalScalePercent,
                });
            }
            return true;
        }

        /// <summary>
        /// Uses a skill: starts its cooldown and announces it. The announcement is added to <paramref name="events"/> as a skill event
        /// carrying the place and the aim. The skill takes effect later, in <see cref="Step"/>.
        /// </summary>
        public BossSkillResult TryUse(int slot, bool fromBoss, in BossSkillState state, ulong owner, List<CombatEvent> events)
        {
            if (!fromBoss) return BossSkillResult.Ignored;
            if (!state.MatchRunning || !state.BossStanding) return BossSkillResult.Unavailable;
            BossSkillDef def = SkillOf(slot);
            if (def == null) return BossSkillResult.UnknownSlot;
            if (slot == BossSkills.PhaseSlot && Phase < BossPhases.Second) return BossSkillResult.Locked;
            if (cooldown[slot] > 0f) return BossSkillResult.Cooldown;

            // The strengthening of the boss's shots applies: a weaker boss hits harder and its cooldown is shorter, and in the last phase
            // every cooldown is shorter still. All are decided now, from the health and the phase of this moment.
            int baseDamage = BaseDamageOverride > 0 ? BaseDamageOverride : def.Damage;
            float phaseFactor = Phase >= BossPhases.Third ? BossPhases.ThirdPhaseCooldownFactor : 1f;
            cooldown[slot] = def.CooldownSeconds * BossAttack.CooldownMultiplier(state.HealthFraction) * phaseFactor;
            pending.Add(new Pending
            {
                Slot = slot, Def = def, Remaining = def.WarningSeconds, Position = state.BossPosition, Aim = state.Aim,
                Damage = BossAttack.Damage(baseDamage, state.HealthFraction),
            });

            events.Add(new CombatEvent
            {
                Kind = CombatEventKind.Skill, SkillId = BossSkills.WarningIdOf(def.Effect), Owner = owner, Position = state.BossPosition,
                Rank = 1, Angle = state.Aim,
                RangeScale = SkillCatalog.NormalScalePercent, DurationScale = SkillCatalog.NormalScalePercent,
            });
            return BossSkillResult.Used;
        }

        /// <summary>
        /// Advances the cooldowns and the announcements, only while the match is running, and lets every skill whose announcement has
        /// ended take effect. A skill whose time has come after the boss fell is dropped; when the match is not running everything that
        /// was announced is dropped. Returns how many skills took effect.
        /// </summary>
        public int Step(float dt, bool matchRunning, bool bossStanding, ProjectileSim projectiles, SurvivorSim survivors,
                        IReadOnlyList<SurvivorBody> bodies, ulong owner, List<CombatEvent> events, MobSim mobs = null)
        {
            if (!matchRunning)
            {
                pending.Clear();
                return 0;
            }

            for (int i = 0; i < cooldown.Length; i++) cooldown[i] = Math.Max(0f, cooldown[i] - dt);

            int released = 0;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                Pending p = pending[i];
                p.Remaining -= dt;
                if (p.Remaining > 0f)
                {
                    pending[i] = p;
                    continue;
                }

                pending.RemoveAt(i);
                if (!bossStanding) continue;

                Release(p, projectiles, survivors, bodies, owner, events, mobs);
                released++;
            }
            return released;
        }

        void Release(Pending p, ProjectileSim projectiles, SurvivorSim survivors, IReadOnlyList<SurvivorBody> bodies, ulong owner,
                     List<CombatEvent> events, MobSim mobs)
        {
            BossSkillDef def = p.Def;
            switch (def.Effect)
            {
                case BossSkillEffect.ShotRing:
                    for (int i = 0; i < def.Count; i++)
                    {
                        var spec = new ProjectileSpec
                        {
                            Owner = owner, Origin = p.Position, Hostile = true, Damage = p.Damage, Shape = Kind.Attack.Shape,
                            Angle = (ushort)((p.Aim + i * 65536 / def.Count) & 0xFFFF),
                        };
                        if (!projectiles.TryFire(spec, out _)) break; // the cap leaves room for no more
                    }
                    break;

                case BossSkillEffect.Shockwave:
                    survivors.ApplyBlast(p.Position, def.Radius, p.Damage, bodies);
                    events.Add(new CombatEvent
                    {
                        Kind = CombatEventKind.Explosion, Position = p.Position, Radius10 = CombatCodec.QuantizeRadius(def.Radius),
                    });
                    break;

                case BossSkillEffect.Beam:
                    BeamHits += ReleaseBeam(p, def, survivors, bodies);
                    break;

                case BossSkillEffect.Roar:
                {
                    int made = ReleaseRoar(p, def, mobs);
                    if (p.IsBurst) BurstMobs += made;
                    else RoarMobs += made;
                    break;
                }
            }
        }

        /// <summary>
        /// The beam hurts every living survivor whose body touches the line from where it was used along the aim of that moment, for
        /// the beam's length. Behind the start or beyond the length nothing is hit; mobs are not hurt.
        /// </summary>
        static int ReleaseBeam(Pending p, BossSkillDef def, SurvivorSim survivors, IReadOnlyList<SurvivorBody> bodies)
        {
            int hurt = 0;
            Vector2 direction = AimMath.Direction(p.Aim);
            float reach = def.HalfWidth + survivors.Radius;
            for (int i = 0; i < bodies.Count; i++)
            {
                Vector2 offset = bodies[i].Position - p.Position;
                float along = Vector2.Dot(offset, direction);
                if (along < 0f || along > def.Length) continue;
                Vector2 closest = direction * along;
                if ((offset - closest).sqrMagnitude > reach * reach) continue;

                if (survivors.ApplyDamage(bodies[i].ClientId, p.Damage)) hurt++;
            }
            return hurt;
        }

        /// <summary>The roar creates its mobs of the summoned kind around the place it was used, as many as the mob cap allows.</summary>
        static int ReleaseRoar(Pending p, BossSkillDef def, MobSim mobs)
        {
            if (mobs == null) return 0;
            int made = 0;
            byte type = MobKinds.Encode(MobKinds.OriginSummoned, MobKinds.Basic);
            int count = p.Mobs > 0 ? p.Mobs : def.Count;
            for (int i = 0; i < count; i++)
            {
                float angle = i * 2f * Mathf.PI / Mathf.Max(1, count);
                Vector2 spot = p.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * RoarDistance;
                if (!mobs.TrySpawn(spot, type, out _)) break;
                made++;
            }
            return made;
        }

        /// <summary>How far from the boss the mobs of a roar appear.</summary>
        public const float RoarDistance = 2.5f;
    }
}
