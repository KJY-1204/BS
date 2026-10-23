using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class SkillEvolutionEffectTests
    {
        const ulong A = 1, B = 2;
        const int Q = 0, E = 1, R = 2;

        static SkillSim NewSkills(int mana = 1000)
        {
            var sim = new SkillSim();
            sim.Register(A, mana, 100f);
            return sim;
        }

        static int[] Ranks(params (byte id, int rank)[] set)
        {
            var ranks = new int[SkillCatalog.MaxSkillId + 1];
            foreach ((byte id, int rank) in set) ranks[id] = rank;
            return ranks;
        }

        static SkillActivation Fire(SkillSim sim, byte character, int slot, int[] ranks)
        {
            Assert.AreEqual(SkillResult.Fired, sim.TryActivate(A, character, slot, true, ranks, out SkillActivation act));
            return act;
        }

        static SkillActivation Evolved(byte evolution, params (byte id, int rank)[] more)
        {
            SkillDef def = SkillCatalog.Get(evolution);
            var set = new List<(byte, int)> { (def.EvolvesFrom, 3), (evolution, 1) };
            set.AddRange(more);
            return Fire(NewSkills(), def.Character, Q, Ranks(set.ToArray()));
        }

        static MobSim Mobs(int health = 1000) => new MobSim(targetCount: 0) { MaxHealth = health };

        static ushort Spawn(MobSim mobs, float x, float y)
        {
            Assert.IsTrue(mobs.TrySpawn(new Vector2(x, y), 0, out ushort id));
            return id;
        }

        static int Health(MobSim mobs, ushort id)
        {
            Assert.IsTrue(mobs.TryGetHealth(id, out int h));
            return h;
        }

        static ushort AngleDegrees(float degrees) => AimMath.QuantizeAngle(degrees * Mathf.Deg2Rad);

        // ---- pressing Q fires the evolution ----

        [Test]
        public void PressingQFiresTheEvolutionWithItsOwnCostCooldownAndValues()
        {
            foreach (SkillDef evolution in SkillCatalog.All.Where(d => d.EvolvesFrom != 0))
            {
                SkillDef baseSkill = SkillCatalog.Get(evolution.EvolvesFrom);
                SkillSim sim = NewSkills();
                int[] ranks = Ranks((baseSkill.Id, 3), (evolution.Id, 1));

                SkillActivation act = Fire(sim, evolution.Character, Q, ranks);

                Assert.AreEqual(evolution.Id, act.SkillId, evolution.Name);
                Assert.AreEqual(1, act.Rank);
                Assert.AreEqual(1000f - evolution.ManaCost, sim.Mana(A), 1e-3f, evolution.Name);
                Assert.AreEqual(evolution.Cooldown, sim.CooldownLeft(A, Q), 1e-3f, evolution.Name);
                Assert.AreEqual(evolution.Cooldown, act.Cooldown, 1e-3f);
            }
        }

        [Test]
        public void TheBaseSkillFiresWhenNoEvolutionIsLearned()
        {
            SkillDef burst = SkillCatalog.Get(SkillIds.Burst);
            SkillActivation act = Fire(NewSkills(), CharacterIds.Reimu, Q, Ranks((SkillIds.Burst, 3)));

            Assert.AreEqual(SkillIds.Burst, act.SkillId);
            Assert.AreEqual(burst.Damage[2], act.Damage);
        }

        // ---- the eight effects ----

        [Test]
        public void ReimusSpreadBurstIsWiderAndWeaker_AndTheFocusBurstSmallerAndHarder()
        {
            SkillActivation spread = Evolved(SkillIds.BurstSpread), focus = Evolved(SkillIds.BurstFocus);
            MobSim viaSpread = Mobs(), viaFocus = Mobs();
            ushort s4 = Spawn(viaSpread, 4, 0), s6 = Spawn(viaSpread, 6, 0);
            ushort f1 = Spawn(viaFocus, 1.5f, 0), f3 = Spawn(viaFocus, 3, 0);

            SkillExecutor.Apply(spread, new SkillContext { CasterId = A, Mobs = viaSpread });
            SkillExecutor.Apply(focus, new SkillContext { CasterId = A, Mobs = viaFocus });

            Assert.AreEqual(1000 - spread.Damage, Health(viaSpread, s4));
            Assert.AreEqual(1000, Health(viaSpread, s6));
            Assert.AreEqual(1000 - focus.Damage, Health(viaFocus, f1));
            Assert.AreEqual(1000, Health(viaFocus, f3), "outside the focused burst");
            Assert.Greater(focus.Damage, spread.Damage);
        }

        [Test]
        public void MarisasWideBeamAndLongLaserReachWhatTheirNumbersSay()
        {
            SkillActivation wide = Evolved(SkillIds.SparkWide), laser = Evolved(SkillIds.SparkLaser);
            Vector2 origin = new Vector2(-15f, 0f);
            MobSim viaWide = Mobs(), viaLaser = Mobs();
            ushort wSide = Spawn(viaWide, origin.x + 5f, 1.0f), wBeyond = Spawn(viaWide, origin.x + 11.5f, 0f);
            ushort lSide = Spawn(viaLaser, origin.x + 5f, 0.7f), lFar = Spawn(viaLaser, origin.x + 14f, 0f);

            SkillExecutor.Apply(wide, new SkillContext { CasterId = A, Origin = origin, AimAngle = AngleDegrees(0), Mobs = viaWide });
            SkillExecutor.Apply(laser, new SkillContext { CasterId = A, Origin = origin, AimAngle = AngleDegrees(0), Mobs = viaLaser });

            Assert.AreEqual(1000 - wide.Damage, Health(viaWide, wSide), "inside the wide beam");
            Assert.AreEqual(1000, Health(viaWide, wBeyond), "beyond the wide beam's length");
            Assert.AreEqual(1000, Health(viaLaser, lSide), "beside the thin laser");
            Assert.AreEqual(1000 - laser.Damage, Health(viaLaser, lFar), "far down the laser");
        }

        [Test]
        public void SakuyasStopsFreezeForTheirDurations()
        {
            SkillActivation longStop = Evolved(SkillIds.StopLong), frequent = Evolved(SkillIds.StopFrequent);
            Assert.AreEqual(5f, longStop.BuffSeconds, 1e-4f);
            Assert.AreEqual(1.8f, frequent.BuffSeconds, 1e-4f);

            MobSim mobs = Mobs();
            Spawn(mobs, 5, 5);
            Assert.AreEqual(1, SkillExecutor.Apply(frequent, new SkillContext { CasterId = A, Mobs = mobs }));
            Assert.IsTrue(mobs.IsFrozen);
            Assert.AreEqual(1.8f, mobs.FrozenSeconds, 1e-4f);
        }

        static SurvivorSim Hurt(params ulong[] ids)
        {
            var survivors = new SurvivorSim();
            foreach (ulong id in ids) { survivors.Register(id, 200); survivors.ApplyDamage(id, 150); }
            return survivors;
        }

        [Test]
        public void SanaesBigMiracleReachesFartherAndTheQuickOneHealsLess()
        {
            SkillActivation big = Evolved(SkillIds.MiracleBig), quick = Evolved(SkillIds.MiracleQuick);
            var bodies = new List<GrowthBody>
            {
                new GrowthBody { ClientId = A, Position = Vector2.zero, Alive = true },
                new GrowthBody { ClientId = B, Position = new Vector2(6f, 0), Alive = true },
            };

            SurvivorSim viaBig = Hurt(A, B), viaQuick = Hurt(A, B);
            SkillExecutor.Apply(big, new SkillContext { CasterId = A, Survivors = viaBig, Bodies = bodies });
            SkillExecutor.Apply(quick, new SkillContext { CasterId = A, Survivors = viaQuick, Bodies = bodies });

            Assert.AreEqual(50 + big.Amount, viaBig.Health(B), "6 away is inside the big radius");
            Assert.AreEqual(50, viaQuick.Health(B), "and outside the quick one's");
            Assert.AreEqual(50 + quick.Amount, viaQuick.Health(A));
            Assert.Less(quick.Amount, big.Amount);
        }

        // ---- the rules every skill follows ----

        [Test]
        public void ThePassivesApplyToTheEvolutionLikeToTheBaseSkill()
        {
            SkillDef focus = SkillCatalog.Get(SkillIds.BurstFocus);
            int[] saving = Ranks((SkillIds.Burst, 3), (SkillIds.BurstFocus, 1), (SkillIds.SpiritSaving, 3));
            SkillSim sim = NewSkills();
            sim.TryActivate(A, CharacterIds.Reimu, Q, true, saving, out _);
            int cost = SkillCatalog.EffectiveManaCost(focus, saving, CharacterIds.Reimu);
            Assert.Less(cost, focus.ManaCost);
            Assert.AreEqual(1000f - cost, sim.Mana(A), 1e-3f);

            int[] power = Ranks((SkillIds.MasterSpark, 3), (SkillIds.SparkLaser, 1), (SkillIds.FirePower, 3), (SkillIds.CritMagic, 3));
            SkillActivation boosted = Fire(NewSkills(), CharacterIds.Marisa, Q, power);
            SkillActivation plain = Evolved(SkillIds.SparkLaser);
            Assert.Greater(boosted.Damage, plain.Damage);
            Assert.Greater(boosted.Length, plain.Length);
            Assert.AreEqual(SkillCatalog.Get(SkillIds.SparkLaser).Length[0] * boosted.RangeScale / 100f, boosted.Length, 1e-4f);

            SkillActivation stretched = Evolved(SkillIds.StopFrequent, (SkillIds.TimeRule, 3));
            Assert.Greater(stretched.BuffSeconds, 1.8f);
        }

        [Test]
        public void TheSameRefusalsApplyToAnEvolvedSkill()
        {
            SkillDef evolution = SkillCatalog.Get(SkillIds.StopLong);
            int[] ranks = Ranks((SkillIds.TimeStop, 3), (SkillIds.StopLong, 1));

            SkillSim poor = NewSkills(evolution.ManaCost - 1);
            Assert.AreEqual(SkillResult.NoMana, poor.TryActivate(A, CharacterIds.Sakuya, Q, true, ranks, out _));
            Assert.AreEqual(evolution.ManaCost - 1, poor.Mana(A), 1e-4f);
            Assert.AreEqual(0f, poor.CooldownLeft(A, Q));

            SkillSim dead = NewSkills();
            Assert.AreEqual(SkillResult.Dead, dead.TryActivate(A, CharacterIds.Sakuya, Q, false, ranks, out _));

            SkillSim cooling = NewSkills();
            Fire(cooling, CharacterIds.Sakuya, Q, ranks);
            Assert.AreEqual(SkillResult.Cooldown, cooling.TryActivate(A, CharacterIds.Sakuya, Q, true, ranks, out _));

            Assert.AreEqual(SkillResult.NotLearned, NewSkills().TryActivate(A, CharacterIds.Sakuya, Q, true, Ranks(), out _), "nothing learned");
        }

        [Test]
        public void AnEvolvedQSkillLeavesTheECooldownAndTheRCooldownAlone()
        {
            SkillSim sim = NewSkills();
            int[] ranks = Ranks((SkillIds.Burst, 3), (SkillIds.BurstFocus, 1), (SkillIds.Barrier, 1), (SkillIds.FantasyHeaven, 1));

            Fire(sim, CharacterIds.Reimu, Q, ranks);
            Assert.AreEqual(0f, sim.CooldownLeft(A, E));
            Assert.AreEqual(0f, sim.CooldownLeft(A, R));
            Fire(sim, CharacterIds.Reimu, E, ranks);
            Fire(sim, CharacterIds.Reimu, R, ranks);
            Assert.AreEqual(SkillIds.FantasyHeaven, Fire(NewSkills(), CharacterIds.Reimu, R, ranks).SkillId, "R is not replaced");
        }

        // ---- what the clients see ----

        [Test]
        public void TheClientDrawsTheEvolutionExactlyAsTheServerJudgedIt()
        {
            foreach (SkillDef def in SkillCatalog.All.Where(d => d.EvolvesFrom != 0))
            {
                SkillActivation act = Evolved(def.Id, (SkillIds.WideWind, 3), (SkillIds.CritMagic, 3), (SkillIds.TimeRule, 3));
                Assert.AreEqual(def.Id, act.SkillId);
                Assert.AreEqual(act.Radius, SkillEffectSize.Radius(def, act.Rank, act.RangeScale), 1e-6f, def.Name);
                Assert.AreEqual(act.Length, SkillEffectSize.Length(def, act.Rank, act.RangeScale), 1e-6f, def.Name);
                Assert.AreEqual(act.HalfWidth * 2f, SkillEffectSize.Width(def, act.Rank, act.RangeScale), 1e-6f, def.Name);
                Assert.AreEqual(act.BuffSeconds, SkillEffectSize.Seconds(def, act.Rank, act.DurationScale), 1e-6f, def.Name);
            }
        }

        [Test]
        public void ASkillEventCarriesAnEvolutionIdThroughTheCodec()
        {
            foreach (SkillDef def in SkillCatalog.All.Where(d => d.EvolvesFrom != 0))
            {
                var events = new List<CombatEvent> { new CombatEvent { Kind = CombatEventKind.Skill, SkillId = def.Id, Slot = 1, Rank = 1, Angle = 777, RangeScale = 100, DurationScale = 100 } };
                var buffer = new byte[CombatCodec.MaxBatchBytes + 16];
                int length = CombatCodec.WriteBatch(buffer, 9u, events, 0, 1, null);
                int offset = CombatCodec.HeaderSize;

                Assert.IsTrue(CombatCodec.TryReadEvent(buffer, length, ref offset, out CombatEvent read));
                Assert.AreEqual(def.Id, read.SkillId, def.Name);
                Assert.AreEqual(1, read.Rank);
            }
        }
    }
}
