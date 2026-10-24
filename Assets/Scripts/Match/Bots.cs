using System;
using System.Collections.Generic;
using UnityEngine;

namespace BS
{
    /// <summary>How well a bot plays. The numbers are starting values, tuned together with the balance of the game.</summary>
    public sealed class SkillProfile
    {
        public string Name;
        /// <summary>The most a shot misses its target by, in degrees (the miss is picked again each time the bot thinks).</summary>
        public float AimErrorDegrees;
        /// <summary>How long the bot goes on with what it decided before it looks at the field again, in seconds.</summary>
        public float ReactionSeconds;
        /// <summary>The chance, from 0 to 1, that a survivor in range of the boss shoots the boss instead of the nearest mob.</summary>
        public float BossPreference;
    }

    /// <summary>The skill profiles by name.</summary>
    public static class SkillProfiles
    {
        public static readonly SkillProfile Weak = new SkillProfile { Name = "weak", AimErrorDegrees = 12f, ReactionSeconds = 0.6f, BossPreference = 0.3f };
        public static readonly SkillProfile Normal = new SkillProfile { Name = "normal", AimErrorDegrees = 6f, ReactionSeconds = 0.3f, BossPreference = 0.6f };
        public static readonly SkillProfile Strong = new SkillProfile { Name = "strong", AimErrorDegrees = 2f, ReactionSeconds = 0.12f, BossPreference = 0.9f };

        public static readonly IReadOnlyList<SkillProfile> All = new[] { Weak, Normal, Strong };

        public static bool IsKnown(string name) => Find(name) != null;

        /// <summary>The profile of that name, or null.</summary>
        public static SkillProfile Find(string name)
        {
            for (int i = 0; i < All.Count; i++)
                if (string.Equals(All[i].Name, name, StringComparison.OrdinalIgnoreCase)) return All[i];
            return null;
        }
    }

    /// <summary>Small helpers the bots share.</summary>
    public static class BotMath
    {
        /// <summary>False turns the dodging off (experiments that measure what it is worth).</summary>
        public static bool DodgeShots = true;

        public static Vector2 Rotate(Vector2 v, float radians)
        {
            float c = Mathf.Cos(radians), s = Mathf.Sin(radians);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>The nearest mob (a device included) to a point, or -1 when there is none.</summary>
        public static int NearestMob(IMobSource mobs, Vector2 from, out float distance)
        {
            int best = -1;
            float bestSq = float.MaxValue;
            for (int i = 0; i < mobs.Count; i++)
            {
                float d = (mobs.PositionAt(i) - from).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = i; }
            }
            distance = best >= 0 ? Mathf.Sqrt(bestSq) : float.MaxValue;
            return best;
        }

        /// <summary>
        /// The unit direction from <paramref name="threat"/> to <paramref name="me"/>. When the two are on the same spot (a mob that walked
        /// onto the survivor) there is no direction, so one is picked from the position, never zero.
        /// </summary>
        public static Vector2 AwayFrom(Vector2 me, Vector2 threat)
        {
            Vector2 d = me - threat;
            if (d.sqrMagnitude > 1e-6f) return d.normalized;
            float angle = (me.x * 12.9898f + me.y * 78.233f);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        /// <summary>
        /// A walking direction that takes a body out of the way of the hostile projectiles that will pass through it soon, or zero when none
        /// will. Each such projectile pushes sideways (away from where it will pass), harder the sooner it arrives.
        /// </summary>
        public static Vector2 Dodge(ProjectileSim shots, Vector2 me, float bodyRadius)
        {
            Vector2 push = Vector2.zero;
            for (int i = 0; i < shots.Count; i++)
            {
                if (!shots.HostileAt(i)) continue;

                Vector2 v = shots.DirectionAt(i) * shots.SpeedAt(i);
                float speedSq = v.sqrMagnitude;
                if (speedSq < 1e-6f) continue;

                Vector2 r = shots.PositionAt(i) - me;
                float t = -Vector2.Dot(r, v) / speedSq; // when it is nearest to me
                if (t < 0f || t > Mathf.Min(shots.RemainingSecondsAt(i), 1.0f)) continue; // already past, or too far ahead to matter

                Vector2 closest = r + v * t;
                float reach = shots.RadiusAt(i) + bodyRadius + 0.25f;
                if (closest.magnitude >= reach) continue; // it passes clear of me

                Vector2 sideways = new Vector2(-v.y, v.x).normalized;
                if (Vector2.Dot(sideways, closest) > 0f) sideways = -sideways; // step to the side it is not passing on
                if (closest.sqrMagnitude < 1e-4f && Vector2.Dot(sideways, PullToCenter(me)) < 0f) sideways = -sideways; // dead on: toward the middle
                push += sideways / (t + 0.15f);
            }
            return push;
        }

        /// <summary>A walking direction away from the arena's walls (zero in the middle, growing toward the edge).</summary>
        public static Vector2 PullToCenter(Vector2 p)
        {
            float hx = ArenaConfig.Width * 0.5f;
            float hy = ArenaConfig.Height * 0.5f;
            float fx = Mathf.Clamp01((Mathf.Abs(p.x) / hx - 0.7f) / 0.3f);
            float fy = Mathf.Clamp01((Mathf.Abs(p.y) / hy - 0.7f) / 0.3f);
            return new Vector2(-Mathf.Sign(p.x) * fx, -Mathf.Sign(p.y) * fy);
        }
    }

