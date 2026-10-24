using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class RescuePointTests
    {
        const float Dt = 0.02f;
        static readonly Vector2 BossStart = new Vector2(0f, 6f);

        static SurvivorSim Survivors(params ulong[] ids)
        {
            var sim = new SurvivorSim();
            foreach (ulong id in ids) sim.Register(id);
            return sim;
        }

        static RescuePointSim Point(int seed = 1)
        {
            var p = new RescuePointSim();
            p.Reset(seed, BossStart);
            return p;
        }

        static List<SurvivorBody> At(Vector2 position, params ulong[] ids)
        {
            var list = new List<SurvivorBody>();
            foreach (ulong id in ids) list.Add(new SurvivorBody { ClientId = id, Position = position });
            return list;
        }

        /// <summary>Steps until the given match second with nobody inside.</summary>
        static void RunTo(RescuePointSim p, SurvivorSim s, float seconds, float from = 0f)
        {
            var nobody = new List<SurvivorBody>();
            int steps = Mathf.RoundToInt((seconds - from) / Dt);
            for (int i = 0; i < steps; i++) p.Step(Dt, true, nobody, s);
        }

        // ---- schedule ----

        [Test]
        public void TheFirstPointOpensAtNinetySeconds()
        {
            RescuePointSim p = Point();
            SurvivorSim s = Survivors(1);

            RunTo(p, s, 89f);
            Assert.IsFalse(p.IsOpen);

            RunTo(p, s, 91f, 89f);
            Assert.IsTrue(p.IsOpen);
        }

        [Test]
        public void TheNextPointOpensOneHundredAndTwentySecondsAfterACapture()
        {
            RescuePointSim p = Point();
            SurvivorSim s = Survivors(1);
            RunTo(p, s, 91f);
            Vector2 centre = p.Center;
            var inside = At(centre, 1);
            bool captured = false;
            float captureTime = 0f;
            for (int i = 0; i < 1000 && !captured; i++) { captured = p.Step(Dt, true, inside, s); captureTime = 91f + (i + 1) * Dt; }
            Assert.IsTrue(captured);
            Assert.IsFalse(p.IsOpen);

            var nobody = new List<SurvivorBody>();
            float elapsed = 0f;
            while (!p.IsOpen && elapsed < 200f) { p.Step(Dt, true, nobody, s); elapsed += Dt; }

            Assert.AreEqual(RescueConfig.GapSeconds, elapsed, 0.2f, $"opened {elapsed:F1} s after the capture at {captureTime:F1}");
        }

        [Test]
        public void AnUncapturedPointClosesAfterFortyFiveSecondsAndTheNextOpensAfterTheGap()
        {
            RescuePointSim p = Point();
            SurvivorSim s = Survivors(1);
            RunTo(p, s, 91f);
            Assert.IsTrue(p.IsOpen);

            RunTo(p, s, 91f + RescueConfig.LifeSeconds, 91f);
            Assert.IsFalse(p.IsOpen, "closed after its life");

            RunTo(p, s, 91f + RescueConfig.LifeSeconds + RescueConfig.GapSeconds - 2f, 91f + RescueConfig.LifeSeconds);
            Assert.IsFalse(p.IsOpen);
            RunTo(p, s, 91f + RescueConfig.LifeSeconds + RescueConfig.GapSeconds + 2f, 91f + RescueConfig.LifeSeconds + RescueConfig.GapSeconds - 2f);
            Assert.IsTrue(p.IsOpen);
            Assert.AreEqual(2, p.Opened);
        }

        [Test]
        public void NothingOpensWhileInactiveOrDisabledAndAnOpenPointClosesWhenTheMatchStopsRunning()
        {
            RescuePointSim p = Point();
            SurvivorSim s = Survivors(1);
            var nobody = new List<SurvivorBody>();
            for (int i = 0; i < 6000; i++) p.Step(Dt, false, nobody, s);
            Assert.IsFalse(p.IsOpen, "inactive (no boss or the match is not running)");

            RunTo(p, s, 91f);
            Assert.IsTrue(p.IsOpen);
            p.Step(Dt, false, nobody, s);
            Assert.IsFalse(p.IsOpen);

            RescuePointSim off = Point();
            off.Enabled = false;
            for (int i = 0; i < 6000; i++) off.Step(Dt, true, nobody, s);
            Assert.IsFalse(off.IsOpen);
        }

        // ---- place ----

        [Test]
        public void TheSameSeedGivesTheSamePlacesInTheSameOrder()
        {
            for (int index = 0; index < 6; index++)
                Assert.AreEqual(RescuePointSim.PickCenter(9, index, BossStart), RescuePointSim.PickCenter(9, index, BossStart));
            Assert.AreNotEqual(RescuePointSim.PickCenter(9, 0, BossStart), RescuePointSim.PickCenter(10, 0, BossStart));
        }

        [Test]
        public void EveryPlaceIsInsideTheArenaAndFarFromTheBossStart()
        {
            float hx = ArenaConfig.Width * 0.5f - RescueConfig.Radius + 0.001f;
            float hy = ArenaConfig.Height * 0.5f - RescueConfig.Radius + 0.001f;
            for (int seed = 0; seed < 300; seed++)
                for (int index = 0; index < 4; index++)
                {
                    Vector2 c = RescuePointSim.PickCenter(seed, index, BossStart);
                    Assert.LessOrEqual(Mathf.Abs(c.x), hx, $"seed {seed} index {index}");
                    Assert.LessOrEqual(Mathf.Abs(c.y), hy, $"seed {seed} index {index}");
                    Assert.GreaterOrEqual(Vector2.Distance(c, BossStart), RescueConfig.MinBossDistance - 0.001f, $"seed {seed} index {index}");
                }
        }

        // ---- capture ----

        static RescuePointSim OpenPoint(SurvivorSim s)
        {
            RescuePointSim p = Point();
            RunTo(p, s, 90.5f);
            Assert.IsTrue(p.IsOpen);
            return p;
        }

        static float SecondsToCapture(RescuePointSim p, SurvivorSim s, List<SurvivorBody> bodies)
        {
            for (int i = 1; i <= 2000; i++)
                if (p.Step(Dt, true, bodies, s)) return i * Dt;
            return -1f;
        }

        [Test]
        public void OneSurvivorNeedsTwelveSecondsAndFourNeedThree()
        {
            SurvivorSim one = Survivors(1);
            RescuePointSim p1 = OpenPoint(one);
            Assert.AreEqual(RescueConfig.CaptureSeconds, SecondsToCapture(p1, one, At(p1.Center, 1)), 0.1f);

            SurvivorSim four = Survivors(1, 2, 3, 4);
            RescuePointSim p4 = OpenPoint(four);
            Assert.AreEqual(RescueConfig.CaptureSeconds / 4f, SecondsToCapture(p4, four, At(p4.Center, 1, 2, 3, 4)), 0.1f);
        }

        [Test]
        public void LeavingKeepsTheProgress()
        {
            SurvivorSim s = Survivors(1);
            RescuePointSim p = OpenPoint(s);
            var inside = At(p.Center, 1);
            int half = Mathf.RoundToInt(RescueConfig.CaptureSeconds * 0.5f / Dt);
            for (int i = 0; i < half; i++) p.Step(Dt, true, inside, s);
            float atHalf = p.Progress;
            Assert.AreEqual(0.5f, atHalf, 0.02f);

            var outside = At(p.Center + new Vector2(RescueConfig.Radius + 3f, 0f), 1);
            for (int i = 0; i < 100; i++) p.Step(Dt, true, outside, s);

            Assert.AreEqual(atHalf, p.Progress, 0.0001f);
        }

        [Test]
        public void AFallenSurvivorDoesNotCount()
        {
            SurvivorSim s = Survivors(1);
            RescuePointSim p = OpenPoint(s);
            s.Kill(1);

            for (int i = 0; i < 200; i++) p.Step(Dt, true, At(p.Center, 1), s);

            Assert.AreEqual(0f, p.Progress);
        }

        // ---- heal ----

        [Test]
        public void ACaptureHealsHalfTheMaximumOfThoseInsideOnlyAndRevivesNobody()
        {
            SurvivorSim s = Survivors(1, 2, 3);
            RescuePointSim p = OpenPoint(s);
            int max = s.MaxHealthOf(1);
            s.ApplyDamage(1, max - 10);
            s.ApplyDamage(3, max - 10);
            s.Kill(2);
            Vector2 away = p.Center + new Vector2(RescueConfig.Radius + 4f, 0f);
            var bodies = new List<SurvivorBody>
            {
                new SurvivorBody { ClientId = 1, Position = p.Center },
                new SurvivorBody { ClientId = 2, Position = p.Center },
                new SurvivorBody { ClientId = 3, Position = away },
            };

            Assert.Greater(SecondsToCapture(p, s, bodies), 0f);

            Assert.AreEqual(10 + max / 2, s.Health(1), "inside: healed by half the maximum");
            Assert.AreEqual(10, s.Health(3), "outside: not healed");
            Assert.IsFalse(s.IsAlive(2), "a fallen survivor in the circle is not revived");
        }

        // ---- slow ----

        [Test]
        public void MobsMoveAtTheSlowFactorInsideTheZoneAndFullSpeedOutsideAndAfterItCloses()
        {
            float Travel(Vector2 start, bool zone)
            {
                var mobs = new MobSim(targetCount: 0);
                mobs.TrySpawn(start, 0, out _);
                mobs.SetSlowZone(zone, new Vector2(0f, 0f), 3f, 0.6f);
                var survivors = new List<SurvivorInfo> { new SurvivorInfo { ClientId = 1, Position = new Vector2(-8f, 0f), Alive = true } };
                Vector2 before = mobs.PositionAt(0);
                for (int i = 0; i < 10; i++) mobs.Step(Dt, survivors);
                return Vector2.Distance(before, mobs.PositionAt(0));
            }

            float full = Travel(new Vector2(2f, 0f), zone: false);
            float slowed = Travel(new Vector2(2f, 0f), zone: true);
            float outside = Travel(new Vector2(6f, 0f), zone: true);

            Assert.Greater(full, 0f);
            Assert.AreEqual(full * 0.6f, slowed, full * 0.02f, "inside the open zone");
            Assert.AreEqual(full, outside, full * 0.02f, "outside the zone");
        }

        [Test]
        public void ADeviceInsideTheZoneStillStandsStill()
        {
            var mobs = new MobSim(targetCount: 0);
            mobs.TrySpawn(new Vector2(1f, 0f), MobKinds.Encode(MobKinds.OriginSummoned, MobKinds.Device), out _);
            mobs.SetSlowZone(true, Vector2.zero, 3f, 0.6f);
            Vector2 before = mobs.PositionAt(0);
            for (int i = 0; i < 10; i++) mobs.Step(Dt, new List<SurvivorInfo> { new SurvivorInfo { ClientId = 1, Position = new Vector2(-8f, 0f), Alive = true } });

            Assert.AreEqual(before, mobs.PositionAt(0));
        }

        // ---- the world, the override and the bot ----

        static HarnessSettings Settings(params (string name, float value)[] overrides)
        {
            var s = new HarnessSettings();
            s.overrides.Add(new NamedValue { name = "mobAutoTarget", value = 0f });
            s.overrides.Add(new NamedValue { name = "matchSeconds", value = 400f });
            foreach ((string name, float value) o in overrides) s.overrides.Add(new NamedValue { name = o.name, value = o.value });
            return s;
        }

        [Test]
        public void TheOverrideTurnsThePointsOffAndRefusesOtherValues()
        {
            Assert.IsTrue(HarnessOverrides.IsKnown("rescuePoints"));
            Assert.IsNull(Settings(("rescuePoints", 0f)).Validate());
            Assert.IsNull(Settings(("rescuePoints", 1f)).Validate());
            StringAssert.Contains("rescuePoints", Settings(("rescuePoints", 2f)).Validate());
        }

        [Test]
        public void AMatchOpensAPointAtNinetySecondsUnlessTheyAreOff()
        {
            BossCatalog.SetCurrentKind(BossCatalog.Standard);
            MatchSession on = MatchSession.Start(Settings(), 3);
            for (int i = 0; i < Mathf.RoundToInt(91f / Dt); i++) on.Advance();
            Assert.IsTrue(on.World.RescuePoint.IsOpen || on.World.RescuePoint.Opened > 0);
            Assert.IsTrue(on.World.Mobs.SlowZoneActive == on.World.RescuePoint.IsOpen);

            MatchSession off = MatchSession.Start(Settings(("rescuePoints", 0f)), 3);
            for (int i = 0; i < Mathf.RoundToInt(91f / Dt); i++) off.Advance();
            Assert.AreEqual(0, off.World.RescuePoint.Opened);
            Assert.IsFalse(off.World.Mobs.SlowZoneActive);
        }

        [Test]
        public void ASameSeedMatchWithPointsIsRepeatable()
        {
            string Run()
            {
                BossCatalog.SetCurrentKind(BossCatalog.Standard);
                MatchSession s = MatchSession.Start(Settings(), 5);
                var controller = new BotController(s, SkillProfiles.Normal);
                for (int i = 0; i < 6500; i++) { controller.Decide(s, s.Step); s.Advance(); }
                return $"{s.World.Boss.Health}/{s.World.RescuePoint.Captured}/{s.World.RescuePoint.Opened}";
            }

            Assert.AreEqual(Run(), Run());
        }

        [Test]
        public void ASurvivorBotWithNothingNearWalksToAnOpenPoint()
        {
            BossCatalog.SetCurrentKind(BossCatalog.Standard);
            MatchSession s = MatchSession.Start(Settings(("bossPhases", 0f)), 4);
            for (int i = 0; i < Mathf.RoundToInt(91f / Dt); i++) s.Advance();
            Assert.IsTrue(s.World.RescuePoint.IsOpen);
            Vector2 centre = s.World.RescuePoint.Center;

            // Put the first survivor far from the mobs and the boss; the bot must head for the point.
            for (int i = 0; i < s.World.Bodies.Count; i++)
            {
                MatchBody b = s.World.Bodies[i];
                if (b.ClientId == 1) { b.Position = new Vector2(-14f, -9f); s.World.Bodies[i] = b; }
            }
            s.World.Mobs.Clear();
            var bot = new SurvivorBot(1, s.CharacterOf(1), new SkillProfile { Name = "exact", AimErrorDegrees = 0f, ReactionSeconds = 0.3f, BossPreference = 0f }, 1);
            bot.Decide(s, s.Step);

            Vector2 toPoint = (centre - s.PositionOf(1)).normalized;
            Assert.Greater(Vector2.Dot(s.DirectionOf(1), toPoint), 0.9f);
        }

        // ---- the contest ----

        [Test]
        public void TheBossInTheCircleStopsTheProgressAndKeepsWhatWasMadeAndLeavingResumesIt()
        {
            SurvivorSim s = Survivors(1);
            RescuePointSim p = OpenPoint(s);
            var inside = At(p.Center, 1);
            for (int i = 0; i < 100; i++) p.Step(Dt, true, inside, s);
            float made = p.Progress;
            Assert.Greater(made, 0f);

            for (int i = 0; i < 100; i++) p.Step(Dt, true, inside, s, bossInside: true);
            Assert.IsTrue(p.Contested);
            Assert.AreEqual(made, p.Progress, 0.0001f, "kept, not raised");

            for (int i = 0; i < 100; i++) p.Step(Dt, true, inside, s, bossInside: false);
            Assert.IsFalse(p.Contested);
            Assert.Greater(p.Progress, made);
        }

        [Test]
        public void AContestedPointStillClosesAfterItsLifeWithoutACapture()
        {
            SurvivorSim s = Survivors(1);
            RescuePointSim p = OpenPoint(s);
            var inside = At(p.Center, 1);
            bool captured = false;
            for (int i = 0; i < Mathf.RoundToInt((RescueConfig.LifeSeconds + 1f) / Dt); i++) captured |= p.Step(Dt, true, inside, s, bossInside: true);

            Assert.IsFalse(captured);
            Assert.IsFalse(p.IsOpen);
            Assert.AreEqual(0, p.Captured);
        }

        [Test]
        public void TurningTheContestOffLetsTheSurvivorsCaptureWithTheBossInside()
        {
            SurvivorSim s = Survivors(1);
            RescuePointSim p = OpenPoint(s);
            p.ContestEnabled = false;

            float seconds = -1f;
            for (int i = 1; i <= 2000 && seconds < 0f; i++)
                if (p.Step(Dt, true, At(p.Center, 1), s, bossInside: true)) seconds = i * Dt;

            Assert.AreEqual(RescueConfig.CaptureSeconds, seconds, 0.1f);
        }

        [Test]
        public void TheBossOverlappingTheCircleContestsButNextToItDoesNotAndAFallenBossNever()
        {
            BossCatalog.SetCurrentKind(BossCatalog.Standard);
            MatchSession s = MatchSession.Start(Settings(("bossPhases", 0f)), 4);
            for (int i = 0; i < Mathf.RoundToInt(91f / Dt); i++) s.Advance();
            RescuePointSim p = s.World.RescuePoint;
            Assert.IsTrue(p.IsOpen);
            float reach = RescueConfig.Radius + s.World.Boss.Radius;

            void PlaceBoss(Vector2 at)
            {
                for (int i = 0; i < s.World.Bodies.Count; i++)
                {
                    MatchBody b = s.World.Bodies[i];
                    if (b.ClientId == MatchSession.BossId) { b.Position = at; s.World.Bodies[i] = b; }
                }
            }

            PlaceBoss(p.Center + new Vector2(reach - 0.2f, 0f));
            s.Advance();
            Assert.IsTrue(p.Contested, "overlapping");

            PlaceBoss(p.Center + new Vector2(reach + 0.5f, 0f));
            s.Advance();
            Assert.IsFalse(p.Contested, "next to the circle");

            PlaceBoss(p.Center);
            s.World.Boss.TakeDamage(s.World.Boss.MaxHealth, out _, out _);
            s.Advance();
            Assert.IsFalse(p.Contested, "a fallen boss does not contest");
        }

        [Test]
        public void TheContestOverrideAndTheBarText()
        {
            Assert.IsTrue(HarnessOverrides.IsKnown("rescueContest"));
            Assert.IsNull(Settings(("rescueContest", 0f)).Validate());
            Assert.IsNull(Settings(("rescueContest", 1f)).Validate());
            StringAssert.Contains("rescueContest", Settings(("rescueContest", 2f)).Validate());

            BossCatalog.SetCurrentKind(BossCatalog.Standard);
            MatchSession off = MatchSession.Start(Settings(("rescueContest", 0f)), 3);
            Assert.IsFalse(off.World.RescuePoint.ContestEnabled);

            StringAssert.DoesNotContain("방해", MatchHud.RescueBarText(0.5f, 20));
            StringAssert.Contains("방해", MatchHud.RescueBarText(0.5f, 20, contested: true));
        }
    }
}
