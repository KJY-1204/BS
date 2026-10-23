using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class UltimateSkillEffectTests
    {
        const ulong A = 1, B = 2, C = 3;
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

        static SkillActivation Activate(SkillSim sim, byte character, int slot, int[] ranks)
        {
            Assert.AreEqual(SkillResult.Fired, sim.TryActivate(A, character, slot, true, ranks, out SkillActivation activation));
            return activation;
        }

        static SkillActivation Fresh(byte character, int slot, params (byte id, int rank)[] ranks) =>
            Activate(NewSkills(), character, slot, Ranks(ranks));

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

        static BossSim Boss(float x, float y, float radius = 0.9f) =>
            new BossSim(new BossDef { Name = "t", MaxHealth = 100000, MoveSpeed = 6f, Radius = radius, ContactDamage = 25, MatchSeconds = 60f }, new Vector2(x, y));

        // ---- Reimu: the large area burst ----

        [Test]
        public void ReimusRBurstReachesMobsTheQBurstDoesNot()
        {
            SkillActivation q = Fresh(CharacterIds.Reimu, Q, (SkillIds.Burst, 1));
            SkillActivation r = Fresh(CharacterIds.Reimu, R, (SkillIds.FantasyHeaven, 1));
            Assert.AreEqual(SkillCatalog.Get(SkillIds.FantasyHeaven).Radius[0], r.Radius);

            MobSim viaQ = Mobs(), viaR = Mobs();
            float between = (q.Radius + r.Radius) * 0.5f;
            float beyond = r.Radius + 1f;
            ushort q1 = Spawn(viaQ, between, 0), q2 = Spawn(viaQ, beyond, 0);
            ushort r1 = Spawn(viaR, between, 0), r2 = Spawn(viaR, beyond, 0);

            SkillExecutor.Apply(q, new SkillContext { CasterId = A, Mobs = viaQ });
            SkillExecutor.Apply(r, new SkillContext { CasterId = A, Mobs = viaR });

            Assert.AreEqual(1000, Health(viaQ, q1), "outside the Q burst");
            Assert.AreEqual(1000 - r.Damage, Health(viaR, r1), "inside the R burst only");
            Assert.AreEqual(1000, Health(viaR, r2), "beyond the R burst");
            Assert.AreEqual(1000, Health(viaQ, q2));
        }

        [Test]
        public void ReimusRBurstHitsHarderThanTheQBurstOnAMobBothReach()
        {
            SkillActivation q = Fresh(CharacterIds.Reimu, Q, (SkillIds.Burst, 1));
            SkillActivation r = Fresh(CharacterIds.Reimu, R, (SkillIds.FantasyHeaven, 1));
            MobSim viaQ = Mobs(), viaR = Mobs();
            ushort a = Spawn(viaQ, 1, 0), b = Spawn(viaR, 1, 0);

            SkillExecutor.Apply(q, new SkillContext { CasterId = A, Mobs = viaQ });
            SkillExecutor.Apply(r, new SkillContext { CasterId = A, Mobs = viaR });

            Assert.Less(Health(viaR, b), Health(viaQ, a));
        }

        [Test]
        public void ReimusRBurstKillsReportWhereMobsDiedAndHurtTheBoss()
        {
            SkillActivation r = Fresh(CharacterIds.Reimu, R, (SkillIds.FantasyHeaven, 3));
            MobSim mobs = Mobs(10);
            Spawn(mobs, 3, 0);
            Spawn(mobs, -4, 2);
            var deaths = new List<Vector2>();
            BossSim boss = Boss(r.Radius + 0.5f, 0f);

            int touched = SkillExecutor.Apply(r, new SkillContext { CasterId = A, Mobs = mobs, Boss = boss, Deaths = deaths });

            Assert.AreEqual(2, deaths.Count);
            Assert.AreEqual(3, touched, "two mobs and the boss");
            Assert.AreEqual(100000 - r.Damage, boss.Health);
        }

        // ---- Marisa: the long, wide beam ----

        [Test]
        public void MarisasRBeamIsWiderAndLongerThanTheQBeam()
        {
            SkillActivation q = Fresh(CharacterIds.Marisa, Q, (SkillIds.MasterSpark, 1));
            SkillActivation r = Fresh(CharacterIds.Marisa, R, (SkillIds.FinalSpark, 1));
            MobSim viaQ = Mobs(), viaR = Mobs();
            // Along the arena's long side, so no mob is pulled in by the arena edge.
            Vector2 origin = new Vector2(-15f, 0f);
            float side = (q.HalfWidth + r.HalfWidth) * 0.5f;
            float far = (q.Length + r.Length) * 0.5f;
            ushort qSide = Spawn(viaQ, origin.x + 5f, side), rSide = Spawn(viaR, origin.x + 5f, side);
            ushort qFar = Spawn(viaQ, origin.x + far, 0), rFar = Spawn(viaR, origin.x + far, 0);

            SkillExecutor.Apply(q, new SkillContext { CasterId = A, Origin = origin, AimAngle = AngleDegrees(0), Mobs = viaQ });
            SkillExecutor.Apply(r, new SkillContext { CasterId = A, Origin = origin, AimAngle = AngleDegrees(0), Mobs = viaR });

            Assert.AreEqual(1000, Health(viaQ, qSide), "beside the Q beam");
            Assert.AreEqual(1000, Health(viaQ, qFar), "beyond the Q beam");
            Assert.AreEqual(1000 - r.Damage, Health(viaR, rSide));
            Assert.AreEqual(1000 - r.Damage, Health(viaR, rFar));
        }

        [Test]
        public void MarisasRBeamDoesNotStopAtTheFirstMobAndHurtsTheBossOnTheLine()
        {
            SkillActivation r = Fresh(CharacterIds.Marisa, R, (SkillIds.FinalSpark, 2));
            MobSim mobs = Mobs();
            ushort[] row = { Spawn(mobs, 0, 2), Spawn(mobs, 0.3f, 6), Spawn(mobs, -0.3f, 10), Spawn(mobs, 0, 14) };
            ushort behind = Spawn(mobs, 0, -3);
            BossSim boss = Boss(0f, r.Length + 0.5f); // the body overlaps the end of the line

            int touched = SkillExecutor.Apply(r, new SkillContext { CasterId = A, AimAngle = AngleDegrees(90), Mobs = mobs, Boss = boss });

            Assert.AreEqual(5, touched, "four mobs and the boss");
            foreach (ushort id in row) Assert.AreEqual(1000 - r.Damage, Health(mobs, id));
            Assert.AreEqual(1000, Health(mobs, behind));
            Assert.AreEqual(100000 - r.Damage, boss.Health);
        }

        // ---- Sakuya: the large ring of knives ----

        [Test]
        public void SakuyasRRingFiresMoreKnivesThanTheERingEvenlySpread()
        {
            SkillActivation e = Fresh(CharacterIds.Sakuya, E, (SkillIds.KnifeBarrage, 1));
            SkillActivation r = Fresh(CharacterIds.Sakuya, R, (SkillIds.KillingDoll, 1));
            Assert.Greater(r.Count, e.Count);
            Assert.Greater(r.Damage, e.Damage);

            var projectiles = new ProjectileSim();
            ProjectileShape shape = CharacterCatalog.Get(CharacterIds.Sakuya).Attack.Shape;
            int fired = SkillExecutor.Apply(r, new SkillContext { CasterId = A, Origin = new Vector2(1, 2), AimAngle = 0, Projectiles = projectiles, Shape = shape });

            Assert.AreEqual(r.Count, fired);
            var events = new List<CombatEvent>();
            projectiles.DrainEvents(events);
            int[] angles = events.Where(ev => ev.Kind == CombatEventKind.Fire).Select(ev => (int)ev.Angle).OrderBy(a => a).ToArray();
            Assert.AreEqual(r.Count, angles.Length);
            for (int i = 1; i < angles.Length; i++) Assert.AreEqual(65536.0 / r.Count, angles[i] - angles[i - 1], 1.5, "even steps");
            Assert.IsTrue(events.All(ev => ev.Position == new Vector2(1, 2) && ev.Owner == A));
        }

        [Test]
        public void EveryKnifeThatReachesAMobHitsForTheSkillsDamage()
        {
            SkillActivation r = Fresh(CharacterIds.Sakuya, R, (SkillIds.KillingDoll, 2));
            var projectiles = new ProjectileSim();
            ProjectileShape shape = CharacterCatalog.Get(CharacterIds.Sakuya).Attack.Shape;
            MobSim mobs = Mobs();
            Spawn(mobs, 1.5f, 0);
            SkillExecutor.Apply(r, new SkillContext { CasterId = A, AimAngle = 0, Projectiles = projectiles, Shape = shape });

            for (int i = 0; i < 20; i++) projectiles.Step(0.02f, mobs);

            mobs.TryGetHealth(mobs.IdAt(0), out int health);
            int lost = 1000 - health;
            Assert.Greater(lost, 0, "the dense ring reaches a mob next to the caster");
            Assert.AreEqual(0, lost % r.Damage, "every knife that hit did the skill's damage");
        }

        [Test]
        public void WhenTheCapLeavesRoomForFewerKnivesThoseFireAndTheRSkillStillCosts()
        {
            SkillSim skills = NewSkills();
            float manaBefore = skills.Mana(A);
            SkillActivation r = Activate(skills, CharacterIds.Sakuya, R, Ranks((SkillIds.KillingDoll, 3)));
            var projectiles = new ProjectileSim(7);

            int fired = SkillExecutor.Apply(r, new SkillContext { CasterId = A, Projectiles = projectiles, Shape = ProjectileShape.Default });

            Assert.AreEqual(7, fired);
            Assert.AreEqual(manaBefore - SkillCatalog.Get(SkillIds.KillingDoll).ManaCost, skills.Mana(A), 1e-3f);
            Assert.Greater(skills.CooldownLeft(A, R), 0f);
        }

        // ---- Sanae: the wide, strong heal ----

        static SurvivorSim HurtSurvivors(params ulong[] ids)
        {
            var survivors = new SurvivorSim();
            foreach (ulong id in ids) { survivors.Register(id, 200); survivors.ApplyDamage(id, 150); }
            return survivors;
        }

        [Test]
        public void SanaesRHealReachesSurvivorsTheQHealDoesNot()
        {
            SkillActivation q = Fresh(CharacterIds.Sanae, Q, (SkillIds.Miracle, 1));
            SkillActivation r = Fresh(CharacterIds.Sanae, R, (SkillIds.GreatMiracle, 1));
            Assert.Greater(r.Radius, q.Radius);
            float distance = (q.Radius + r.Radius) * 0.5f;
            var bodies = new List<GrowthBody>
            {
                new GrowthBody { ClientId = A, Position = Vector2.zero, Alive = true },
                new GrowthBody { ClientId = B, Position = new Vector2(distance, 0), Alive = true },
            };

            SurvivorSim viaQ = HurtSurvivors(A, B), viaR = HurtSurvivors(A, B);
            SkillExecutor.Apply(q, new SkillContext { CasterId = A, Survivors = viaQ, Bodies = bodies });
            SkillExecutor.Apply(r, new SkillContext { CasterId = A, Survivors = viaR, Bodies = bodies });

            Assert.AreEqual(50, viaQ.Health(B), "outside the Q radius");
            Assert.AreEqual(50 + r.Amount, viaR.Health(B), "inside the R radius");
            Assert.Greater(viaR.Health(A), viaQ.Health(A), "heals the caster more too");
        }

        [Test]
        public void SanaesRHealDoesNotHealOrReviveAFallenSurvivor()
        {
            SkillActivation r = Fresh(CharacterIds.Sanae, R, (SkillIds.GreatMiracle, 3));
            SurvivorSim survivors = HurtSurvivors(A);
            survivors.Register(C, 200);
            survivors.Kill(C);
            var bodies = new List<GrowthBody>
            {
                new GrowthBody { ClientId = A, Position = Vector2.zero, Alive = true },
                new GrowthBody { ClientId = C, Position = new Vector2(2, 0), Alive = false },
            };

            int healed = SkillExecutor.Apply(r, new SkillContext { CasterId = A, Survivors = survivors, Bodies = bodies });

            Assert.AreEqual(1, healed);
            Assert.IsFalse(survivors.IsAlive(C));
            Assert.AreEqual(0, survivors.Health(C));
        }

        // ---- The rules every skill follows ----

        [Test]
        public void ARefusedRPressChangesNeitherManaNorCooldown()
        {
            int[] ranks = Ranks((SkillIds.FantasyHeaven, 1));
            SkillDef def = SkillCatalog.Get(SkillIds.FantasyHeaven);

            SkillSim notLearned = NewSkills();
            Assert.AreEqual(SkillResult.NotLearned, notLearned.TryActivate(A, CharacterIds.Reimu, R, true, Ranks(), out _));
            Assert.AreEqual(1000f, notLearned.Mana(A));
            Assert.AreEqual(0f, notLearned.CooldownLeft(A, R));

            SkillSim dead = NewSkills();
            Assert.AreEqual(SkillResult.Dead, dead.TryActivate(A, CharacterIds.Reimu, R, false, ranks, out _));
            Assert.AreEqual(1000f, dead.Mana(A));

            SkillSim poor = NewSkills(def.ManaCost - 1);
            Assert.AreEqual(SkillResult.NoMana, poor.TryActivate(A, CharacterIds.Reimu, R, true, ranks, out _));
            Assert.AreEqual(def.ManaCost - 1, poor.Mana(A), 1e-4f);
            Assert.AreEqual(0f, poor.CooldownLeft(A, R));

            SkillSim cooling = NewSkills();
            Activate(cooling, CharacterIds.Reimu, R, ranks);
            float mana = cooling.Mana(A), cooldown = cooling.CooldownLeft(A, R);
            Assert.AreEqual(SkillResult.Cooldown, cooling.TryActivate(A, CharacterIds.Reimu, R, true, ranks, out _));
            Assert.AreEqual(mana, cooling.Mana(A), 1e-4f);
            Assert.AreEqual(cooldown, cooling.CooldownLeft(A, R), 1e-4f);
        }

        [Test]
        public void AnotherCharactersRSkillIsNotFiredByTheRKey()
        {
            // The slot belongs to the survivor's own character: Reimu's ranks for Marisa's skill fire nothing.
            SkillSim sim = NewSkills();
            Assert.AreEqual(SkillResult.NotLearned, sim.TryActivate(A, CharacterIds.Reimu, R, true, Ranks((SkillIds.FinalSpark, 3)), out _));
        }

        [Test]
        public void TheRCooldownDoesNotBlockQAndEAndTheOtherWayRound()
        {
            SkillSim sim = NewSkills();
            int[] ranks = Ranks((SkillIds.Burst, 1), (SkillIds.Barrier, 1), (SkillIds.FantasyHeaven, 1));

            Activate(sim, CharacterIds.Reimu, R, ranks);
            Assert.Greater(sim.CooldownLeft(A, R), 0f);
            Assert.AreEqual(0f, sim.CooldownLeft(A, Q));
            Assert.AreEqual(0f, sim.CooldownLeft(A, E));

            Activate(sim, CharacterIds.Reimu, Q, ranks);
            Activate(sim, CharacterIds.Reimu, E, ranks);
            Assert.AreEqual(SkillResult.Cooldown, sim.TryActivate(A, CharacterIds.Reimu, R, true, ranks, out _));
        }

        [Test]
        public void TheCharactersPassivesApplyToTheRSkillLikeToQAndE()
        {
            // Marisa: power raises the damage, the range passive lengthens and widens the beam.
            int[] plainRanks = Ranks((SkillIds.FinalSpark, 2));
            int[] passiveRanks = Ranks((SkillIds.FinalSpark, 2), (SkillIds.FirePower, 3), (SkillIds.CritMagic, 3));
            SkillActivation plain = Activate(NewSkills(), CharacterIds.Marisa, R, plainRanks);
            SkillActivation boosted = Activate(NewSkills(), CharacterIds.Marisa, R, passiveRanks);
            SkillDef def = SkillCatalog.Get(SkillIds.FinalSpark);

            Assert.AreEqual(def.Damage[1], plain.Damage);
            Assert.AreEqual(SkillCatalog.EffectivePower(def.Damage[1], passiveRanks, CharacterIds.Marisa), boosted.Damage);
            Assert.Greater(boosted.Damage, plain.Damage);
            Assert.AreEqual(def.Length[1] * boosted.RangeScale / 100f, boosted.Length, 1e-4f);
            Assert.Greater(boosted.Length, plain.Length);
            Assert.Greater(boosted.HalfWidth, plain.HalfWidth);
        }

        [Test]
        public void TheDevelopmentGrantTeachesTheRSkillToo_WhateverItsPrerequisite()
        {
            foreach (byte c in new[] { CharacterIds.Reimu, CharacterIds.Marisa, CharacterIds.Sakuya, CharacterIds.Sanae })
            {
                var leveling = new LevelingSim(new System.Random(1));
                leveling.Register(A, c);

                GrowthServer.GrantSkills(leveling, A, c, 2, 0);

                foreach (int slot in new[] { Q, E, R })
                    Assert.AreEqual(2, leveling.RankOf(A, SkillCatalog.ForSlot(slot, c).Id), $"character {c} slot {slot}");
            }
        }

        [Test]
        public void TheDevelopmentGrantCutsTheRankToTheMaximumAndCanAddThePassives()
        {
            var leveling = new LevelingSim(new System.Random(1));
            leveling.Register(A, CharacterIds.Marisa);

            GrowthServer.GrantSkills(leveling, A, CharacterIds.Marisa, 9, 9);

            SkillDef r = SkillCatalog.ForSlot(R, CharacterIds.Marisa);
            Assert.AreEqual(r.MaxRank, leveling.RankOf(A, r.Id));
            Assert.AreEqual(SkillCatalog.Get(SkillIds.FirePower).MaxRank, leveling.RankOf(A, SkillIds.FirePower));
            Assert.AreEqual(0, leveling.RankOf(A, SkillIds.SpiritSaving), "another character's passive");
        }

        // ---- input and display ----

        [Test]
        public void TheAutoSkillKeysNameQEAndRInAnyCombination()
        {
            Assert.AreEqual(1, DevArgs.SkillKeyMask("q"));
            Assert.AreEqual(2, DevArgs.SkillKeyMask("e"));
            Assert.AreEqual(4, DevArgs.SkillKeyMask("r"));
            Assert.AreEqual(3, DevArgs.SkillKeyMask("qe"), "qe still means Q and E");
            Assert.AreEqual(7, DevArgs.SkillKeyMask("qer"));
            Assert.AreEqual(5, DevArgs.SkillKeyMask("RQ"));
            Assert.AreEqual(1, DevArgs.SkillKeyMask("x"), "anything else meant Q before and still does");
            Assert.AreEqual(1, DevArgs.SkillKeyMask(null));
            Assert.AreEqual(0, DevArgs.AutoSkillKeys, "not given in the test run");
        }

        [Test]
        public void TheSkillBarHasAKeyForEverySlotAndIsCentred()
        {
            Assert.AreEqual("Q", GrowthHud.KeyLabel(0));
            Assert.AreEqual("E", GrowthHud.KeyLabel(1));
            Assert.AreEqual("R", GrowthHud.KeyLabel(SkillCatalog.UltimateSlot));
            Assert.AreEqual("?", GrowthHud.KeyLabel(SkillCatalog.SkillSlots));

            float width = 1280f;
            float left = GrowthHud.SlotLeft(width, 0);
            float right = GrowthHud.SlotLeft(width, SkillCatalog.SkillSlots - 1) + 84f;
            Assert.AreEqual(width * 0.5f, (left + right) * 0.5f, 1e-3f, "centred");
            for (int slot = 1; slot < SkillCatalog.SkillSlots; slot++)
                Assert.Greater(GrowthHud.SlotLeft(width, slot), GrowthHud.SlotLeft(width, slot - 1) + 84f, "slots do not overlap");
        }

        [Test]
        public void TheCostAndCooldownPassivesApplyToTheRSkill()
        {
            SkillDef def = SkillCatalog.Get(SkillIds.FantasyHeaven);
            int[] ranks = Ranks((SkillIds.FantasyHeaven, 1), (SkillIds.SpiritSaving, 3));
            SkillSim sim = NewSkills();

            sim.TryActivate(A, CharacterIds.Reimu, R, true, ranks, out SkillActivation act);

            int cost = SkillCatalog.EffectiveManaCost(def, ranks, CharacterIds.Reimu);
            Assert.Less(cost, def.ManaCost);
            Assert.AreEqual(1000f - cost, sim.Mana(A), 1e-3f);

            int[] timeSense = Ranks((SkillIds.KillingDoll, 1), (SkillIds.TimeSense, 3));
            SkillActivation faster = Activate(NewSkills(), CharacterIds.Sakuya, R, timeSense);
            Assert.AreEqual(SkillCatalog.EffectiveCooldown(SkillCatalog.Get(SkillIds.KillingDoll), timeSense, CharacterIds.Sakuya), faster.Cooldown, 1e-4f);
            Assert.Less(faster.Cooldown, SkillCatalog.Get(SkillIds.KillingDoll).Cooldown);
        }
    }
}
