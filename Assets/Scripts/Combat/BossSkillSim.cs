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
        public const int Count = 2;
        public const int Barrage = 0;
        public const int Shockwave = 1;

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

        public static bool IsWarningId(byte skillId) => skillId == WarningIdBarrage || skillId == WarningIdShockwave;

        public static byte WarningIdOf(int slot) => slot == Barrage ? WarningIdBarrage : WarningIdShockwave;

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
            public float Remaining;
            public Vector2 Position;
            public ushort Aim;
            public int Damage;
        }

        readonly float[] cooldown = new float[BossSkills.Count];
        readonly List<Pending> pending = new List<Pending>();

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
        }

        /// <summary>
        /// Uses a skill: starts its cooldown and announces it. The announcement is added to <paramref name="events"/> as a skill event
        /// carrying the place and the aim. The skill takes effect later, in <see cref="Step"/>.
        /// </summary>
        public BossSkillResult TryUse(int slot, bool fromBoss, in BossSkillState state, ulong owner, List<CombatEvent> events)
        {
            if (!fromBoss) return BossSkillResult.Ignored;
            if (!state.MatchRunning || !state.BossStanding) return BossSkillResult.Unavailable;
            if (slot < 0 || slot >= BossSkills.Count) return BossSkillResult.UnknownSlot;
            if (cooldown[slot] > 0f) return BossSkillResult.Cooldown;

            // The strengthening of the boss's shots applies: a weaker boss hits harder and its cooldown is shorter. Both are
            // decided now, from the health of this moment.
            int baseDamage = BaseDamageOverride > 0 ? BaseDamageOverride : BossSkills.BaseDamageOf(slot);
            cooldown[slot] = BossSkills.CooldownOf(slot) * BossAttack.CooldownMultiplier(state.HealthFraction);
            pending.Add(new Pending
            {
                Slot = slot, Remaining = BossSkills.WarningSeconds, Position = state.BossPosition, Aim = state.Aim,
                Damage = BossAttack.Damage(baseDamage, state.HealthFraction),
            });

            events.Add(new CombatEvent
            {
                Kind = CombatEventKind.Skill, SkillId = BossSkills.WarningIdOf(slot), Owner = owner, Position = state.BossPosition,
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
                        IReadOnlyList<SurvivorBody> bodies, ulong owner, List<CombatEvent> events)
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

                Release(p, projectiles, survivors, bodies, owner, events);
                released++;
            }
            return released;
        }

        static void Release(Pending p, ProjectileSim projectiles, SurvivorSim survivors, IReadOnlyList<SurvivorBody> bodies, ulong owner,
                            List<CombatEvent> events)
        {
            if (p.Slot == BossSkills.Barrage)
            {
                for (int i = 0; i < BossSkills.BarrageShots; i++)
                {
                    var spec = new ProjectileSpec
                    {
                        Owner = owner, Origin = p.Position, Hostile = true, Damage = p.Damage, Shape = BossAttack.Shape,
                        Angle = (ushort)((p.Aim + i * 65536 / BossSkills.BarrageShots) & 0xFFFF),
                    };
                    if (!projectiles.TryFire(spec, out _)) break; // the cap leaves room for no more
                }
                return;
            }

            survivors.ApplyBlast(p.Position, BossSkills.ShockwaveRadius, p.Damage, bodies);
            events.Add(new CombatEvent
            {
                Kind = CombatEventKind.Explosion, Position = p.Position, Radius10 = CombatCodec.QuantizeRadius(BossSkills.ShockwaveRadius),
            });
        }
    }
}