    /// <summary>
    /// A survivor played by the harness. It reads only what a player could see (where the boss, the mobs and the other survivors are, its own
    /// health) and every <see cref="SkillProfile.ReactionSeconds"/> it decides again whom to shoot and where to walk: away from what is
    /// close, toward a fallen teammate to revive, toward the boss when out of range.
    /// </summary>
    public sealed class SurvivorBot
    {
        const float MobDanger = 2.2f;
        const float BossDanger = 4.5f;
        const float ReviveSearch = 14f;
        const float SkillEvery = 0.4f;

        readonly ulong id;
        readonly SkillProfile profile;
        readonly System.Random rng;
        readonly float shotRange;

        float nextThink;
        float nextSkill;
        bool targetBoss;
        bool hasTarget;
        float aimOffset;
        Vector2 move;

        // An open skill choice waits for the bot's answer.
        bool offerOpen;
        byte offerId;
        int offerChoices;
        float answerAt;

        public SurvivorBot(ulong survivorId, byte character, SkillProfile skillProfile, int seed)
        {
            id = survivorId;
            profile = skillProfile;
            rng = new System.Random(seed);
            CharacterDef def = CharacterCatalog.Get(character);
            shotRange = def != null ? def.Attack.Shape.Range : ProjectileShape.Default.Range;
            nextThink = 0f;
        }

        float Jitter(float seconds) => seconds * (0.8f + 0.4f * (float)rng.NextDouble());

        public void Decide(MatchSession s, float dt)
        {
            if (!s.IsAlive(id))
            {
                s.Move(id, Vector2.zero);
                s.Aim(id, 0, false);
                return;
            }

            MatchWorld w = s.World;
            Vector2 me = s.PositionOf(id);

            ObserveOffer(s);
            if (s.Time >= nextThink)
            {
                Think(s, me);
                nextThink = s.Time + Jitter(profile.ReactionSeconds);
            }

            // Aim at where the target is now, off by the miss decided when the bot last thought.
            bool firing = false;
            ushort angle = 0;
            if (hasTarget && TryTargetPosition(s, out Vector2 target))
            {
                Vector2 toTarget = target - me;
                firing = toTarget.magnitude <= shotRange * 1.05f;
                angle = AimMath.FromDirection(BotMath.Rotate(toTarget, aimOffset));
            }
            s.Aim(id, angle, firing);
            s.Move(id, move);

            if (s.Time >= nextSkill)
            {
                nextSkill = s.Time + SkillEvery;
                if (AnyoneToUseSkillsOn(s, me))
                    for (int slot = 0; slot < SkillCatalog.SkillSlots; slot++) s.UseSkill(id, slot);
            }

            if (offerOpen && s.Time >= answerAt)
            {
                offerOpen = false;
                w.Pick(id, offerId, rng.Next(Mathf.Max(1, offerChoices)));
            }
        }

        void ObserveOffer(MatchSession s)
        {
            List<GrowthEvent> events = s.World.GrowthEvents;
            for (int i = 0; i < events.Count; i++)
            {
                GrowthEvent e = events[i];
                if (e.ClientId != id) continue;

                if (e.Kind == GrowthEventKind.OfferOpened)
                {
                    offerOpen = true;
                    offerId = e.OfferId;
                    offerChoices = e.Choices != null ? e.Choices.Length : 1;
                    answerAt = s.Time + Jitter(profile.ReactionSeconds * 2f);
                }
                else if (e.Kind == GrowthEventKind.OfferClosed)
                {
                    offerOpen = false;
                }
            }
        }

        bool TryTargetPosition(MatchSession s, out Vector2 position)
        {
            position = default;
            if (targetBoss)
            {
                if (!s.World.Boss.CanBeHit) return false;
                position = s.PositionOf(MatchSession.BossId);
                return true;
            }

            int index = BotMath.NearestMob(s.World.Mobs, s.PositionOf(id), out _);
            if (index < 0) return false;
            position = s.World.Mobs.PositionAt(index);
            return true;
        }

