using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class SkillEvolutionSlotEffectTests
    {
        const ulong A = 1, B = 2;

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

        static SkillActivation Evolved(byte evolution, SkillSim sim = null, params (byte id, int rank)[] more)
        {
            SkillDef def = SkillCatalog.Get(evolution);
            var set = new List<(byte, int)> { (def.EvolvesFrom, 3), (evolution, 1) };
            set.AddRange(more);
            Assert.AreEqual(SkillResult.Fired, (sim ?? NewSkills()).TryActivate(A, def.Character, def.Slot, true, Ranks(set.ToArray()), out SkillActivation act));
            return act;
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

        static IEnumerable<SkillDef> NewEvolutions() => SkillCatalog.All.Where(d => d.EvolvesFrom != 0 && d.Slot >= 1);

        // ---- the numbers of every evolution reach the activation ----

        [Test]
        public void EveryNewEvolutionFiresWithItsOwnCostCooldownAndValues()
        {
            foreach (SkillDef def in NewEvolutions())
            {
                SkillSim sim = NewSkills();
                SkillActivation act = Evolved(def.Id, sim);

                Assert.AreEqual(def.Id, act.SkillId, def.Name);
                Assert.AreEqual(1, act.Rank, def.Name);
                Assert.AreEqual(1000f - def.ManaCost, sim.Mana(A), 1e-3f, def.Name);
                Assert.AreEqual(def.Cooldown, sim.CooldownLeft(A, def.Slot), 1e-3f, def.Name);
                if (def.Radius != null) Assert.AreEqual(def.Radius[0], act.Radius, 1e-4f, def.Name);
                if (def.Length != null) Assert.AreEqual(def.Length[0], act.Length, 1e-4f, def.Name);
                if (def.HalfWidth != null) Assert.AreEqual(def.HalfWidth[0], act.HalfWidth, 1e-4f, def.Name);
                if (def.Distance != null) Assert.AreEqual(def.Distance[0], act.Distance, 1e-4f, def.Name);
                if (def.Duration != null) Assert.AreEqual(def.Duration[0], act.BuffSeconds, 1e-4f, def.Name);
                if (def.Count != null) Assert.AreEqual(def.Count[0], act.Count, def.Name);
                if (def.Damage != null) Assert.AreEqual(def.Damage[0], act.Damage, def.Name);
                if (def.Amount != null) Assert.AreEqual(def.Amount[0], act.Amount, def.Name);
                if (def.CooldownMultiplier != null) Assert.AreEqual(def.CooldownMultiplier[0], act.CooldownMultiplier, 1e-4f, def.Name);
            }
        }

        [Test]
        public void TheClientDrawsEveryNewEvolutionExactlyAsTheServerJudgedIt()
        {
            foreach (SkillDef def in NewEvolutions())
            {
                SkillActivation act = Evolved(def.Id, null, (SkillIds.WideWind, 3), (SkillIds.CritMagic, 3), (SkillIds.BarrierExtension, 3));
                Assert.AreEqual(act.Radius, SkillEffectSize.Radius(def, act.Rank, act.RangeScale), 1e-6f, def.Name);
                Assert.AreEqual(act.Length, SkillEffectSize.Length(def, act.Rank, act.RangeScale), 1e-6f, def.Name);
                Assert.AreEqual(act.HalfWidth * 2f, SkillEffectSize.Width(def, act.Rank, act.RangeScale), 1e-6f, def.Name);
                Assert.AreEqual(act.BuffSeconds, SkillEffectSize.Seconds(def, act.Rank, act.DurationScale), 1e-6f, def.Name);
            }
        }

        [Test]
        public void ASkillEventCarriesEveryNewEvolutionIdThroughTheCodec()
        {
            foreach (SkillDef def in NewEvolutions())
            {
                var events = new List<CombatEvent> { new CombatEvent { Kind = CombatEventKind.Skill, SkillId = def.Id, Slot = 2, Rank = 1, Angle = 321, RangeScale = 100, DurationScale = 100 } };
                var buffer = new byte[CombatCodec.MaxBatchBytes + 16];
                int length = CombatCodec.WriteBatch(buffer, 9u, events, 0, 1, null);
                int offset = CombatCodec.HeaderSize;

                Assert.IsTrue(CombatCodec.TryReadEvent(buffer, length, ref offset, out CombatEvent read), def.Name);
                Assert.AreEqual(def.Id, read.SkillId, def.Name);
            }
        }

        // ---- what each effect does ----

        [Test]
        public void ReimusBarrierEvolutionsProtectForTheirDurations()
        {
            foreach (byte id in new[] { SkillIds.BarrierLong, SkillIds.BarrierShort })
            {
                SkillActivation act = Evolved(id);
                var survivors = new SurvivorSim();
                survivors.Register(A);

                int touched = SkillExecutor.Apply(act, new SkillContext { CasterId = A, Survivors = survivors });

                Assert.AreEqual(1, touched);
                Assert.IsTrue(survivors.IsInvulnerable(A));
                Assert.AreEqual(SkillCatalog.Get(id).Duration[0], act.BuffSeconds, 1e-4f);
            }
            Assert.Greater(SkillCatalog.Get(SkillIds.BarrierLong).Duration[0], SkillCatalog.Get(SkillIds.BarrierShort).Duration[0]);
        }

        [Test]
        public void MarisasStardustEvolutionsBoostTheAttackForTheirOwnTimeAndStrength()
        {
            foreach (byte id in new[] { SkillIds.StardustLong, SkillIds.StardustRapid })
            {
                SkillSim sim = NewSkills();
                SkillActivation act = Evolved(id, sim);
                SkillDef def = SkillCatalog.Get(id);

                Assert.AreEqual(def.CooldownMultiplier[0], sim.AttackMultiplier(A), 1e-4f, def.Name);
                Assert.AreEqual(def.Duration[0], sim.BuffLeft(A), 1e-3f, def.Name);
                Assert.AreEqual(0, SkillExecutor.Apply(act, new SkillContext { CasterId = A }), "it touches nothing else");
            }
            Assert.Less(SkillCatalog.Get(SkillIds.StardustRapid).CooldownMultiplier[0], SkillCatalog.Get(SkillIds.StardustLong).CooldownMultiplier[0], "the rapid one is the stronger boost");
            Assert.Less(SkillCatalog.Get(SkillIds.StardustRapid).Duration[0], SkillCatalog.Get(SkillIds.StardustLong).Duration[0]);
        }

        [Test]
        public void SakuyasKnifeEvolutionsFireTheirOwnNumberOfKnivesForTheirOwnDamage()
        {
            ProjectileShape shape = CharacterCatalog.Get(CharacterIds.Sakuya).Attack.Shape;
            foreach (byte id in new[] { SkillIds.KnivesMany, SkillIds.KnivesFocused, SkillIds.DollDense, SkillIds.DollPrecise })
            {
                SkillDef def = SkillCatalog.Get(id);
                SkillActivation act = Evolved(id);
                var projectiles = new ProjectileSim();

                int fired = SkillExecutor.Apply(act, new SkillContext { CasterId = A, Origin = Vector2.zero, AimAngle = 0, Projectiles = projectiles, Shape = shape });

                Assert.AreEqual(def.Count[0], fired, def.Name);
                Assert.AreEqual(def.Damage[0], act.Damage, def.Name);
                var events = new List<CombatEvent>();
                projectiles.DrainEvents(events);
                Assert.AreEqual(def.Count[0], events.Count(e => e.Kind == CombatEventKind.Fire), def.Name);
            }
        }

        [Test]
        public void TheDenseRingIsTheBiggestRingAndFitsTheProjectileCap()
        {
            SkillDef dense = SkillCatalog.Get(SkillIds.DollDense);
            Assert.Greater(dense.Count[0], SkillCatalog.Get(SkillIds.KillingDoll).Count[2], "denser than the base R ring");
            Assert.Less(dense.Count[0], CombatConfig.ProjectileCap);
            var projectiles = new ProjectileSim();
            SkillActivation act = Evolved(SkillIds.DollDense);

            int fired = SkillExecutor.Apply(act, new SkillContext { CasterId = A, Projectiles = projectiles, Shape = ProjectileShape.Default });

            Assert.AreEqual(dense.Count[0], fired);
            Assert.AreEqual(dense.Count[0], projectiles.Count);
        }

        [Test]
        public void SanaesWindVeilEvolutionsPushWithTheirOwnReachAndDistance()
        {
            SkillActivation wide = Evolved(SkillIds.VeilWide), impact = Evolved(SkillIds.VeilImpact);
            MobSim viaWide = Mobs(), viaImpact = Mobs();
            ushort w5 = Spawn(viaWide, 5, 0), i5 = Spawn(viaImpact, 5, 0), i2 = Spawn(viaImpact, 2, 0);
            Vector2 w5Before = PositionOf(viaWide, w5), i2Before = PositionOf(viaImpact, i2);

            int pushedWide = SkillExecutor.Apply(wide, new SkillContext { CasterId = A, Origin = Vector2.zero, Mobs = viaWide });
            int pushedImpact = SkillExecutor.Apply(impact, new SkillContext { CasterId = A, Origin = Vector2.zero, Mobs = viaImpact });

            Assert.AreEqual(1, pushedWide, "5 away is inside the wide radius");
            Assert.AreEqual(1, pushedImpact, "only the one at 2 is inside the impact radius");
            Assert.AreEqual(w5Before.x + wide.Distance, PositionOf(viaWide, w5).x, 0.01f);
            Assert.AreEqual(i2Before.x + impact.Distance, PositionOf(viaImpact, i2).x, 0.01f);
            Assert.AreEqual(5f, PositionOf(viaImpact, i5).x, 0.01f, "outside the impact radius");
            Assert.Greater(impact.Distance, wide.Distance);
        }

        static Vector2 PositionOf(MobSim mobs, ushort id)
        {
            for (int i = 0; i < mobs.Count; i++) if (mobs.IdAt(i) == id) return mobs.PositionAt(i);
            Assert.Fail("mob missing");
            return default;
        }

        [Test]
        public void ReimusBurstEvolutionsReachAndHitAsTheirNumbersSay()
        {
            SkillActivation wide = Evolved(SkillIds.HeavenWide), focus = Evolved(SkillIds.HeavenFocus);
            MobSim viaWide = Mobs(), viaFocus = Mobs();
            ushort w9 = Spawn(viaWide, wide.Radius - 0.5f, 0), w11 = Spawn(viaWide, wide.Radius + 2f, 0);
            ushort f3 = Spawn(viaFocus, focus.Radius - 0.5f, 0), f6 = Spawn(viaFocus, focus.Radius + 2f, 0);

            SkillExecutor.Apply(wide, new SkillContext { CasterId = A, Mobs = viaWide });
            SkillExecutor.Apply(focus, new SkillContext { CasterId = A, Mobs = viaFocus });

            Assert.AreEqual(1000 - wide.Damage, Health(viaWide, w9));
            Assert.AreEqual(1000, Health(viaWide, w11), "beyond the wide burst");
            Assert.AreEqual(1000 - focus.Damage, Health(viaFocus, f3));
            Assert.AreEqual(1000, Health(viaFocus, f6), "beyond the focused burst");
            Assert.Greater(focus.Damage, wide.Damage);
        }

        [Test]
        public void MarisasBeamEvolutionsReachAndWidenAsTheirNumbersSay()
        {
            SkillActivation broad = Evolved(SkillIds.SparkBroad), piercing = Evolved(SkillIds.SparkPiercing);
            Vector2 origin = new Vector2(-16f, 0f);
            MobSim viaBroad = Mobs(), viaPiercing = Mobs();
            ushort bSide = Spawn(viaBroad, origin.x + 5f, (broad.HalfWidth + 1.0f * 0f) - 0.2f), bBeyond = Spawn(viaBroad, origin.x + broad.Length + 2f, 0f);
            ushort pSide = Spawn(viaPiercing, origin.x + 5f, piercing.HalfWidth + 0.6f), pFar = Spawn(viaPiercing, origin.x + piercing.Length - 1f, 0f);

            SkillExecutor.Apply(broad, new SkillContext { CasterId = A, Origin = origin, AimAngle = 0, Mobs = viaBroad });
            SkillExecutor.Apply(piercing, new SkillContext { CasterId = A, Origin = origin, AimAngle = 0, Mobs = viaPiercing });

            Assert.AreEqual(1000 - broad.Damage, Health(viaBroad, bSide), "inside the broad beam's width");
            Assert.AreEqual(1000, Health(viaBroad, bBeyond), "beyond the broad beam's length");
            Assert.AreEqual(1000, Health(viaPiercing, pSide), "beside the piercing beam");
            Assert.AreEqual(1000 - piercing.Damage, Health(viaPiercing, pFar), "far down the piercing beam");
        }

        static SurvivorSim Hurt(params ulong[] ids)
        {
            var survivors = new SurvivorSim();
            foreach (ulong id in ids) { survivors.Register(id, 400); survivors.ApplyDamage(id, 350); }
            return survivors;
        }

        [Test]
        public void SanaesGreatMiracleEvolutionsHealWithTheirOwnReachAndAmount()
        {
            SkillActivation wide = Evolved(SkillIds.GreatWide), quick = Evolved(SkillIds.GreatQuick);
            var bodies = new List<GrowthBody>
            {
                new GrowthBody { ClientId = A, Position = Vector2.zero, Alive = true },
                new GrowthBody { ClientId = B, Position = new Vector2(10f, 0), Alive = true },
            };

            SurvivorSim viaWide = Hurt(A, B), viaQuick = Hurt(A, B);
            SkillExecutor.Apply(wide, new SkillContext { CasterId = A, Survivors = viaWide, Bodies = bodies });
            SkillExecutor.Apply(quick, new SkillContext { CasterId = A, Survivors = viaQuick, Bodies = bodies });

            Assert.AreEqual(50 + wide.Amount, viaWide.Health(B), "10 away is inside the wide radius");
            Assert.AreEqual(50, viaQuick.Health(B), "and outside the quick one's");
            Assert.AreEqual(50 + quick.Amount, viaQuick.Health(A));
            Assert.Less(quick.Amount, SkillCatalog.Get(SkillIds.GreatMiracle).Amount[2]);
        }

        // ---- the rules every skill follows ----

        [Test]
        public void ThePassivesApplyToTheNewEvolutionsLikeToTheBaseSkills()
        {
            SkillActivation plain = Evolved(SkillIds.SparkPiercing);
            SkillActivation boosted = Evolved(SkillIds.SparkPiercing, null, (SkillIds.FirePower, 3), (SkillIds.CritMagic, 3));
            Assert.Greater(boosted.Damage, plain.Damage);
            Assert.Greater(boosted.Length, plain.Length);

            SkillDef focus = SkillCatalog.Get(SkillIds.HeavenFocus);
            int[] saving = Ranks((SkillIds.FantasyHeaven, 3), (SkillIds.HeavenFocus, 1), (SkillIds.SpiritSaving, 3));
            SkillSim sim = NewSkills();
            sim.TryActivate(A, CharacterIds.Reimu, SkillCatalog.UltimateSlot, true, saving, out _);
            Assert.AreEqual(1000f - SkillCatalog.EffectiveManaCost(focus, saving, CharacterIds.Reimu), sim.Mana(A), 1e-3f);
            Assert.Less(SkillCatalog.EffectiveManaCost(focus, saving, CharacterIds.Reimu), focus.ManaCost);

            SkillActivation longer = Evolved(SkillIds.BarrierShort, null, (SkillIds.BarrierExtension, 3));
            Assert.Greater(longer.BuffSeconds, SkillCatalog.Get(SkillIds.BarrierShort).Duration[0]);
        }

        [Test]
        public void TheSameRefusalsAndSeparateCooldownsApplyToTheNewEvolutions()
        {
            SkillDef evolution = SkillCatalog.Get(SkillIds.DollPrecise);
            int[] ranks = Ranks((SkillIds.KillingDoll, 3), (SkillIds.DollPrecise, 1), (SkillIds.TimeStop, 3), (SkillIds.KnifeBarrage, 3));

            SkillSim poor = NewSkills(evolution.ManaCost - 1);
            Assert.AreEqual(SkillResult.NoMana, poor.TryActivate(A, CharacterIds.Sakuya, SkillCatalog.UltimateSlot, true, ranks, out _));
            Assert.AreEqual(evolution.ManaCost - 1, poor.Mana(A), 1e-4f);

            SkillSim dead = NewSkills();
            Assert.AreEqual(SkillResult.Dead, dead.TryActivate(A, CharacterIds.Sakuya, SkillCatalog.UltimateSlot, false, ranks, out _));

            SkillSim sim = NewSkills();
            Assert.AreEqual(SkillResult.Fired, sim.TryActivate(A, CharacterIds.Sakuya, SkillCatalog.UltimateSlot, true, ranks, out _));
            Assert.AreEqual(SkillResult.Cooldown, sim.TryActivate(A, CharacterIds.Sakuya, SkillCatalog.UltimateSlot, true, ranks, out _));
            Assert.AreEqual(SkillResult.Fired, sim.TryActivate(A, CharacterIds.Sakuya, 0, true, ranks, out SkillActivation q), "Q has its own cooldown");
            Assert.AreEqual(SkillResult.Fired, sim.TryActivate(A, CharacterIds.Sakuya, 1, true, ranks, out SkillActivation e), "E has its own cooldown");
            Assert.AreEqual(SkillIds.TimeStop, q.SkillId);
            Assert.AreEqual(SkillIds.KnifeBarrage, e.SkillId);
        }
    }
}
