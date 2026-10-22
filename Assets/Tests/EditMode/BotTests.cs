using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class BotTests
    {
        static readonly SkillProfile Exact = new SkillProfile { Name = "exact", AimErrorDegrees = 0f, ReactionSeconds = 0.3f, BossPreference = 1f };
        static readonly SkillProfile MobsOnly = new SkillProfile { Name = "mobs", AimErrorDegrees = 0f, ReactionSeconds = 0.3f, BossPreference = 0f };

        static MatchSession Quiet(params (string name, float value)[] more)
        {
            var s = new HarnessSettings();
            s.overrides.Add(new NamedValue { name = "mobAutoTarget", value = 0f });
            s.overrides.Add(new NamedValue { name = "matchSeconds", value = 120f });
            foreach ((string name, float value) o in more) s.overrides.Add(new NamedValue { name = o.name, value = o.value });
            BossCatalog.SetCurrentKind(BossCatalog.Standard);
            return MatchSession.Start(s, 1);
        }

        static SurvivorBot Bot(MatchSession s, ulong id, SkillProfile profile, int seed = 1) => new SurvivorBot(id, s.CharacterOf(id), profile, seed);

        static void Teleport(MatchSession s, ulong id, Vector2 position)
        {
            for (int i = 0; i < s.World.Bodies.Count; i++)
            {
                MatchBody b = s.World.Bodies[i];
                if (b.ClientId == id) { b.Position = position; s.World.Bodies[i] = b; }
            }
        }

        static ushort AngleTo(Vector2 from, Vector2 to) => AimMath.FromDirection(to - from);

        static float AngleDifferenceDegrees(ushort a, ushort b)
        {
            float d = Mathf.Abs(AimMath.DequantizeAngle(a) - AimMath.DequantizeAngle(b)) * Mathf.Rad2Deg;
            return d > 180f ? 360f - d : d;
        }

        static void SpawnMob(MatchSession s, Vector2 at)
        {
            Assert.IsTrue(s.World.Mobs.TrySpawn(at, MobSim.TypeSummoned, out _));
        }

        static void Idle(MatchSession s, float seconds)
        {
            int steps = Mathf.RoundToInt(seconds / s.Step);
            for (int i = 0; i < steps; i++) s.Advance();
        }

        // ---- survivors (3.1) ----

        [Test]
        public void TheProfilesAreOrderedFromWeakToStrong()
        {
            Assert.Greater(SkillProfiles.Weak.AimErrorDegrees, SkillProfiles.Normal.AimErrorDegrees);
            Assert.Greater(SkillProfiles.Normal.AimErrorDegrees, SkillProfiles.Strong.AimErrorDegrees);
            Assert.Greater(SkillProfiles.Weak.ReactionSeconds, SkillProfiles.Normal.ReactionSeconds);
            Assert.Greater(SkillProfiles.Normal.ReactionSeconds, SkillProfiles.Strong.ReactionSeconds);
            Assert.Less(SkillProfiles.Weak.BossPreference, SkillProfiles.Normal.BossPreference);
            Assert.Less(SkillProfiles.Normal.BossPreference, SkillProfiles.Strong.BossPreference);
            Assert.AreSame(SkillProfiles.Strong, SkillProfiles.Find("STRONG"));
            Assert.IsNull(SkillProfiles.Find("godlike"));
        }

        [Test]
        public void ASurvivorWalksAwayFromAMobThatIsTooClose()
        {
            MatchSession s = Quiet();
            Vector2 me = s.PositionOf(1);
            SpawnMob(s, me + new Vector2(0.8f, 0f));
            SurvivorBot bot = Bot(s, 1, Exact);

            bot.Decide(s, s.Step);

            Vector2 dir = s.DirectionOf(1);
            Assert.Less(dir.x, -0.3f);
        }

        [Test]
        public void ASurvivorWithNothingNearStaysPutWhenTheBossIsInRange()
        {
            MatchSession s = Quiet();
            Teleport(s, 1, new Vector2(0f, -2f)); // the boss stands at (0, 6): 8 away, inside the shot range
            SurvivorBot bot = Bot(s, 1, Exact);

            bot.Decide(s, s.Step);

            Assert.AreEqual(Vector2.zero, s.DirectionOf(1));
        }

        [Test]
        public void ASurvivorOutOfRangeOfTheBossWalksTowardIt()
        {
            MatchSession s = Quiet();
            Teleport(s, 1, new Vector2(-15f, -9f));
            SurvivorBot bot = Bot(s, 1, Exact);

            bot.Decide(s, s.Step);

            Vector2 toBoss = (s.PositionOf(MatchSession.BossId) - s.PositionOf(1)).normalized;
            Assert.Greater(Vector2.Dot(s.DirectionOf(1), toBoss), 0.9f);
        }

        [Test]
        public void ASurvivorGoesToAFallenTeammateAndOnlyTheNearestOneDoes()
        {
            MatchSession s = Quiet();
            // The boss is far away on this side, so nothing pulls the survivors elsewhere.
            Teleport(s, 1, new Vector2(-6f, -2f));
            Teleport(s, 2, new Vector2(-13f, -2f));
            Teleport(s, 3, new Vector2(-9f, -8f));
            Teleport(s, 4, new Vector2(8f, -8f));
            s.World.Survivors.Kill(3);
            SurvivorBot nearest = Bot(s, 1, Exact);
            SurvivorBot other = Bot(s, 2, Exact);

            nearest.Decide(s, s.Step);
            other.Decide(s, s.Step);

            Vector2 toFallen = (s.PositionOf(3) - s.PositionOf(1)).normalized;
            Assert.Greater(Vector2.Dot(s.DirectionOf(1), toFallen), 0.9f);
            Assert.AreNotEqual(Vector2.zero, s.DirectionOf(1));
            // The one that is not nearest does not rush in: it keeps its own plan (toward the boss, which is out of range from there).
            Vector2 toFallenFromOther = (s.PositionOf(3) - s.PositionOf(2)).normalized;
            Assert.Less(Vector2.Dot(s.DirectionOf(2), toFallenFromOther), 0.9f);
        }

        [Test]
        public void ARescuerStandsStillNextToTheFallenOne()
        {
            MatchSession s = Quiet();
            Teleport(s, 1, new Vector2(-6f, -2f));
            Teleport(s, 3, new Vector2(-6.5f, -2f));
            s.World.Survivors.Kill(3);
            SurvivorBot bot = Bot(s, 1, Exact);

            bot.Decide(s, s.Step);

            Assert.AreEqual(Vector2.zero, s.DirectionOf(1));
        }

        [Test]
        public void ASurvivorThatPrefersTheBossAimsAtItAndOneThatDoesNotAimsAtTheNearestMob()
        {
            MatchSession s = Quiet();
            Vector2 me = new Vector2(0f, -2f);
            Teleport(s, 1, me);
            Vector2 boss = s.PositionOf(MatchSession.BossId);
            Vector2 mobAt = me + new Vector2(3f, 0f);
            SpawnMob(s, mobAt);

            Bot(s, 1, Exact).Decide(s, s.Step);
            Assert.Less(AngleDifferenceDegrees(s.World.Attacks.AimOf(1), AngleTo(me, boss)), 1f);

            Bot(s, 1, MobsOnly).Decide(s, s.Step);
            Assert.Less(AngleDifferenceDegrees(s.World.Attacks.AimOf(1), AngleTo(me, mobAt)), 1f);
        }

        [Test]
        public void TheAimStaysWithinTheErrorOfTheProfile()
        {
            var profile = new SkillProfile { Name = "wide", AimErrorDegrees = 10f, ReactionSeconds = 0.3f, BossPreference = 1f };
            float worst = 0f;
            var seen = new HashSet<int>();
            for (int seed = 1; seed <= 40; seed++)
            {
                MatchSession s = Quiet();
                Vector2 me = new Vector2(0f, -2f);
                Teleport(s, 1, me);
                Bot(s, 1, profile, seed).Decide(s, s.Step);
                float off = AngleDifferenceDegrees(s.World.Attacks.AimOf(1), AngleTo(me, s.PositionOf(MatchSession.BossId)));
                worst = Mathf.Max(worst, off);
                seen.Add(Mathf.RoundToInt(off));
            }

            Assert.LessOrEqual(worst, 10.5f);
            Assert.Greater(seen.Count, 3); // the miss differs from bot to bot
        }

        [Test]
        public void ASurvivorWithAMobOnTopOfItStillRunsAndAnEqualDistanceSendsOnlyOneRescuer()
        {
            MatchSession s = Quiet();
            Teleport(s, 1, new Vector2(0f, -2f));
            SpawnMob(s, new Vector2(0f, -2f)); // exactly on the survivor
            Bot(s, 1, Exact).Decide(s, s.Step);
            Assert.Greater(s.DirectionOf(1).magnitude, 0.5f);

            MatchSession t = Quiet();
            Teleport(t, 1, new Vector2(-6f, -2f));
            Teleport(t, 2, new Vector2(-12f, -2f));
            Teleport(t, 3, new Vector2(-9f, -8f)); // as far from survivor 1 as from survivor 2
            t.World.Survivors.Kill(3);
            Bot(t, 1, Exact).Decide(t, t.Step);
            Bot(t, 2, Exact).Decide(t, t.Step);
            Vector2 toFallen1 = (t.PositionOf(3) - t.PositionOf(1)).normalized;
            Vector2 toFallen2 = (t.PositionOf(3) - t.PositionOf(2)).normalized;
            bool oneGoes1 = Vector2.Dot(t.DirectionOf(1), toFallen1) > 0.9f;
            bool oneGoes2 = Vector2.Dot(t.DirectionOf(2), toFallen2) > 0.9f;
            Assert.AreNotEqual(oneGoes1, oneGoes2);
            Assert.IsTrue(oneGoes1); // the lower id
        }

        [Test]
        public void ASurvivorStepsSidewaysOutOfTheWayOfAShotThatWouldHit()
        {
            MatchSession s = Quiet();
            Vector2 me = new Vector2(0f, -2f);
            Teleport(s, 1, me);
            Assert.IsTrue(s.World.Projectiles.TryFire(new ProjectileSpec
            {
                Owner = MatchSession.BossId, Origin = me + new Vector2(6f, 0f), Angle = AimMath.FromDirection(Vector2.left),
                Damage = 15, Shape = BossAttack.Shape, Hostile = true,
            }, out _));

            Bot(s, 1, Exact).Decide(s, s.Step);

            Vector2 dir = s.DirectionOf(1);
            Assert.Greater(Mathf.Abs(dir.y), 0.7f); // across the line of flight, which runs along x
            Assert.Less(Mathf.Abs(dir.x), 0.7f);
        }

        [Test]
        public void ASurvivorIgnoresAShotThatPassesClearAndOneThatIsNotHostile()
        {
            MatchSession s = Quiet();
            Vector2 me = new Vector2(0f, -2f);
            Teleport(s, 1, me);
            // Passes two units above: clear.
            Assert.IsTrue(s.World.Projectiles.TryFire(new ProjectileSpec
            {
                Owner = MatchSession.BossId, Origin = me + new Vector2(6f, 2f), Angle = AimMath.FromDirection(Vector2.left),
                Damage = 15, Shape = BossAttack.Shape, Hostile = true,
            }, out _));
            // Heads straight at the survivor but is a survivor's own shot.
            Assert.IsTrue(s.World.Projectiles.TryFire(new ProjectileSpec
            {
                Owner = 2, Origin = me + new Vector2(-6f, 0f), Angle = AimMath.FromDirection(Vector2.right),
                Damage = 10, Shape = ProjectileShape.Default, Hostile = false,
            }, out _));

            Bot(s, 1, Exact).Decide(s, s.Step);

            Assert.AreEqual(Vector2.zero, s.DirectionOf(1));
        }

        /// <summary>A lone survivor bot against a boss that stands still and shoots at it; returns the damage taken over ten seeds of twenty seconds.</summary>
        static int DamageFromAStandingBoss(float reactionSeconds, bool dodge)
        {
            bool before = BotMath.DodgeShots;
            BotMath.DodgeShots = dodge;
            try
            {
                var profile = new SkillProfile { Name = "x", AimErrorDegrees = 0f, ReactionSeconds = reactionSeconds, BossPreference = 0f };
                int total = 0;
                for (int seed = 1; seed <= 10; seed++)
                {
                    var settings = new HarnessSettings { matches = 1, characters = new[] { 1 } };
                    foreach ((string name, float value) o in new[]
                    {
                        ("mobAutoTarget", 0f), ("summonMaxEnergy", 0f), ("bossContactDamage", 0f), ("bossSkillDamage", 1f),
                        ("bossHealth", 5000000f), ("survivorHealthBonus", 100000f), ("matchSeconds", 20f),
                    })
                        settings.overrides.Add(new NamedValue { name = o.name, value = o.value });

                    BossCatalog.SetCurrentKind(BossCatalog.Standard);
                    MatchSession s = MatchSession.Start(settings, seed);
                    var bot = new SurvivorBot(1, s.CharacterOf(1), profile, seed);
                    while (s.World.MatchRunning)
                    {
                        bot.Decide(s, s.Step);
                        Vector2 toSurvivor = s.PositionOf(1) - s.PositionOf(MatchSession.BossId);
                        s.Aim(MatchSession.BossId, AimMath.FromDirection(toSurvivor), true);
                        s.Advance();
                    }
                    total += s.World.Survivors.MaxHealthOf(1) - s.World.Survivors.Health(1);
                }
                return total;
            }
            finally { BotMath.DodgeShots = before; }
        }

        [Test]
        public void ASurvivorThatDodgesTakesFarLessFromABossThatShootsAtIt()
        {
            int dodging = DamageFromAStandingBoss(0.1f, dodge: true);
            int standing = DamageFromAStandingBoss(0.1f, dodge: false);

            Assert.Greater(standing, 1000);
            Assert.Less(dodging, standing / 4);
        }

        [Test]
        public void ASlowReactingBotDodgesLessThanAFastOne()
        {
            int quick = DamageFromAStandingBoss(0.1f, dodge: true);
            int sluggish = DamageFromAStandingBoss(0.9f, dodge: true);

            Assert.Less(quick, sluggish);
        }

        [Test]
        public void ASurvivorDoesNotChangeItsMindDuringTheReactionTime()
        {
            var slow = new SkillProfile { Name = "slow", AimErrorDegrees = 0f, ReactionSeconds = 1.0f, BossPreference = 1f };
            MatchSession s = Quiet(("mobHealth", 1000000f)); // the mob must outlive the survivor's shots
            Teleport(s, 1, new Vector2(0f, -2f));
            SurvivorBot bot = Bot(s, 1, slow);
            bot.Decide(s, s.Step);
            Assert.AreEqual(Vector2.zero, s.DirectionOf(1));

            SpawnMob(s, s.PositionOf(1) + new Vector2(0.7f, 0f)); // danger appears
            Idle(s, 0.3f);
            bot.Decide(s, s.Step);
            Assert.AreEqual(Vector2.zero, s.DirectionOf(1)); // still going on with the old plan

            Idle(s, 1.2f);
            bot.Decide(s, s.Step);
            Assert.AreNotEqual(Vector2.zero, s.DirectionOf(1)); // it has looked again
        }

        [Test]
        public void ASurvivorUsesALearnedSkillWhenSomethingIsInRange()
        {
            MatchSession s = Quiet();
            Teleport(s, 1, new Vector2(0f, -2f));
            SkillDef q = SkillCatalog.ForSlot(0, CharacterIds.Reimu);
            s.World.Leveling.GrantRank(1, q.Id, 1);
            SurvivorBot bot = Bot(s, 1, Exact);

            bot.Decide(s, s.Step);
            s.Advance();

            Assert.IsTrue(s.World.SkillOutcomes.Exists(o => o.ClientId == 1 && o.Slot == 0 && o.Result == SkillResult.Fired));
        }

        [Test]
        public void ASurvivorAnswersASkillChoice()
        {
            MatchSession s = Quiet();
            Teleport(s, 1, new Vector2(0f, -2f));
            SurvivorBot bot = Bot(s, 1, Exact);
            s.World.Leveling.AddXp(1, GrowthConfig.XpNeeded(1));

            bool answered = false;
            for (int i = 0; i < 400 && !answered; i++)
            {
                bot.Decide(s, s.Step);
                s.Advance();
                answered = s.World.GrowthEvents.Exists(e => e.Kind == GrowthEventKind.OfferClosed && e.ClientId == 1 && !e.Auto);
            }

            Assert.IsTrue(answered, "the bot picked before the offer timed out");
        }

        [Test]
        public void AFallenSurvivorGivesNoCommands()
        {
            MatchSession s = Quiet();
            Teleport(s, 1, new Vector2(0f, -2f));
            SurvivorBot bot = Bot(s, 1, Exact);
            bot.Decide(s, s.Step);
            s.World.Survivors.Kill(1);

            bot.Decide(s, s.Step);

            Assert.AreEqual(Vector2.zero, s.DirectionOf(1));
        }

        [Test]
        public void ASurvivorBotShootsSoTheBossLosesHealth()
        {
            MatchSession s = Quiet();
            Teleport(s, 1, new Vector2(0f, -2f));
            SurvivorBot bot = Bot(s, 1, Exact);
            int before = s.World.Boss.Health;

            for (int i = 0; i < 250; i++)
            {
                bot.Decide(s, s.Step);
                s.Advance();
            }

            Assert.Less(s.World.Boss.Health, before);
        }

        // ---- the boss (3.2) ----

        [Test]
        public void TheBossBotChasesTheNearestLivingSurvivor()
        {
            MatchSession s = Quiet();
            Teleport(s, 1, new Vector2(-4f, 4f));
            Teleport(s, 2, new Vector2(12f, -8f));
            s.World.Survivors.Kill(3);
            Teleport(s, 3, new Vector2(0.5f, 5f)); // the nearest of all, but fallen
            var bot = new BossBot(s.BossData, Exact, 1);

            bot.Decide(s, s.Step);

            Vector2 toNearestLiving = (s.PositionOf(1) - s.PositionOf(MatchSession.BossId)).normalized;
            Assert.Greater(Vector2.Dot(s.DirectionOf(MatchSession.BossId), toNearestLiving), 0.9f);
        }

        [Test]
        public void TheBossBotShootsAtItsTargetWithinRange()
        {
            MatchSession s = Quiet();
            Teleport(s, 1, new Vector2(0f, -2f));
            var bot = new BossBot(s.BossData, Exact, 1);
            bot.Decide(s, s.Step);
            Vector2 boss = s.PositionOf(MatchSession.BossId);

            Assert.Less(AngleDifferenceDegrees(s.World.Attacks.AimOf(MatchSession.BossId), AngleTo(boss, new Vector2(0f, -2f))), 1f);
        }

        [Test]
        public void TheBossBotUsesBothSkillsWhenTheyAreReady()
        {
            MatchSession s = Quiet();
            var bot = new BossBot(s.BossData, Exact, 1);
            Assert.AreEqual(0f, s.World.BossSkills.CooldownLeft(0));

            bot.Decide(s, s.Step);

            Assert.Greater(s.World.BossSkills.CooldownLeft(0), 0f);
            Assert.Greater(s.World.BossSkills.CooldownLeft(1), 0f);
        }

        [Test]
        public void TheBossBotSummonsMobsInTurnAndPlacesADeviceWhenItCan()
        {
            MatchSession s = Quiet();
            var bot = new BossBot(s.BossData, Exact, 1);
            var kinds = new HashSet<byte>();

            for (int i = 0; i < 900; i++)
            {
                bot.Decide(s, s.Step);
                s.Advance();
                for (int m = 0; m < s.World.Mobs.Count; m++) if (!s.World.Mobs.IsDevice(m)) kinds.Add(s.World.Mobs.KindAt(m));
            }

            Assert.Greater(s.World.Mobs.DevicesPlaced, 0);
            Assert.GreaterOrEqual(kinds.Count, 3);
        }

        [Test]
        public void ADeviceIsNotPlacedWhileASurvivorIsClose()
        {
            MatchSession s = Quiet();
            Teleport(s, 1, new Vector2(0f, 5f)); // right next to the boss
            var bot = new BossBot(s.BossData, Exact, 1);

            bot.Decide(s, s.Step);

            Assert.AreEqual(0, s.World.Mobs.DevicesPlaced);
        }

        [Test]
        public void AFallenBossGivesNoCommands()
        {
            MatchSession s = Quiet();
            var bot = new BossBot(s.BossData, Exact, 1);
            s.World.Boss.TakeDamage(int.MaxValue, out _, out _);

            bot.Decide(s, s.Step);

            Assert.AreEqual(Vector2.zero, s.DirectionOf(MatchSession.BossId));
            Assert.AreEqual(0, s.World.Mobs.Count);
            Assert.AreEqual(0f, s.World.BossSkills.CooldownLeft(0));
        }

        // ---- whole matches (3.3) ----

        [Test]
        public void EveryProfileAndEveryBossKindPlaysAMatchToAResult()
        {
            foreach (SkillProfile profile in SkillProfiles.All)
            {
                for (byte kind = BossCatalog.Standard; kind <= BossCatalog.Sniper; kind++)
                {
                    var settings = new HarnessSettings { profile = profile.Name, bossKind = kind };
                    var watch = Stopwatch.StartNew();

                    MatchRow row = MatchRunner.RunOne(settings, 4);

                    watch.Stop();
                    UnityEngine.Debug.Log($"[harness] {profile.Name}/{BossCatalog.Get(kind).Name}: {row.winner} ({row.reason}) after {row.lengthSeconds:F0} s, boss t50={row.bossT50:F0} falls={row.falls} maxLevel={row.maxLevel} devices={row.devicesPlaced}/{row.devicesDestroyed}/{row.devicesExpired}; real time {watch.Elapsed.TotalSeconds:F2} s");
                    Assert.AreNotEqual("none", row.winner, $"{profile.Name}/{kind}");
                    Assert.Less(watch.Elapsed.TotalSeconds, 30.0, $"{profile.Name}/{kind}");
                }
            }
        }

        [Test]
        public void ABotMatchOfTheSameSeedIsTheSameMatch()
        {
            var settings = new HarnessSettings { profile = "normal" };

            MatchRow a = MatchRunner.RunOne(settings, 9);
            MatchRow b = MatchRunner.RunOne(settings, 9);
            MatchRow c = MatchRunner.RunOne(settings, 10);

            Assert.AreEqual(JsonUtility.ToJson(a), JsonUtility.ToJson(b));
            Assert.AreNotEqual(a.stateHash, c.stateHash);
        }

        [Test]
        public void AnUnknownProfileIsRefused()
        {
            Assert.That(new HarnessSettings { profile = "godlike" }.Validate(), Does.Contain("unknown profile"));
        }
    }
}
