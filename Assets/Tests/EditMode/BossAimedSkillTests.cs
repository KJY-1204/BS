using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class BossAimedSkillTests
    {
        const float Dt = 0.02f;

        static HarnessSettings Settings(byte kind, params (string name, float value)[] overrides)
        {
            var s = new HarnessSettings { bossKind = kind };
            s.overrides.Add(new NamedValue { name = "mobAutoTarget", value = 0f });
            s.overrides.Add(new NamedValue { name = "matchSeconds", value = 120f });
            s.overrides.Add(new NamedValue { name = "survivorHealthBonus", value = 900f });
            foreach ((string name, float value) o in overrides) s.overrides.Add(new NamedValue { name = o.name, value = o.value });
            return s;
        }

        /// <summary>A match with the opening cooldown of the giant's leap strike already run down.</summary>
        static MatchSession Start(byte kind)
        {
            MatchSession s = MatchSession.Start(Settings(kind), 1);
            for (int i = 0; i < Mathf.RoundToInt((BossCatalog.Get(kind).Skills[0].OpeningCooldownSeconds + 1f) / Dt); i++) s.Advance();
            return s;
        }

        [TearDown]
        public void RestoreTheStandardKind() => BossCatalog.SetCurrentKind(BossCatalog.Standard);

        static void Teleport(MatchSession s, ulong id, Vector2 position)
        {
            for (int i = 0; i < s.World.Bodies.Count; i++)
            {
                MatchBody b = s.World.Bodies[i];
                if (b.ClientId == id) { b.Position = position; s.World.Bodies[i] = b; }
            }
        }

        static void ParkOthers(MatchSession s, params ulong[] keep)
        {
            Vector2 far = new Vector2(-15f, -9f);
            foreach (ulong id in s.SurvivorIds)
            {
                if (System.Array.IndexOf(keep, id) >= 0) continue;
                Teleport(s, id, far);
                far += new Vector2(1.2f, 0f);
            }
        }

        static CombatEvent Use(MatchSession s, int slot, ushort aim, out BossSkillResult result)
        {
            var events = new List<CombatEvent>();
            result = s.World.UseBossSkill(MatchSession.BossId, slot, aim, events);
            return events.FirstOrDefault(e => e.Kind == CombatEventKind.Skill);
        }

        static void Wait(MatchSession s, float seconds)
        {
            for (int i = 0; i < Mathf.RoundToInt(seconds / Dt); i++) s.Advance();
        }

        // ---- the aimed skills ----

        [Test]
        public void TheGiantsLeapStrikeIsAnnouncedOnTheNearestSurvivorAndHurtsThereAfterTheWarning()
        {
            MatchSession s = Start(BossCatalog.Giant);
            Vector2 boss = s.PositionOf(MatchSession.BossId);
            ParkOthers(s, 1);
            Vector2 target = boss + new Vector2(0f, -8f);
            Teleport(s, 1, target);
            BossSkillDef leap = BossCatalog.Get(BossCatalog.Giant).Skills[0];
            Assert.IsTrue(leap.AtTarget);

            CombatEvent announced = Use(s, 0, 0, out BossSkillResult result);

            Assert.AreEqual(BossSkillResult.Used, result);
            Assert.AreEqual(target, announced.Position, "announced where the survivor stands, not at the boss");
            int before = s.World.Survivors.Health(1);

            // The survivor walks away during the warning; the strike still lands at the announced place.
            Teleport(s, 1, target + new Vector2(12f, 0f));
            Wait(s, leap.WarningSeconds + 0.2f);
            Assert.AreEqual(before, s.World.Survivors.Health(1), "it was not there any more");
        }

        [Test]
        public void ASurvivorStillAtTheAnnouncedPlaceIsHurtByTheLeapStrike()
        {
            MatchSession s = Start(BossCatalog.Giant);
            Vector2 boss = s.PositionOf(MatchSession.BossId);
            ParkOthers(s, 1);
            Teleport(s, 1, boss + new Vector2(0f, -8f));
            BossSkillDef leap = BossCatalog.Get(BossCatalog.Giant).Skills[0];
            int before = s.World.Survivors.Health(1);

            Use(s, 0, 0, out _);
            Wait(s, leap.WarningSeconds + 0.2f);

            Assert.Less(s.World.Survivors.Health(1), before);
        }

        [Test]
        public void TheSnipersTargetedShotAimsItsBeamAtTheNearestSurvivorWhateverTheBossPlayerAimsAt()
        {
            MatchSession s = Start(BossCatalog.Sniper);
            Vector2 boss = s.PositionOf(MatchSession.BossId);
            ParkOthers(s, 1);
            Teleport(s, 1, boss + new Vector2(0f, -10f)); // straight down

            CombatEvent announced = Use(s, 1, AimMath.QuantizeAngle(0f), out BossSkillResult result); // the player aims along +x

            Assert.AreEqual(BossSkillResult.Used, result);
            Assert.AreEqual(boss, announced.Position, "a beam still starts at the boss");
            float degrees = AimMath.DequantizeAngle(announced.Angle) * Mathf.Rad2Deg;
            Assert.AreEqual(270f, degrees, 1f, "toward the survivor, straight down");
        }

        [Test]
        public void TheStandardRallyMakesItsMobsAroundTheNearestSurvivor()
        {
            MatchSession s = Start(BossCatalog.Standard);
            Vector2 boss = s.PositionOf(MatchSession.BossId);
            ParkOthers(s, 1);
            Vector2 target = boss + new Vector2(0f, -9f);
            Teleport(s, 1, target);
            BossSkillDef rally = BossCatalog.Get(BossCatalog.Standard).PhaseSkill;
            s.World.BossSkills.UpdatePhase(0.5f, true, true, boss, MatchSession.BossId, new List<CombatEvent>()); // phase 2 opens the third skill
            for (int i = 0; i < 80; i++) s.Advance();
            s.World.Mobs.Clear();

            CombatEvent announced = Use(s, BossSkills.PhaseSlot, 0, out BossSkillResult result);

            Assert.AreEqual(BossSkillResult.Used, result);
            Assert.AreEqual(target, announced.Position);
            Wait(s, rally.WarningSeconds + 0.2f);
            Assert.GreaterOrEqual(s.World.Mobs.Count, 1, "the rally's mobs appear");
            for (int i = 0; i < s.World.Mobs.Count; i++)
                Assert.Less(Vector2.Distance(s.World.Mobs.PositionAt(i), target), BossSkillSim.RoarDistance + 4f, "around the survivor, not around the boss");
        }

        [Test]
        public void TheNearestOfTwoSurvivorsIsTheTarget()
        {
            MatchSession s = Start(BossCatalog.Giant);
            Vector2 boss = s.PositionOf(MatchSession.BossId);
            ParkOthers(s, 1, 2);
            Teleport(s, 1, boss + new Vector2(0f, -12f));
            Teleport(s, 2, boss + new Vector2(5f, -3f));

            CombatEvent announced = Use(s, 0, 0, out _);

            Assert.AreEqual(boss + new Vector2(5f, -3f), announced.Position);
        }

        [Test]
        public void WithNobodyAliveAnAimedSkillIsAnnouncedAtTheBossLikeAnOrdinaryOne()
        {
            MatchSession s = Start(BossCatalog.Giant);
            Vector2 boss = s.PositionOf(MatchSession.BossId);
            foreach (ulong id in s.SurvivorIds) s.World.Survivors.Kill(id);

            CombatEvent announced = Use(s, 0, 0, out BossSkillResult result);

            Assert.AreEqual(BossSkillResult.Used, result);
            Assert.AreEqual(boss, announced.Position);
        }

        [Test]
        public void ASkillThatIsNotMarkedStillStrikesAtTheBoss()
        {
            MatchSession s = Start(BossCatalog.Standard);
            Vector2 boss = s.PositionOf(MatchSession.BossId);
            ParkOthers(s, 1);
            Teleport(s, 1, boss + new Vector2(0f, -9f));
            Assert.IsFalse(BossCatalog.Get(BossCatalog.Standard).Skills[BossSkills.Shockwave].AtTarget);

            CombatEvent announced = Use(s, BossSkills.Shockwave, 0, out _);

            Assert.AreEqual(boss, announced.Position);
        }

        // ---- the data ----

        [Test]
        public void EveryKindHasItsSignatureSkillOnTheRightKeyWithTheRightEffectAndNoNewKey()
        {
            BossDef standard = BossCatalog.Get(BossCatalog.Standard);
            BossDef giant = BossCatalog.Get(BossCatalog.Giant);
            BossDef sniper = BossCatalog.Get(BossCatalog.Sniper);

            Assert.IsTrue(giant.Skills[0].AtTarget);
            Assert.AreEqual("Q", giant.Skills[0].Key);
            Assert.AreEqual(BossSkillEffect.Shockwave, giant.Skills[0].Effect);
            Assert.AreEqual(5.5f, giant.Skills[0].Radius);
            Assert.AreEqual(40, giant.Skills[0].Damage);

            Assert.IsTrue(sniper.Skills[1].AtTarget);
            Assert.AreEqual("E", sniper.Skills[1].Key);
            Assert.AreEqual(BossSkillEffect.Beam, sniper.Skills[1].Effect);

            Assert.IsTrue(standard.PhaseSkill.AtTarget);
            Assert.AreEqual("F", standard.PhaseSkill.Key);
            Assert.AreEqual(BossSkillEffect.Roar, standard.PhaseSkill.Effect);
            Assert.AreEqual(8, standard.PhaseSkill.Count);

            foreach (BossDef kind in BossCatalog.All)
            {
                Assert.AreEqual(2, kind.Skills.Length, kind.Name);
                var effects = kind.Skills.Select(k => k.Effect).Concat(new[] { kind.PhaseSkill.Effect }).ToArray();
                CollectionAssert.AllItemsAreUnique(effects, $"{kind.Name}: the effects of the skills do not repeat");
            }
        }

        [Test]
        public void TheFlagSurvivesTheCopiesTheMatchesUse()
        {
            BossCatalog.SetCurrentKind(BossCatalog.Giant);
            MatchSession s = MatchSession.Start(Settings(BossCatalog.Giant), 1);
            Assert.IsTrue(s.BossData.Skills[0].AtTarget);
            Assert.IsTrue(BossCatalog.ForMatch(BossCatalog.Giant).Skills[0].AtTarget);
            Assert.IsTrue(BossCatalog.Get(BossCatalog.Standard).PhaseSkill.Clone().AtTarget);
        }

        // ---- the opening cooldown of the leap strike ----

        static BossSkillState State() => new BossSkillState { MatchRunning = true, BossStanding = true, HealthFraction = 1f };

        [Test]
        public void TheGiantsLeapStrikeStartsTheMatchOnItsOpeningCooldownAndTheOtherSkillsStartReady()
        {
            var sim = new BossSkillSim();
            sim.Reset(BossCatalog.Get(BossCatalog.Giant));
            var events = new List<CombatEvent>();

            Assert.AreEqual(20f, sim.CooldownLeft(0));
            Assert.AreEqual(0f, sim.CooldownLeft(1), "the roar is ready at once");
            Assert.AreEqual(BossSkillResult.Cooldown, sim.TryUse(0, true, State(), 1, events));
            Assert.AreEqual(BossSkillResult.Used, sim.TryUse(1, true, State(), 1, events));

            var projectiles = new ProjectileSim();
            var survivors = new SurvivorSim();
            for (int i = 0; i < 1010; i++) sim.Step(Dt, true, true, projectiles, survivors, new List<SurvivorBody>(), 1, events);

            Assert.AreEqual(0f, sim.CooldownLeft(0));
            Assert.AreEqual(BossSkillResult.Used, sim.TryUse(0, true, State(), 1, events));

            sim.Reset(BossCatalog.Get(BossCatalog.Giant));
            Assert.AreEqual(20f, sim.CooldownLeft(0), "a second match starts the opening cooldown again");
        }

        [Test]
        public void OnlyTheGiantsLeapStrikeHasAnOpeningCooldown()
        {
            foreach (BossDef kind in BossCatalog.All)
            {
                var skills = kind.Skills.Concat(new[] { kind.PhaseSkill, kind.PhaseBurst });
                foreach (BossSkillDef skill in skills)
                {
                    bool leap = kind.Id == BossCatalog.Giant && skill == kind.Skills[0];
                    Assert.AreEqual(leap ? 20f : 0f, skill.OpeningCooldownSeconds, $"{kind.Name} {skill.Name}");
                }
            }

            var sim = new BossSkillSim();
            foreach (byte id in new[] { BossCatalog.Standard, BossCatalog.Sniper })
            {
                sim.Reset(BossCatalog.Get(id));
                for (int slot = 0; slot < 2; slot++) Assert.AreEqual(0f, sim.CooldownLeft(slot), $"kind {id} slot {slot}");
            }
        }
    }
}