        bool AnyoneToUseSkillsOn(MatchSession s, Vector2 me)
        {
            BossSim boss = s.World.Boss;
            if (boss.CanBeHit && Vector2.Distance(s.PositionOf(MatchSession.BossId), me) <= shotRange) return true;
            BotMath.NearestMob(s.World.Mobs, me, out float mobDistance);
            if (mobDistance <= shotRange * 0.8f) return true;

            foreach (ulong other in s.SurvivorIds)
            {
                if (!s.IsAlive(other)) continue;
                if (s.World.Survivors.Health(other) < s.World.Survivors.MaxHealthOf(other) * 0.7f) return true;
            }
            return false;
        }

        void Think(MatchSession s, Vector2 me)
        {
            MatchWorld w = s.World;
            aimOffset = ((float)rng.NextDouble() * 2f - 1f) * profile.AimErrorDegrees * Mathf.Deg2Rad;

            bool bossUp = w.Boss.CanBeHit;
            Vector2 bossPos = s.PositionOf(MatchSession.BossId);
            float bossDistance = bossUp ? Vector2.Distance(bossPos, me) : float.MaxValue;
            int mob = BotMath.NearestMob(w.Mobs, me, out float mobDistance);

            // Whom to shoot: the boss when it is in range and the bot prefers it, else the nearest mob, else the boss.
            hasTarget = true;
            if (bossUp && bossDistance <= shotRange && rng.NextDouble() < profile.BossPreference) targetBoss = true;
            else if (mob >= 0 && mobDistance <= shotRange) targetBoss = false;
            else if (bossUp) targetBoss = true;
            else if (mob >= 0) targetBoss = false;
            else hasTarget = false;

            // Where to walk: away from what is close, else to a fallen teammate, else toward the boss when it is out of range.
            Vector2 away = Vector2.zero;
            if (mob >= 0 && mobDistance < MobDanger)
                away += BotMath.AwayFrom(me, w.Mobs.PositionAt(mob)) * (MobDanger / Mathf.Max(0.3f, mobDistance));
            if (bossUp && bossDistance < BossDanger)
                away += BotMath.AwayFrom(me, bossPos) * (BossDanger / Mathf.Max(0.5f, bossDistance)) * 1.5f;

            // A shot that is about to hit comes first: step sideways out of its way.
            Vector2 dodge = BotMath.DodgeShots ? BotMath.Dodge(w.Projectiles, me, CombatConfig.SurvivorRadius) : Vector2.zero;
            if (dodge.sqrMagnitude > 1e-4f)
            {
                move = (dodge.normalized + (away.sqrMagnitude > 1e-4f ? away.normalized * 0.5f : Vector2.zero)).normalized;
                return;
            }

            if (away.sqrMagnitude > 1e-4f)
            {
                move = (away.normalized + BotMath.PullToCenter(me)).normalized;
                return;
            }

            if (TryFallenTeammate(s, me, out Vector2 fallen, out float fallenDistance))
            {
                // Walk to the teammate and stand still next to it: a moving rescuer resets the revival.
                move = fallenDistance > 1.0f ? (fallen - me).normalized : Vector2.zero;
                return;
            }

            if (bossUp && bossDistance > shotRange * 0.75f) move = (bossPos - me).normalized;
            else move = Vector2.zero;
        }

