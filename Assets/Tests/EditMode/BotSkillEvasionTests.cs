using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class BotSkillEvasionTests
    {
        static readonly SkillProfile Exact = new SkillProfile { Name = "exact", AimErrorDegrees = 0f, ReactionSeconds = 0.3f, BossPreference = 0f };

        static MatchSession Start(byte kind = BossCatalog.Standard)
        {
            var settings = new HarnessSettings { bossKind = kind };
            settings.overrides.Add(new NamedValue { name = "mobAutoTarget", value = 0f });
            settings.overrides.Add(new NamedValue { name = "matchSeconds", value = 120f });
            return MatchSession.Start(settings, 1);
        }

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
            Vector2 far = new Vector2(-14f, -9f);
            foreach (ulong id in s.SurvivorIds)
            {
                if (System.Array.IndexOf(keep, id) >= 0) continue;
                Teleport(s, id, far);
                far += new Vector2(1.5f, 0f);
            }
        }

        [TearDown]
        public void RestoreTheStandardKind() => BossCatalog.SetCurrentKind(BossCatalog.Standard);

        static float AngleBetween(Vector2 a, Vector2 b) => Vector2.Angle(a, b);

        // ---- BossSkillSim.Evade ----

        [Test]
        public void NothingAnnouncedGivesNoPush()
        {
            MatchSession s = Start();
            Assert.AreEqual(Vector2.zero, s.World.BossSkills.Evade(s.PositionOf(MatchSession.BossId), CombatConfig.SurvivorRadius));
        }

        [Test]
        public void AnAnnouncedShockwavePushesAwayFromItsCentreInsideAndNotOutside()
        {
            MatchSession s = Start();
            Assert.AreEqual(BossSkillResult.Used, s.UseBossSkill(BossSkills.Shockwave, 0));
            Vector2 centre = s.PositionOf(MatchSession.BossId);
            BossSkillDef def = BossCatalog.Get(BossCatalog.Standard).Skills[BossSkills.Shockwave];

            Vector2 inside = centre + new Vector2(def.Radius * 0.5f, 0f);
            Vector2 push = s.World.BossSkills.Evade(inside, CombatConfig.SurvivorRadius);
            Assert.Greater(push.x, 0f, "away from the centre, to the right");
            Assert.AreEqual(0f, push.y, 0.001f);

            Vector2 outside = centre + new Vector2(def.Radius + CombatConfig.SurvivorRadius + 1f, 0f);
            Assert.AreEqual(Vector2.zero, s.World.BossSkills.Evade(outside, CombatConfig.SurvivorRadius));
        }

        [Test]
        public void AnAnnouncedBeamPushesAcrossItsLineToTheSideTheBodyIsOnAndNotBeyondItsEnd()
        {
            MatchSession s = Start(BossCatalog.Sniper);
            Vector2 start = s.PositionOf(MatchSession.BossId);
            ParkOthers(s, 1);
            Teleport(s, 1, start + new Vector2(10f, 0f)); // the nearest survivor stands along +x, where the targeted shot will aim
            Assert.AreEqual(BossSkillResult.Used, s.UseBossSkill(BossSkills.Shockwave, 0)); // the sniper's E skill is the beam, aimed by itself at the nearest survivor
            BossSkillDef def = BossCatalog.Get(BossCatalog.Sniper).Skills[1];
            Assert.AreEqual(BossSkillEffect.Beam, def.Effect);

            Vector2 above = start + new Vector2(def.Length * 0.5f, 0.1f);
            Vector2 push = s.World.BossSkills.Evade(above, CombatConfig.SurvivorRadius);
            Assert.AreEqual(0f, push.x, 0.001f, "across the line");
            Assert.Greater(push.y, 0f, "to the side the body is on");

            Vector2 below = start + new Vector2(def.Length * 0.5f, -0.1f);
            Assert.Less(s.World.BossSkills.Evade(below, CombatConfig.SurvivorRadius).y, 0f);

            Assert.AreEqual(Vector2.zero, s.World.BossSkills.Evade(start + new Vector2(def.Length + 3f, 0f), CombatConfig.SurvivorRadius), "beyond the end");
            Assert.AreEqual(Vector2.zero, s.World.BossSkills.Evade(start + new Vector2(-3f, 0f), CombatConfig.SurvivorRadius), "behind the start");
            Assert.AreEqual(Vector2.zero, s.World.BossSkills.Evade(start + new Vector2(5f, 3f), CombatConfig.SurvivorRadius), "far to the side");
        }

        // ---- the bot ----

        [Test]
        public void ABotInsideAnAnnouncedShockwaveWalksAwayFromItsCentre()
        {
            MatchSession s = Start();
            Vector2 centre = s.PositionOf(MatchSession.BossId);
            ParkOthers(s, 1);
            Teleport(s, 1, centre + new Vector2(1.5f, 0f));
            s.UseBossSkill(BossSkills.Shockwave, 0);

            new SurvivorBot(1, s.CharacterOf(1), Exact, 1).Decide(s, s.Step);

            Vector2 away = (s.PositionOf(1) - centre).normalized;
            Assert.Greater(Vector2.Dot(s.DirectionOf(1), away), 0.7f);
        }

        [Test]
        public void WithTheSwitchOffTheSameBotStandsStillInTheAnnouncedShockwaveAndWithItOnItWalksOut()
        {
            Vector2 DirectionWith(bool evade)
            {
                bool before = BotMath.EvadeSkills;
                try
                {
                    BotMath.EvadeSkills = evade;
                    MatchSession s = Start(BossCatalog.Giant);
                    Vector2 centre = s.PositionOf(MatchSession.BossId);
                    ParkOthers(s, 1);
                    Teleport(s, 1, centre + new Vector2(5f, 0f)); // inside the giant's radius 5.5, but beyond the bot's own boss-danger distance
                    s.UseBossSkill(0, 0);
                    new SurvivorBot(1, s.CharacterOf(1), Exact, 1).Decide(s, s.Step);
                    return s.DirectionOf(1);
                }
                finally { BotMath.EvadeSkills = before; }
            }

            Assert.AreEqual(0f, DirectionWith(false).magnitude, 0.001f, "ignores the announcement");
            Assert.Greater(DirectionWith(true).x, 0.5f, "walks away from the centre");
        }

        [Test]
        public void TheEvasionChangesNoGameRule()
        {
            MatchSession s = Start();
            BossSkillDef def = BossCatalog.Get(BossCatalog.Standard).Skills[BossSkills.Shockwave];
            float warning = def.WarningSeconds;
            int damage = def.Damage;
            float radius = def.Radius;

            bool before = BotMath.EvadeSkills;
            try
            {
                BotMath.EvadeSkills = true;
                var bot = new SurvivorBot(1, s.CharacterOf(1), Exact, 1);
                bot.Decide(s, s.Step);
            }
            finally { BotMath.EvadeSkills = before; }

            Assert.AreEqual(warning, def.WarningSeconds);
            Assert.AreEqual(damage, def.Damage);
            Assert.AreEqual(radius, def.Radius);
        }

        [Test]
        public void ABotThatEvadesTakesFarLessFromTheBossSkillsOverAWholeMatchStretch()
        {
            int Falls(bool evade)
            {
                bool before = BotMath.EvadeSkills;
                try
                {
                    BotMath.EvadeSkills = evade;
                    int total = 0;
                    for (int seed = 1; seed <= 6; seed++)
                    {
                        BossCatalog.SetCurrentKind(BossCatalog.Giant);
                        var settings = new HarnessSettings { bossKind = BossCatalog.Giant };
                        settings.overrides.Add(new NamedValue { name = "mobAutoTarget", value = 0f });
                        MatchSession s = MatchSession.Start(settings, seed);
                        var controller = new BotController(s, SkillProfiles.Normal);
                        for (int i = 0; i < 1500; i++) { controller.Decide(s, s.Step); s.Advance(); }
                        foreach (ulong id in s.SurvivorIds) if (!s.IsAlive(id)) total++;
                        BossCatalog.SetCurrentKind(BossCatalog.Standard);
                    }
                    return total;
                }
                finally { BotMath.EvadeSkills = before; }
            }

            Assert.LessOrEqual(Falls(true), Falls(false), "evading bots are not worse off in the first half minute");
        }
    }
}
