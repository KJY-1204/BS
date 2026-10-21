using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class SkillEffectScaleTests
    {
        const ulong A = 1, B = 2;
        const int Q = 0, E = 1;

        static SkillSim NewSkills()
        {
            var sim = new SkillSim();
            sim.Register(A, 1000, 100f);
            return sim;
        }

        static int[] Ranks(params (byte id, int rank)[] set)
        {
            var ranks = new int[SkillCatalog.MaxSkillId + 1];
            foreach ((byte id, int rank) in set) ranks[id] = rank;
            return ranks;
        }

        static SkillActivation Activate(byte character, int slot, int[] ranks)
        {
            Assert.AreEqual(SkillResult.Fired, NewSkills().TryActivate(A, character, slot, true, ranks, out SkillActivation activation));
            return activation;
        }

        static MobSim Mobs() => new MobSim(targetCount: 0) { MaxHealth = 100 };

        static ushort Spawn(MobSim mobs, float x, float y)
        {
            Assert.IsTrue(mobs.TrySpawn(new Vector2(x, y), 0, out ushort id));
            return id;
        }

        // ---- no passive: nothing changes ----

        [Test]
        public void WithoutPassivesEverySkillUsesItsDefinedValuesAndScalesOfOneHundred()
        {
            foreach (SkillDef def in SkillCatalog.All.Where(d => d.IsActive))
            {
                int[] ranks = Ranks((def.Id, def.MaxRank));
                SkillActivation a = Activate(def.Character, def.Slot, ranks);
                int r = def.MaxRank - 1;

                Assert.AreEqual(100, a.RangeScale, def.Name);
                Assert.AreEqual(100, a.DurationScale, def.Name);
                Assert.AreEqual(def.Radius != null ? def.Radius[r] : 0f, a.Radius, 1e-6f, def.Name);
                Assert.AreEqual(def.Length != null ? def.Length[r] : 0f, a.Length, 1e-6f, def.Name);
                Assert.AreEqual(def.HalfWidth != null ? def.HalfWidth[r] : 0f, a.HalfWidth, 1e-6f, def.Name);
                Assert.AreEqual(def.Distance != null ? def.Distance[r] : 0f, a.Distance, 1e-6f, def.Name);
                Assert.AreEqual(def.Duration != null ? def.Duration[r] : 0f, a.BuffSeconds, 1e-6f, def.Name);
            }
        }

        // ---- the values the passives scale ----

        [Test]
        public void TheBarrierLastsLongerWithTheDurationPassive()
        {
            SkillDef barrier = SkillCatalog.Get(SkillIds.Barrier);
            SkillActivation plain = Activate(CharacterIds.Reimu, E, Ranks((SkillIds.Barrier, 2)));
            SkillActivation longer = Activate(CharacterIds.Reimu, E, Ranks((SkillIds.Barrier, 2), (SkillIds.BarrierExtension, 2)));

            Assert.AreEqual(barrier.Duration[1], plain.BuffSeconds, 1e-6f);
            Assert.AreEqual(140, longer.DurationScale);
            Assert.AreEqual(100, longer.RangeScale);
            Assert.AreEqual(barrier.Duration[1] * 1.4f, longer.BuffSeconds, 1e-5f);
        }

        [Test]
        public void TheProtectionReallyLastsTheScaledTime()
        {
            var survivors = new SurvivorSim();
            survivors.Register(A);
            SkillActivation act = Activate(CharacterIds.Reimu, E, Ranks((SkillIds.Barrier, 1), (SkillIds.BarrierExtension, 3)));
            Assert.AreEqual(1, SkillExecutor.Apply(act, new SkillContext { CasterId = A, Survivors = survivors }));

            var bodies = new List<SurvivorBody> { new SurvivorBody { ClientId = A, Position = Vector2.zero } };
            MobSim mobs = Mobs();
            float baseSeconds = SkillCatalog.Get(SkillIds.Barrier).Duration[0];
            Assert.Greater(act.BuffSeconds, baseSeconds + 0.5f, "longer than the defined time");

            for (float t = 0f; t < baseSeconds + 0.3f; t += 0.02f) survivors.Step(0.02f, bodies, mobs);
            Assert.IsTrue(survivors.IsInvulnerable(A), "still protected after the defined time");

            for (float t = 0f; t < act.BuffSeconds - baseSeconds; t += 0.02f) survivors.Step(0.02f, bodies, mobs);
            Assert.IsFalse(survivors.IsInvulnerable(A), "protection ended after the scaled time");
        }

        [Test]
        public void WindVeilReachesFartherAndPushesFartherWithTheRangePassive()
        {
            SkillDef veil = SkillCatalog.Get(SkillIds.WindVeil);
            SkillActivation plain = Activate(CharacterIds.Sanae, E, Ranks((SkillIds.WindVeil, 2)));
            SkillActivation wide = Activate(CharacterIds.Sanae, E, Ranks((SkillIds.WindVeil, 2), (SkillIds.WideWind, 3)));

            Assert.AreEqual(145, wide.RangeScale);
            Assert.AreEqual(100, wide.DurationScale);
            Assert.AreEqual(veil.Radius[1] * 1.45f, wide.Radius, 1e-5f);
            Assert.AreEqual(veil.Distance[1] * 1.45f, wide.Distance, 1e-5f);
            Assert.Greater(wide.Radius, plain.Radius);
            Assert.Greater(wide.Distance, plain.Distance);

            // A mob just outside the defined radius is untouched by the plain skill and pushed by the wide one.
            float x = plain.Radius + 0.5f;
            MobSim a = Mobs();
            Spawn(a, x, 0);
            MobSim b = Mobs();
            Spawn(b, x, 0);

            Assert.AreEqual(0, SkillExecutor.Apply(plain, new SkillContext { CasterId = A, Origin = Vector2.zero, Mobs = a }));
            Assert.AreEqual(1, SkillExecutor.Apply(wide, new SkillContext { CasterId = A, Origin = Vector2.zero, Mobs = b }));
            Assert.AreEqual(x + wide.Distance, b.PositionAt(0).x, 1e-3f);
        }

        [Test]
        public void MiracleHealsFartherAlliesWithTheRangePassive()
        {
            SkillActivation plain = Activate(CharacterIds.Sanae, Q, Ranks((SkillIds.Miracle, 1)));
            SkillActivation wide = Activate(CharacterIds.Sanae, Q, Ranks((SkillIds.Miracle, 1), (SkillIds.WideWind, 3)));
            float x = plain.Radius + 0.5f;

            int Healed(SkillActivation act)
            {
                var survivors = new SurvivorSim();
                survivors.Register(A);
                survivors.Register(B);
                survivors.ApplyDamage(B, 70);
                var bodies = new List<GrowthBody>
                {
                    new GrowthBody { ClientId = A, Position = Vector2.zero, Alive = true },
                    new GrowthBody { ClientId = B, Position = new Vector2(x, 0), Alive = true },
                };
                SkillExecutor.Apply(act, new SkillContext { CasterId = A, Origin = Vector2.zero, Survivors = survivors, Bodies = bodies });
                return survivors.Health(B);
            }

            Assert.AreEqual(CombatConfig.SurvivorMaxHealth - 70, Healed(plain), "outside the defined radius");
            Assert.Greater(Healed(wide), CombatConfig.SurvivorMaxHealth - 70, "inside the widened radius");
            Assert.AreEqual(plain.Amount, wide.Amount, "healing amount is not a range");
        }

        // ---- scales only touch what they scale ----

        [Test]
        public void AScaleChangesNothingForASkillThatHasNoSuchValue()
        {
            // Reimu's burst has a radius but no duration; her duration passive must not change it.
            SkillActivation plain = Activate(CharacterIds.Reimu, Q, Ranks((SkillIds.Burst, 3)));
            SkillActivation withDuration = Activate(CharacterIds.Reimu, Q, Ranks((SkillIds.Burst, 3), (SkillIds.BarrierExtension, 3)));
            Assert.AreEqual(plain.Radius, withDuration.Radius, 1e-6f);
            Assert.AreEqual(plain.BuffSeconds, withDuration.BuffSeconds, 1e-6f);
            Assert.AreEqual(plain.Damage, withDuration.Damage);
        }

        [Test]
        public void OtherCharactersAreNotAffectedByTheTwoPassives()
        {
            int[] ranks = Ranks((SkillIds.MasterSpark, 3), (SkillIds.WideWind, 3), (SkillIds.BarrierExtension, 3));
            SkillActivation a = Activate(CharacterIds.Marisa, Q, ranks);
            Assert.AreEqual(100, a.RangeScale);
            Assert.AreEqual(100, a.DurationScale);
            Assert.AreEqual(SkillCatalog.Get(SkillIds.MasterSpark).Length[2], a.Length, 1e-6f);
        }

        [Test]
        public void EachUseGetsItsOwnScales()
        {
            var sim = NewSkills();
            int[] ranks = Ranks((SkillIds.Miracle, 1), (SkillIds.WindVeil, 1), (SkillIds.WideWind, 2));

            Assert.AreEqual(SkillResult.Fired, sim.TryActivate(A, CharacterIds.Sanae, Q, true, ranks, out SkillActivation first));
            ranks[SkillIds.WideWind] = 3;
            Assert.AreEqual(SkillResult.Fired, sim.TryActivate(A, CharacterIds.Sanae, E, true, ranks, out SkillActivation second));

            Assert.AreEqual(130, first.RangeScale);
            Assert.AreEqual(145, second.RangeScale);
        }

        [Test]
        public void ScaledGivesTheDefinedValueExactlyAtOneHundred()
        {
            Assert.AreEqual(2.5f, SkillCatalog.Scaled(2.5f, 100));
            Assert.AreEqual(3.25f, SkillCatalog.Scaled(2.5f, 130), 1e-6f);
        }

        // ---- the skill event carries the scales ----

        static CombatEvent SkillEvent(byte skill, byte range, byte duration, ushort angle = 7) => new CombatEvent
        {
            Kind = CombatEventKind.Skill, SkillId = skill, Rank = 2, Position = new Vector2(1.5f, -2f), Angle = angle,
            RangeScale = range, DurationScale = duration,
        };

        [Test]
        public void TheScalesSurviveEncodingAndDecoding()
        {
            foreach ((byte range, byte duration) in new[] { ((byte)100, (byte)100), ((byte)130, (byte)140), ((byte)250, (byte)100), ((byte)100, (byte)250) })
            {
                var buffer = new byte[64];
                var events = new List<CombatEvent> { SkillEvent(SkillIds.WindVeil, range, duration) };
                int length = CombatCodec.WriteBatch(buffer, 9u, events, 0, 1, null);
                Assert.AreEqual(CombatCodec.HeaderSize + CombatCodec.SkillSize, length);

                int offset = CombatCodec.HeaderSize;
                Assert.IsTrue(CombatCodec.TryReadEvent(buffer, length, ref offset, out CombatEvent read));
                Assert.AreEqual(range, read.RangeScale);
                Assert.AreEqual(duration, read.DurationScale);
                Assert.AreEqual(SkillIds.WindVeil, read.SkillId);
                Assert.AreEqual(2, read.Rank);
                Assert.AreEqual(7, read.Angle);
                Assert.AreEqual(length, offset, "the whole event was consumed");
            }
        }

        [Test]
        public void ASkillEventWithoutTheLastScaleByteIsRejected()
        {
            var buffer = new byte[64];
            int length = CombatCodec.WriteBatch(buffer, 9u, new List<CombatEvent> { SkillEvent(SkillIds.Barrier, 100, 160) }, 0, 1, null);
            int offset = CombatCodec.HeaderSize;
            Assert.IsFalse(CombatCodec.TryReadEvent(buffer, length - 1, ref offset, out _));
        }

        [Test]
        public void ManySkillEventsMixedWithShotsAreSplitIntoBatchesWithoutLossOrScaleMixups()
        {
            var events = new List<CombatEvent>();
            for (int i = 0; i < 120; i++)
            {
                events.Add(new CombatEvent { Kind = CombatEventKind.Fire, ProjectileId = (ushort)(i + 1), Slot = 1, Position = Vector2.zero, Angle = (ushort)(i * 100) });
                events.Add(SkillEvent(SkillIds.WindVeil, (byte)(100 + i % 100), (byte)(100 + (i * 3) % 100), (ushort)i));
            }

            var read = new List<CombatEvent>();
            var buffer = new byte[CombatCodec.MaxBatchBytes + 16];
            int start = 0;
            while (start < events.Count)
            {
                int fit = CombatCodec.CountThatFit(events, start);
                Assert.Greater(fit, 0);
                int length = CombatCodec.WriteBatch(buffer, 5u, events, start, fit, null);
                Assert.LessOrEqual(length, CombatCodec.MaxBatchBytes + CombatCodec.HeaderSize);
                int offset = CombatCodec.HeaderSize;
                for (int i = 0; i < fit; i++)
                {
                    Assert.IsTrue(CombatCodec.TryReadEvent(buffer, length, ref offset, out CombatEvent e));
                    read.Add(e);
                }
                start += fit;
            }

            Assert.AreEqual(events.Count, read.Count);
            for (int i = 0; i < events.Count; i++)
            {
                Assert.AreEqual(events[i].Kind, read[i].Kind);
                if (events[i].Kind != CombatEventKind.Skill) continue;
                Assert.AreEqual(events[i].RangeScale, read[i].RangeScale, $"event {i}");
                Assert.AreEqual(events[i].DurationScale, read[i].DurationScale, $"event {i}");
            }
        }

        [Test]
        public void TheBossWarningEventCarriesScalesOfOneHundred()
        {
            var sim = new BossSkillSim();
            var events = new List<CombatEvent>();
            var state = new BossSkillState { MatchRunning = true, BossStanding = true, BossPosition = new Vector2(1, 1), Aim = 100, HealthFraction = 1f };
            Assert.AreEqual(BossSkillResult.Used, sim.TryUse(BossSkills.Shockwave, true, state, 0, events));
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(100, events[0].RangeScale);
            Assert.AreEqual(100, events[0].DurationScale);
        }

        // ---- what the client draws ----

        [Test]
        public void TheDrawnSizesAreTheDefinedValuesAtOneHundredAndScaledOtherwise()
        {
            SkillDef veil = SkillCatalog.Get(SkillIds.WindVeil);
            Assert.AreEqual(veil.Radius[2], SkillEffectSize.Radius(veil, 3, 100), 1e-6f);
            Assert.AreEqual(veil.Radius[2], SkillEffectSize.Radius(veil, 3, 0), 1e-6f, "no scale given counts as none");
            Assert.AreEqual(veil.Radius[2] * 1.4f, SkillEffectSize.Radius(veil, 3, 140), 1e-5f);

            SkillDef spark = SkillCatalog.Get(SkillIds.MasterSpark);
            Assert.AreEqual(spark.Length[1] * 1.5f, SkillEffectSize.Length(spark, 2, 150), 1e-5f);
            Assert.AreEqual(spark.HalfWidth[1] * 2f * 1.5f, SkillEffectSize.Width(spark, 2, 150), 1e-5f);

            SkillDef barrier = SkillCatalog.Get(SkillIds.Barrier);
            Assert.AreEqual(barrier.Duration[0] * 1.2f, SkillEffectSize.Seconds(barrier, 1, 120), 1e-5f);
            Assert.AreEqual(0f, SkillEffectSize.Seconds(veil, 1, 120), "a skill with no duration");
            Assert.AreEqual(0f, SkillEffectSize.Radius(barrier, 1, 140), "a skill with no radius");
        }

        [Test]
        public void TheClientDrawsExactlyWhatTheServerJudged()
        {
            foreach (SkillDef def in SkillCatalog.All.Where(d => d.IsActive))
            {
                int[] ranks = Ranks((def.Id, def.MaxRank), (SkillIds.WideWind, 3), (SkillIds.BarrierExtension, 3));
                SkillActivation act = Activate(def.Character, def.Slot, ranks);

                Assert.AreEqual(act.Radius, SkillEffectSize.Radius(def, act.Rank, act.RangeScale), 1e-6f, def.Name);
                Assert.AreEqual(act.Length, SkillEffectSize.Length(def, act.Rank, act.RangeScale), 1e-6f, def.Name);
                Assert.AreEqual(act.HalfWidth * 2f, SkillEffectSize.Width(def, act.Rank, act.RangeScale), 1e-6f, def.Name);
                Assert.AreEqual(act.BuffSeconds, SkillEffectSize.Seconds(def, act.Rank, act.DurationScale), 1e-6f, def.Name);
            }
        }

        [Test]
        public void TheReplayKeepsTheScalesUntilTheEffectIsDue()
        {
            var replay = new SkillEffectReplay();
            replay.OnSkill(new SkillEffect { SkillId = SkillIds.WindVeil, Slot = 2, Position = Vector2.one, Rank = 2, RangeScale = 130, DurationScale = 140, ServerTime = 5f });
            replay.Advance(6f);
            var due = new List<SkillEffect>();
            replay.DrainDue(due);

            Assert.AreEqual(1, due.Count);
            Assert.AreEqual(130, due[0].RangeScale);
            Assert.AreEqual(140, due[0].DurationScale);
        }

        // ---- Marisa's beam and Sakuya's time stop ----

        [Test]
        public void TheBeamIsLongerAndWiderWithCritMagicAndTheOtherMarisaSkillIsNot()
        {
            SkillDef spark = SkillCatalog.Get(SkillIds.MasterSpark);
            SkillActivation plain = Activate(CharacterIds.Marisa, Q, Ranks((SkillIds.MasterSpark, 3)));
            SkillActivation wide = Activate(CharacterIds.Marisa, Q, Ranks((SkillIds.MasterSpark, 3), (SkillIds.CritMagic, 3)));

            Assert.AreEqual(145, wide.RangeScale);
            Assert.AreEqual(100, wide.DurationScale);
            Assert.AreEqual(spark.Length[2], plain.Length, 1e-6f);
            Assert.AreEqual(spark.Length[2] * 1.45f, wide.Length, 1e-4f);
            Assert.AreEqual(spark.HalfWidth[2] * 1.45f, wide.HalfWidth, 1e-5f);
            Assert.AreEqual(plain.Damage, wide.Damage, "range is not damage");

            SkillDef dust = SkillCatalog.Get(SkillIds.RapidFire);
            SkillActivation boost = Activate(CharacterIds.Marisa, E, Ranks((SkillIds.RapidFire, 3), (SkillIds.CritMagic, 3)));
            Assert.AreEqual(dust.Duration[2], boost.BuffSeconds, 1e-6f, "the attack speed boost keeps its time");
        }

        [Test]
        public void TheBeamReallyHitsMobsBeyondTheDefinedLengthAndWidth()
        {
            SkillActivation plain = Activate(CharacterIds.Marisa, Q, Ranks((SkillIds.MasterSpark, 3)));
            SkillActivation wide = Activate(CharacterIds.Marisa, Q, Ranks((SkillIds.MasterSpark, 3), (SkillIds.CritMagic, 3)));
            ushort aim = AimMath.QuantizeAngle(0f);

            float far = plain.Length + 1.5f;           // beyond the defined length, inside the widened one
            float beside = plain.HalfWidth + 0.15f;    // beside the defined width, inside the widened one
            var points = new[] { new Vector2(far, 0f), new Vector2(3f, beside) };

            foreach ((SkillActivation act, int expected) in new[] { (plain, 0), (wide, 2) })
            {
                MobSim mobs = Mobs();
                foreach (Vector2 p in points) Spawn(mobs, p.x, p.y);
                int hit = SkillExecutor.Apply(act, new SkillContext { CasterId = A, Origin = Vector2.zero, AimAngle = aim, Mobs = mobs });
                Assert.AreEqual(expected, hit, $"scale {act.RangeScale}");
            }
        }

        [Test]
        public void TheTimeStopLastsLongerWithTimeRuleAndTheKnivesAreUnchanged()
        {
            SkillDef stop = SkillCatalog.Get(SkillIds.TimeStop);
            SkillActivation plain = Activate(CharacterIds.Sakuya, Q, Ranks((SkillIds.TimeStop, 3)));
            SkillActivation long3 = Activate(CharacterIds.Sakuya, Q, Ranks((SkillIds.TimeStop, 3), (SkillIds.TimeRule, 3)));

            Assert.AreEqual(145, long3.DurationScale);
            Assert.AreEqual(100, long3.RangeScale);
            Assert.AreEqual(stop.Duration[2], plain.BuffSeconds, 1e-6f);
            Assert.AreEqual(stop.Duration[2] * 1.45f, long3.BuffSeconds, 1e-4f);

            SkillDef knives = SkillCatalog.Get(SkillIds.KnifeBarrage);
            SkillActivation ring = Activate(CharacterIds.Sakuya, E, Ranks((SkillIds.KnifeBarrage, 3), (SkillIds.TimeRule, 3)));
            Assert.AreEqual(knives.Count[2], ring.Count, "the knife count is not a duration");
            Assert.AreEqual(knives.Damage[2], ring.Damage);
        }

        [Test]
        public void MobsReallyStandStillForTheScaledTime()
        {
            SkillActivation plain = Activate(CharacterIds.Sakuya, Q, Ranks((SkillIds.TimeStop, 1)));
            SkillActivation longer = Activate(CharacterIds.Sakuya, Q, Ranks((SkillIds.TimeStop, 1), (SkillIds.TimeRule, 3)));

            float StillSeconds(SkillActivation act)
            {
                var mobs = new MobSim(targetCount: 0) { MaxHealth = 100 };
                Assert.IsTrue(mobs.TrySpawn(new Vector2(6f, 0f), 0, out _));
                SkillExecutor.Apply(act, new SkillContext { CasterId = A, Mobs = mobs });
                var nobody = new List<SurvivorInfo>();
                float t = 0f;
                while (mobs.IsFrozen && t < 20f)
                {
                    mobs.Step(0.02f, nobody);
                    t += 0.02f;
                }
                return t;
            }

            float a = StillSeconds(plain);
            float b = StillSeconds(longer);
            Assert.AreEqual(plain.BuffSeconds, a, 0.1f);
            Assert.AreEqual(longer.BuffSeconds, b, 0.1f);
            Assert.Greater(b, a + 0.5f);
        }
    }
}
