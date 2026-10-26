using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class BossKindTests
    {
        const float Dt = 0.02f;
        const ulong BossOwner = 1;

        [TearDown]
        public void RestoreTheStandardKind() => BossCatalog.SetCurrentKind(BossCatalog.Standard);

        // ---- 1.1 the data ----

        [Test]
        public void ThereAreThreeKindsWithTheirNumbers()
        {
            Assert.AreEqual(3, BossCatalog.All.Count);
            Assert.AreEqual(BossCatalog.Standard, BossCatalog.Get(BossCatalog.Standard).Id);
            Assert.AreEqual(BossCatalog.Giant, BossCatalog.Get(BossCatalog.Giant).Id);
            Assert.AreEqual(BossCatalog.Sniper, BossCatalog.Get(BossCatalog.Sniper).Id);
            Assert.AreEqual(1, BossCatalog.Standard);
            Assert.AreEqual(2, BossCatalog.Giant);
            Assert.AreEqual(3, BossCatalog.Sniper);
            Assert.AreSame(BossCatalog.Get(BossCatalog.Standard), BossCatalog.Default);
        }

        [Test]
        public void ANumberThatIsNoKindGivesTheStandardKind()
        {
            Assert.IsFalse(BossCatalog.IsValid(0));
            Assert.IsFalse(BossCatalog.IsValid(4));
            Assert.IsTrue(BossCatalog.IsValid(1));
            Assert.IsTrue(BossCatalog.IsValid(3));
            Assert.AreSame(BossCatalog.Default, BossCatalog.Get(0));
            Assert.AreSame(BossCatalog.Default, BossCatalog.Get(200));
        }

        [Test]
        public void EveryKindIsCompleteData()
        {
            foreach (BossDef kind in BossCatalog.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(kind.Name), $"kind {kind.Id}");
                Assert.Greater(kind.MaxHealth, 0, kind.Name);
                Assert.Greater(kind.MoveSpeed, 0f, kind.Name);
                Assert.Greater(kind.Radius, 0f, kind.Name);
                Assert.Greater(kind.ContactDamage, 0, kind.Name);
                Assert.Greater(kind.MatchSeconds, 0f, kind.Name);
                Assert.IsNotNull(kind.Attack, kind.Name);
                Assert.Greater(kind.Attack.BaseDamage, 0, kind.Name);
                Assert.Greater(kind.Attack.CooldownSeconds, 0f, kind.Name);
                Assert.Greater(kind.Attack.Shape.Speed, 0f, kind.Name);
                Assert.AreEqual(2, kind.Skills.Length, kind.Name);
                foreach (BossSkillDef skill in kind.Skills)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(skill.Name), kind.Name);
                    Assert.Greater(skill.CooldownSeconds, 0f, $"{kind.Name} {skill.Name}");
                    Assert.Greater(skill.WarningSeconds, 0f, $"{kind.Name} {skill.Name}");
                }
                Assert.AreEqual("Q", kind.Skills[0].Key);
                Assert.AreEqual("E", kind.Skills[1].Key);
            }
        }

        [Test]
        public void TheStandardKindKeepsItsSizeDamageAttackAndSkills()
        {
            // Health and speed were retuned for long matches (see TheKindsHaveTheTunedHealthAndSpeed); the rest is as it always was.
            BossDef k = BossCatalog.Get(BossCatalog.Standard);
            Assert.AreEqual(0.9f, k.Radius);
            Assert.AreEqual(25, k.ContactDamage);
            Assert.AreEqual(new Color(0.85f, 0.15f, 0.15f), k.Color);
            Assert.AreEqual(900f, k.MatchSeconds);
            Assert.AreEqual("보스", k.Name);

            Assert.AreEqual(15, k.Attack.BaseDamage);
            Assert.AreEqual(0.8f, k.Attack.CooldownSeconds);
            Assert.AreEqual(11f, k.Attack.Shape.Speed);
            Assert.AreEqual(1.6f, k.Attack.Shape.Lifetime);
            Assert.AreEqual(0.3f, k.Attack.Shape.Radius);
            Assert.AreEqual(1, k.Attack.Shape.Pierce);

            BossSkillDef q = k.Skills[0], e = k.Skills[1];
            Assert.AreEqual(BossSkillEffect.ShotRing, q.Effect);
            Assert.AreEqual(12, q.Count);
            Assert.AreEqual(10, q.Damage);
            Assert.AreEqual(10f, q.CooldownSeconds);
            Assert.AreEqual(0.8f, q.WarningSeconds);
            Assert.AreEqual(BossSkillEffect.Shockwave, e.Effect);
            Assert.AreEqual(4f, e.Radius);
            Assert.AreEqual(30, e.Damage);
            Assert.AreEqual(8f, e.CooldownSeconds);
            Assert.AreEqual(0.8f, e.WarningSeconds);
        }

        [Test]
        public void TheGiantIsBiggerSlowerAndHitsHarderAndTheSniperIsTheOpposite()
        {
            BossDef standard = BossCatalog.Get(BossCatalog.Standard);
            BossDef giant = BossCatalog.Get(BossCatalog.Giant);
            BossDef sniper = BossCatalog.Get(BossCatalog.Sniper);

            Assert.Greater(giant.MaxHealth, standard.MaxHealth);
            Assert.Less(giant.MoveSpeed, standard.MoveSpeed);
            Assert.Greater(giant.Radius, standard.Radius);
            Assert.Greater(giant.ContactDamage, standard.ContactDamage);

            Assert.Less(sniper.MaxHealth, standard.MaxHealth);
            Assert.Greater(sniper.MoveSpeed, standard.MoveSpeed);
            Assert.Less(sniper.Radius, standard.Radius);
            Assert.Less(sniper.Attack.CooldownSeconds, standard.Attack.CooldownSeconds, "fires its basic attack faster");

            Assert.AreEqual(3, new[] { standard.Name, giant.Name, sniper.Name }.Distinct().Count());
            Assert.AreEqual(3, new[] { standard.Color, giant.Color, sniper.Color }.Distinct().Count());
        }

        [Test]
        public void EveryKindsColorStaysDistinctFromEveryCharacter()
        {
            foreach (BossDef kind in BossCatalog.All)
                foreach (CharacterDef c in CharacterCatalog.All)
                    Assert.Greater(Mathf.Abs(c.Color.g - kind.Color.g) + Mathf.Abs(c.Color.b - kind.Color.b) + Mathf.Abs(c.Color.r - kind.Color.r), 0.3f, $"{kind.Name} and {c.Name}");
        }

        [Test]
        public void EachKindHasItsSkillEffectsOnQAndE()
        {
            BossDef standard = BossCatalog.Get(BossCatalog.Standard);
            BossDef giant = BossCatalog.Get(BossCatalog.Giant);
            BossDef sniper = BossCatalog.Get(BossCatalog.Sniper);

            CollectionAssert.AreEqual(new[] { BossSkillEffect.ShotRing, BossSkillEffect.Shockwave }, standard.Skills.Select(s => s.Effect).ToArray());
            CollectionAssert.AreEqual(new[] { BossSkillEffect.Shockwave, BossSkillEffect.Roar }, giant.Skills.Select(s => s.Effect).ToArray());
            CollectionAssert.AreEqual(new[] { BossSkillEffect.ShotRing, BossSkillEffect.Beam }, sniper.Skills.Select(s => s.Effect).ToArray());
            Assert.Greater(giant.Skills[1].Count, 0, "the roar summons some mobs");
            Assert.Greater(sniper.Skills[1].Length, 0f);
            Assert.Greater(sniper.Skills[1].HalfWidth, 0f);
            Assert.Greater(giant.Skills[0].Radius, standard.Skills[1].Radius, "the giant's shockwave is larger");
            Assert.Greater(sniper.Skills[0].Count, standard.Skills[0].Count, "the sniper's ring has more shots");
        }

        [Test]
        public void ForMatchGivesACopyOfTheRequestedKindAndTheCurrentOneWithoutAnArgument()
        {
            BossDef copy = BossCatalog.ForMatch(BossCatalog.Giant);
            Assert.AreEqual(BossCatalog.Giant, copy.Id);
            Assert.AreEqual(BossCatalog.Get(BossCatalog.Giant).MaxHealth, copy.MaxHealth);
            copy.MaxHealth = 1;
            Assert.AreNotEqual(1, BossCatalog.Get(BossCatalog.Giant).MaxHealth, "changing the copy never changes the shared data");

            BossCatalog.SetCurrentKind(BossCatalog.Sniper);
            Assert.AreEqual(BossCatalog.Sniper, BossCatalog.ForMatch().Id);
            Assert.AreEqual(BossCatalog.Sniper, BossCatalog.CurrentKind);
            BossCatalog.SetCurrentKind(99);
            Assert.AreEqual(BossCatalog.Standard, BossCatalog.CurrentKind, "a number that is no kind falls back to the standard kind");
        }

        // ---- 1.2 the code reads the kind, not constants ----

        [Test]
        public void TheCurrentKindDecidesTheBossSpeedAndTheShotDamage()
        {
            Assert.AreEqual(BossCatalog.Get(BossCatalog.Standard).MoveSpeed, CharacterCatalog.BossMoveSpeed);
            Assert.AreEqual(15, CombatServer.BossShotDamage);

            BossCatalog.SetCurrentKind(BossCatalog.Giant);
            Assert.AreEqual(BossCatalog.Get(BossCatalog.Giant).MoveSpeed, CharacterCatalog.BossMoveSpeed);
            Assert.AreEqual(20, CombatServer.BossShotDamage);

            BossCatalog.SetCurrentKind(BossCatalog.Sniper);
            Assert.AreEqual(BossCatalog.Get(BossCatalog.Sniper).MoveSpeed, CharacterCatalog.BossMoveSpeed);
            Assert.AreEqual(BossCatalog.Get(BossCatalog.Sniper).Attack.BaseDamage, CombatServer.BossShotDamage);
        }

        [Test]
        public void TheBasicAttackDefOfAKindCarriesItsCooldownAndShape()
        {
            foreach (BossDef kind in BossCatalog.All)
            {
                AttackDef def = kind.Attack.ToAttackDef();
                Assert.AreEqual(kind.Attack.CooldownSeconds, def.CooldownSeconds, kind.Name);
                Assert.AreEqual(1, def.Shots, kind.Name);
                Assert.AreEqual(kind.Attack.Shape.Speed, def.Shape.Speed, kind.Name);
                Assert.AreEqual(kind.Attack.Shape.Radius, def.Shape.Radius, kind.Name);
            }
        }

        [Test]
        public void EnrageStartsFromTheKindsOwnBaseDamage()
        {
            foreach (BossDef kind in BossCatalog.All)
            {
                Assert.AreEqual(kind.Attack.BaseDamage, BossAttack.Damage(kind.Attack.BaseDamage, 1f), kind.Name);
                Assert.Greater(BossAttack.Damage(kind.Attack.BaseDamage, 0f), kind.Attack.BaseDamage, kind.Name);
            }
        }

        // ---- the skill simulation of a kind ----

        sealed class Rig
        {
            public readonly BossSkillSim Sim = new BossSkillSim();
            public readonly ProjectileSim Projectiles = new ProjectileSim(512);
            public readonly SurvivorSim Survivors = new SurvivorSim();
            public readonly MobSim Mobs;
            public readonly List<SurvivorBody> Bodies = new List<SurvivorBody>();
            public readonly List<CombatEvent> Events = new List<CombatEvent>();
            public Vector2 BossPosition = Vector2.zero;
            public ushort Aim = AimMath.FromDirection(Vector2.right);
            public float HealthFraction = 1f;

            readonly SurvivorTargets targets;

            public Rig(byte kind, int mobCap = 100)
            {
                Sim.Reset(BossCatalog.Get(kind));
                Mobs = new MobSim(hardCap: mobCap, targetCount: 0);
                targets = new SurvivorTargets(Survivors, id => (byte)id);
                Survivors.InvulnerabilitySeconds = 0f;
            }

            public void AddSurvivor(ulong id, float x, float y, int health = 200)
            {
                Survivors.Register(id, health);
                Bodies.Add(new SurvivorBody { ClientId = id, Position = new Vector2(x, y) });
            }

            public BossSkillResult Use(int slot) => Sim.TryUse(slot, true, new BossSkillState
            {
                MatchRunning = true, BossStanding = true, BossPosition = BossPosition, Aim = Aim, HealthFraction = HealthFraction,
            }, BossOwner, Events);

            public void Step(float seconds)
            {
                int steps = Mathf.RoundToInt(seconds / Dt);
                for (int i = 0; i < steps; i++)
                {
                    Sim.Step(Dt, true, true, Projectiles, Survivors, Bodies, BossOwner, Events, Mobs);
                    targets.SetBodies(Bodies);
                    Projectiles.Step(Dt, null, null, targets);
                    Projectiles.DrainEvents(Events);
                }
            }
        }

        [Test]
        public void TheSkillSimUsesTheKindsCooldownWarningAndDamage()
        {
            foreach (BossDef kind in BossCatalog.All)
            {
                var rig = new Rig(kind.Id);
                for (int slot = 0; slot < 2; slot++)
                {
                    Assert.AreEqual(BossSkillResult.Used, rig.Use(slot), $"{kind.Name} slot {slot}");
                    Assert.AreEqual(kind.Skills[slot].CooldownSeconds, rig.Sim.CooldownLeft(slot), 1e-4f, $"{kind.Name} slot {slot}");
                    CombatEvent warning = rig.Events.Last(e => e.Kind == CombatEventKind.Skill);
                    Assert.AreEqual(BossSkills.WarningIdOf(kind.Skills[slot].Effect), warning.SkillId, $"{kind.Name} slot {slot}");
                }
                Assert.AreEqual(2, rig.Sim.PendingCount);
            }
        }

        [Test]
        public void TheGiantsShockwaveReachesFartherThanTheStandardOne()
        {
            // 5 units away: outside the standard radius (4), inside the giant's (5.5)
            var giant = new Rig(BossCatalog.Giant);
            giant.AddSurvivor(1, 5f, 0f);
            giant.Use(0);
            giant.Step(BossCatalog.Get(BossCatalog.Giant).Skills[0].WarningSeconds + 0.1f);
            Assert.AreEqual(200 - 40, giant.Survivors.Health(1));

            var standard = new Rig(BossCatalog.Standard);
            standard.AddSurvivor(1, 5f, 0f);
            standard.Use(1);
            standard.Step(BossCatalog.Get(BossCatalog.Standard).Skills[1].WarningSeconds + 0.1f);
            Assert.AreEqual(200, standard.Survivors.Health(1));
        }

        [Test]
        public void TheSnipersRingHasMoreShotsThanTheStandardRing()
        {
            var rig = new Rig(BossCatalog.Sniper);
            rig.Use(0);
            rig.Step(BossCatalog.Get(BossCatalog.Sniper).Skills[0].WarningSeconds + 0.02f);
            Assert.AreEqual(16, rig.Projectiles.Count);

            var standard = new Rig(BossCatalog.Standard);
            standard.Use(0);
            standard.Step(BossCatalog.Get(BossCatalog.Standard).Skills[0].WarningSeconds + 0.02f);
            Assert.AreEqual(12, standard.Projectiles.Count);
        }

        // ---- 2.1 the beam ----

        BossSkillDef BeamDef => BossCatalog.Get(BossCatalog.Sniper).Skills[1];
        float BeamWait => BeamDef.WarningSeconds + 0.05f;

        [Test]
        public void AFirstSurvivorOnTheLineIsHurtAndOneBesideItIsNot()
        {
            var rig = new Rig(BossCatalog.Sniper);
            rig.AddSurvivor(1, 10f, 0f);
            rig.AddSurvivor(2, 10f, 3f);
            rig.Use(1);
            rig.Step(BeamWait);

            Assert.AreEqual(200 - BeamDef.Damage, rig.Survivors.Health(1));
            Assert.AreEqual(200, rig.Survivors.Health(2));
        }

        [Test]
        public void TheBeamReachesItsLengthAndNoFartherAndNeverBehindTheBoss()
        {
            var rig = new Rig(BossCatalog.Sniper);
            rig.AddSurvivor(1, BeamDef.Length - 0.5f, 0f);
            rig.AddSurvivor(2, BeamDef.Length + 2f, 0f);
            rig.AddSurvivor(3, -4f, 0f);
            rig.Use(1);
            rig.Step(BeamWait);

            Assert.AreEqual(200 - BeamDef.Damage, rig.Survivors.Health(1));
            Assert.AreEqual(200, rig.Survivors.Health(2), "beyond the length");
            Assert.AreEqual(200, rig.Survivors.Health(3), "behind the boss");
        }

        [Test]
        public void TheBeamsWidthIncludesTheSurvivorsBody()
        {
            var rig = new Rig(BossCatalog.Sniper);
            float edge = BeamDef.HalfWidth + rig.Survivors.Radius;
            rig.AddSurvivor(1, 8f, edge - 0.05f);
            rig.AddSurvivor(2, 8f, edge + 0.1f);
            rig.Use(1);
            rig.Step(BeamWait);

            Assert.AreEqual(200 - BeamDef.Damage, rig.Survivors.Health(1), "just touching");
            Assert.AreEqual(200, rig.Survivors.Health(2), "just clear");
        }

        [Test]
        public void ABeamHurtsEachSurvivorOnceEvenIfTheyStandInALine()
        {
            var rig = new Rig(BossCatalog.Sniper);
            rig.AddSurvivor(1, 6f, 0f);
            rig.AddSurvivor(2, 12f, 0f);
            rig.Use(1);
            rig.Step(BeamWait + 1f);

            Assert.AreEqual(200 - BeamDef.Damage, rig.Survivors.Health(1));
            Assert.AreEqual(200 - BeamDef.Damage, rig.Survivors.Health(2), "the beam is not stopped by the first survivor");
        }

        [Test]
        public void AProtectedOrFallenSurvivorOnTheLineTakesNothing()
        {
            var rig = new Rig(BossCatalog.Sniper);
            rig.AddSurvivor(1, 6f, 0f);
            rig.AddSurvivor(2, 9f, 0f);
            rig.Survivors.InvulnerabilitySeconds = 5f;
            rig.Survivors.ApplyDamage(1, 1); // a hit starts the protection time
            int before = rig.Survivors.Health(1);
            rig.Survivors.Kill(2);
            rig.Use(1);
            rig.Step(BeamWait);

            Assert.AreEqual(before, rig.Survivors.Health(1), "protected");
            Assert.AreEqual(0, rig.Survivors.Health(2), "fallen");
        }

        [Test]
        public void TheBeamKeepsTheAimOfTheMomentItWasUsed()
        {
            var rig = new Rig(BossCatalog.Sniper);
            rig.AddSurvivor(1, 10f, 0f);
            rig.AddSurvivor(2, 0f, 10f);
            rig.Aim = AimMath.FromDirection(Vector2.right);
            rig.Use(1);
            rig.Aim = AimMath.FromDirection(Vector2.up); // the boss turns while the warning runs
            rig.Step(BeamWait);

            Assert.AreEqual(200 - BeamDef.Damage, rig.Survivors.Health(1));
            Assert.AreEqual(200, rig.Survivors.Health(2));
        }

        [Test]
        public void TheBeamKeepsThePlaceOfTheMomentItWasUsed()
        {
            var rig = new Rig(BossCatalog.Sniper);
            rig.AddSurvivor(1, 12f, 0f);
            rig.BossPosition = new Vector2(10f, 0f);
            rig.Use(1);
            rig.BossPosition = new Vector2(-10f, 0f); // the boss walks away
            rig.Step(BeamWait);

            Assert.AreEqual(200 - BeamDef.Damage, rig.Survivors.Health(1), "the beam started at (10, 0)");
        }

        [Test]
        public void TheBeamDoesNotHurtMobs()
        {
            var rig = new Rig(BossCatalog.Sniper);
            rig.Mobs.TrySpawn(new Vector2(8f, 0f), MobKinds.Encode(MobKinds.OriginAuto, MobKinds.Basic), out ushort id);
            int before = rig.Mobs.Count;
            rig.AddSurvivor(1, 9f, 0f);
            rig.Use(1);
            rig.Step(BeamWait);

            Assert.AreEqual(before, rig.Mobs.Count);
            Assert.IsTrue(rig.Mobs.Contains(id));
        }

        [Test]
        public void ABeamOfAWeakBossHitsHarder()
        {
            var rig = new Rig(BossCatalog.Sniper) { HealthFraction = 0f };
            rig.AddSurvivor(1, 6f, 0f);
            rig.Use(1);
            rig.Step(BeamWait);

            Assert.AreEqual(200 - BossAttack.Damage(BeamDef.Damage, 0f), rig.Survivors.Health(1));
            Assert.Greater(BossAttack.Damage(BeamDef.Damage, 0f), BeamDef.Damage);
        }

        // ---- 2.2 the roar ----

        BossSkillDef RoarDef => BossCatalog.Get(BossCatalog.Giant).Skills[1];

        [Test]
        public void TheRoarCreatesItsMobsAroundTheBossAfterTheWarning()
        {
            var rig = new Rig(BossCatalog.Giant);
            rig.BossPosition = new Vector2(3f, -2f);
            rig.Use(1);
            rig.Step(RoarDef.WarningSeconds - 0.1f);
            Assert.AreEqual(0, rig.Mobs.Count, "nothing before the warning has passed");

            rig.Step(0.2f);

            Assert.AreEqual(RoarDef.Count, rig.Mobs.Count);
            for (int i = 0; i < rig.Mobs.Count; i++)
                Assert.AreEqual(BossSkillSim.RoarDistance, Vector2.Distance(rig.Mobs.PositionAt(i), rig.BossPosition), 0.05f, $"mob {i}");
            var spots = Enumerable.Range(0, rig.Mobs.Count).Select(i => rig.Mobs.PositionAt(i)).ToList();
            Assert.AreEqual(spots.Count, spots.Distinct().Count(), "different places around the boss");
        }

        [Test]
        public void TheRoarsMobsAreSummonedMobs()
        {
            var rig = new Rig(BossCatalog.Giant);
            rig.Use(1);
            rig.Step(RoarDef.WarningSeconds + 0.1f);

            Assert.AreEqual(RoarDef.Count, rig.Mobs.SummonedCount);
            for (int i = 0; i < rig.Mobs.Count; i++) Assert.IsTrue(rig.Mobs.IsSummoned(i));
        }

        [Test]
        public void TheMobCapLimitsTheRoar()
        {
            var rig = new Rig(BossCatalog.Giant, mobCap: 4);
            rig.Use(1);
            rig.Step(RoarDef.WarningSeconds + 0.1f);
            Assert.AreEqual(4, rig.Mobs.Count, "only the mobs that fit");
            Assert.Greater(rig.Sim.CooldownLeft(1), 0f, "the cooldown started anyway");

            var full = new Rig(BossCatalog.Giant, mobCap: 0);
            full.Use(1);
            full.Step(RoarDef.WarningSeconds + 0.1f);
            Assert.AreEqual(0, full.Mobs.Count, "no room at all");
        }

        [Test]
        public void TheRoarNeedsNoSummonEnergyAndWorksWithoutAMobSimulationToo()
        {
            var sim = new SummonSim();
            float energy = sim.Energy;
            var rig = new Rig(BossCatalog.Giant);
            rig.Use(1);
            rig.Step(RoarDef.WarningSeconds + 0.1f);
            Assert.AreEqual(energy, sim.Energy, "the roar never touches the summoning");

            // a step with no mob simulation (an old caller) does nothing instead of failing
            var bare = new BossSkillSim();
            bare.Reset(BossCatalog.Get(BossCatalog.Giant));
            var events = new List<CombatEvent>();
            bare.TryUse(1, true, new BossSkillState { MatchRunning = true, BossStanding = true, HealthFraction = 1f }, BossOwner, events);
            Assert.DoesNotThrow(() => bare.Step(2f, true, true, new ProjectileSim(8), new SurvivorSim(), new List<SurvivorBody>(), BossOwner, events));
        }

        // ---- 2.3 the warnings are drawn from the event and the kind ----

        [Test]
        public void EveryEffectHasItsOwnWarningNumberThatMapsBack()
        {
            var seen = new HashSet<byte>();
            foreach (BossSkillEffect effect in System.Enum.GetValues(typeof(BossSkillEffect)))
            {
                byte id = BossSkills.WarningIdOf(effect);
                Assert.IsTrue(seen.Add(id), $"{effect} shares a number");
                Assert.IsTrue(BossSkills.IsWarningId(id), effect.ToString());
                Assert.AreEqual(effect, BossSkills.EffectOfWarning(id));
            }
            CollectionAssert.AreEquivalent(new byte[] { 200, 201, 202, 203 }, seen.ToArray());
            Assert.IsFalse(BossSkills.IsWarningId(199));
            Assert.IsFalse(BossSkills.IsWarningId(204));
            foreach (SkillDef def in SkillCatalog.All) Assert.IsFalse(BossSkills.IsWarningId(def.Id), def.Name);
        }

        [Test]
        public void AClientCanFindTheValuesItDrawsFromTheKindAndTheEffect()
        {
            BossDef sniper = BossCatalog.Get(BossCatalog.Sniper);
            BossSkillDef beam = sniper.SkillWithEffect(BossSkills.EffectOfWarning(BossSkills.WarningIdBeam));
            Assert.IsNotNull(beam);
            Assert.AreEqual(24f, beam.Length);
            Assert.AreEqual(0.5f, beam.HalfWidth);
            Assert.AreEqual(1.5f, beam.WarningSeconds);

            BossDef giant = BossCatalog.Get(BossCatalog.Giant);
            Assert.AreEqual(5.5f, giant.SkillWithEffect(BossSkillEffect.Shockwave).Radius);
            Assert.IsNull(giant.SkillWithEffect(BossSkillEffect.Beam), "the giant has no beam to draw");
            Assert.AreSame(sniper.PhaseBurst, sniper.SkillWithEffect(BossSkillEffect.Roar), "the sniper only roars at a phase change, and the client draws that burst from this definition");
        }

        [Test]
        public void TheWarningEventOfEachEffectCarriesThePlaceAndTheAim()
        {
            foreach (BossDef kind in BossCatalog.All)
            {
                var rig = new Rig(kind.Id);
                rig.BossPosition = new Vector2(2f, 3f);
                rig.Aim = 12345;
                for (int slot = 0; slot < 2; slot++)
                {
                    rig.Events.Clear();
                    rig.Sim.Reset(kind);
                    rig.Use(slot);
                    CombatEvent e = rig.Events.Single(x => x.Kind == CombatEventKind.Skill);
                    Assert.AreEqual(new Vector2(2f, 3f), e.Position, $"{kind.Name} slot {slot}");
                    Assert.AreEqual(12345, e.Angle, $"{kind.Name} slot {slot}");
                    Assert.AreEqual(BossSkills.WarningIdOf(kind.Skills[slot].Effect), e.SkillId);
                }
            }
        }
    }
}