        /// <summary>The nearest fallen teammate this bot should rescue (the nearest living survivor to it goes).</summary>
        bool TryFallenTeammate(MatchSession s, Vector2 me, out Vector2 position, out float distance)
        {
            position = default;
            distance = float.MaxValue;
            ulong fallenId = 0;
            foreach (ulong other in s.SurvivorIds)
            {
                if (other == id || s.IsAlive(other)) continue;
                float d = Vector2.Distance(s.PositionOf(other), me);
                if (d < distance && d <= ReviveSearch) { distance = d; fallenId = other; position = s.PositionOf(other); }
            }
            if (fallenId == 0) return false;

            // Someone else is nearer to the fallen one (or as near with a lower id): that one goes.
            foreach (ulong other in s.SurvivorIds)
            {
                if (other == id || !s.IsAlive(other)) continue;
                float theirs = Vector2.Distance(s.PositionOf(other), position);
                if (theirs < distance - 0.01f || (Mathf.Abs(theirs - distance) <= 0.01f && other < id)) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// The boss played by the harness: it chases the nearest living survivor, shoots, uses its skills when they are ready, summons mobs in
    /// turn and places devices. It uses the aim error and the reaction time of the skill profile.
    /// </summary>
    public sealed class BossBot
    {
        const float SummonEvery = 1.5f;
        const float DeviceEvery = 1.0f;
        const float SummonAboveEnergy = 20f;

        static readonly byte[] SummonKinds =
        {
            MobKinds.Basic, MobKinds.Fast, MobKinds.Tough, MobKinds.Ranged, MobKinds.Exploder,
        };

        readonly SkillProfile profile;
        readonly System.Random rng;
        readonly float shotRange;

        float nextThink;
        float nextSummon;
        float nextDevice;
        float nextSkill;
        int summonTurn;
        float aimOffset;
        Vector2 move;
        bool hasTarget;
        Vector2 targetPosition;

        public BossBot(BossDef def, SkillProfile skillProfile, int seed)
        {
            profile = skillProfile;
            rng = new System.Random(seed);
            shotRange = def.Attack.Shape.Range;
        }

        float Jitter(float seconds) => seconds * (0.8f + 0.4f * (float)rng.NextDouble());

        public void Decide(MatchSession s, float dt)
        {
            MatchWorld w = s.World;
            if (!w.Boss.CanBeHit)
            {
                s.Move(MatchSession.BossId, Vector2.zero);
                s.Aim(MatchSession.BossId, 0, false);
                return;
            }

            Vector2 me = s.PositionOf(MatchSession.BossId);

            if (s.Time >= nextThink)
            {
                nextThink = s.Time + Jitter(profile.ReactionSeconds);
                Think(s, me);
            }

            // Shoot at the target where it is now.
            bool firing = false;
            ushort angle = 0;
            if (hasTarget)
            {
                Vector2 now = NearestSurvivor(s, me, out bool found);
                if (found) targetPosition = now;
                Vector2 toTarget = targetPosition - me;
                firing = toTarget.magnitude <= shotRange * 1.05f;
                angle = AimMath.FromDirection(BotMath.Rotate(toTarget, aimOffset));
            }
            s.Aim(MatchSession.BossId, angle, firing);
            s.Move(MatchSession.BossId, move);

            if (hasTarget && s.Time >= nextSkill)
            {
                nextSkill = s.Time + Jitter(profile.ReactionSeconds);
                for (int slot = 0; slot < BossSkills.SlotCount; slot++) s.UseBossSkill(slot, angle); // the third skill is refused until phase 2
            }

            if (s.Time >= nextDevice)
            {
                nextDevice = s.Time + DeviceEvery;
                s.PlaceDevice();
            }

            if (s.Time >= nextSummon && w.Summon.Energy >= SummonAboveEnergy)
            {
                nextSummon = s.Time + SummonEvery;
                s.Summon(SummonKinds[summonTurn++ % SummonKinds.Length]);
            }
        }

        static Vector2 NearestSurvivor(MatchSession s, Vector2 from, out bool found)
        {
            found = false;
            Vector2 best = default;
            float bestSq = float.MaxValue;
            foreach (ulong id in s.SurvivorIds)
            {
                if (!s.IsAlive(id)) continue;
                Vector2 p = s.PositionOf(id);
                float d = (p - from).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = p; found = true; }
            }
            return best;
        }

        void Think(MatchSession s, Vector2 me)
        {
            aimOffset = ((float)rng.NextDouble() * 2f - 1f) * profile.AimErrorDegrees * Mathf.Deg2Rad;
            Vector2 nearest = NearestSurvivor(s, me, out bool found);
            hasTarget = found;
            if (!found) { move = Vector2.zero; return; }

            targetPosition = nearest;
            move = (nearest - me).normalized;
        }
    }

    /// <summary>Plays a whole match: four survivor bots and the boss bot.</summary>
    public sealed class BotController : IMatchController
    {
        readonly List<SurvivorBot> survivors = new List<SurvivorBot>();
        readonly BossBot boss;

        public BotController(MatchSession session, SkillProfile profile)
        {
            for (int i = 0; i < session.SurvivorIds.Count; i++)
            {
                ulong id = session.SurvivorIds[i];
                survivors.Add(new SurvivorBot(id, session.CharacterOf(id), profile, unchecked(session.Seed * 101 + i * 7 + 5)));
            }
            boss = new BossBot(session.BossData, profile, unchecked(session.Seed * 211 + 9));
        }

        public void Decide(MatchSession session, float dt)
        {
            for (int i = 0; i < survivors.Count; i++) survivors[i].Decide(session, dt);
            boss.Decide(session, dt);
        }
    }
}
