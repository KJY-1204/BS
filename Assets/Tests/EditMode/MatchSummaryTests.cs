using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class MatchSummaryTests
    {
        const float Dt = 0.02f;
        static readonly byte DeviceType = MobKinds.Encode(MobKinds.OriginSummoned, MobKinds.Device);

        static MatchWorld NewWorld(bool withBoss = true)
        {
            var world = new MatchWorld(new MatchWorldOptions { MobTarget = 0 }) { MatchRunning = true };
            world.Clock.Start(withBoss, 900f);
            if (withBoss)
            {
                BossDef def = BossCatalog.Get(BossCatalog.Standard);
                world.Boss.Reset(def, new Vector2(10f, 10f));
                world.BossOwnerId = 9;
                world.Slots[9] = RoleTable.BossRole;
            }
            return world;
        }

        static void AddSurvivor(MatchWorld world, ulong id, byte role, byte character)
        {
            world.Survivors.Register(id, 100);
            world.Slots[id] = role;
            world.StartGrowth(id, character);
        }

        static void Leave(MatchWorld world, ulong id)
        {
            world.Survivors.Unregister(id);
            world.StopGrowth(id);
            world.Slots.Remove(id);
        }

        [Test]
        public void ListsEverySurvivorInRoleOrderWithItsNumbers()
        {
            MatchWorld world = NewWorld();
            AddSurvivor(world, 5, 3, CharacterIds.Sakuya);
            AddSurvivor(world, 6, 1, CharacterIds.Reimu);
            world.Leveling.AddXp(6, 100000);
            world.Survivors.Kill(5);

            MatchSummary s = world.BuildSummary();

            Assert.AreEqual(2, s.Survivors.Count);
            Assert.AreEqual(1, s.Survivors[0].Role);
            Assert.AreEqual(CharacterIds.Reimu, s.Survivors[0].Character);
            Assert.AreEqual(world.Leveling.Level(6), s.Survivors[0].Level);
            Assert.IsTrue(s.Survivors[0].Alive);
            Assert.AreEqual(3, s.Survivors[1].Role);
            Assert.AreEqual(CharacterIds.Sakuya, s.Survivors[1].Character);
            Assert.AreEqual(1, s.Survivors[1].Falls);
            Assert.AreEqual(0, s.Survivors[1].Revives);
            Assert.IsFalse(s.Survivors[1].Alive);
        }

        [Test]
        public void CarriesTheBossAndTheDeviceCounters()
        {
            MatchWorld world = NewWorld();
            AddSurvivor(world, 5, 1, CharacterIds.Reimu);
            world.Boss.TakeDamage(1234, out _, out _);
            for (int i = 0; i < 3; i++) Assert.IsTrue(world.Mobs.TrySpawn(new Vector2(i, 0), DeviceType, out _));

            MatchSummary s = world.BuildSummary();

            Assert.AreEqual(world.Boss.MaxHealth, s.BossMaxHealth);
            Assert.AreEqual(world.Boss.MaxHealth - 1234, s.BossHealth);
            Assert.AreEqual(3, s.DevicesPlaced);
            Assert.AreEqual(0, s.DevicesDestroyed);
        }

        [Test]
        public void LengthIsTheTimeLimitLessTheTimeLeft()
        {
            MatchWorld world = NewWorld();
            AddSurvivor(world, 5, 1, CharacterIds.Reimu);
            int steps = Mathf.RoundToInt(754f / Dt);
            for (int i = 0; i < steps; i++) world.StepClock(Dt);

            Assert.AreEqual(754, world.BuildSummary().LengthSeconds);
        }

        [Test]
        public void ASurvivorWhoLeftIsNotListed()
        {
            MatchWorld world = NewWorld();
            AddSurvivor(world, 5, 1, CharacterIds.Reimu);
            AddSurvivor(world, 6, 2, CharacterIds.Marisa);
            AddSurvivor(world, 7, 3, CharacterIds.Sanae);
            Leave(world, 6);

            MatchSummary s = world.BuildSummary();

            Assert.AreEqual(2, s.Survivors.Count);
            Assert.AreEqual(1, s.Survivors[0].Role);
            Assert.AreEqual(3, s.Survivors[1].Role);
        }

        [Test]
        public void APracticeMatchHasNoSummary()
        {
            MatchWorld world = NewWorld(withBoss: false);
            AddSurvivor(world, 5, 1, CharacterIds.Reimu);

            Assert.IsNull(world.BuildSummary());
        }

        // ---- codec ----

        static MatchSummary Sample(int survivors)
        {
            var s = new MatchSummary { LengthSeconds = 754, BossHealth = 0, BossMaxHealth = 250000, DevicesPlaced = 9, DevicesDestroyed = 7, DevicesExpired = 1 };
            for (int i = 0; i < survivors; i++)
                s.Survivors.Add(new SurvivorSummary { Role = (byte)(i + 1), Character = (byte)(i + 1), Level = (byte)(3 + i), Falls = (ushort)(i * 300), Revives = (ushort)i, Alive = i % 2 == 0 });
            return s;
        }

        static void AssertSame(MatchSummary a, MatchSummary b)
        {
            Assert.AreEqual(a.LengthSeconds, b.LengthSeconds);
            Assert.AreEqual(a.BossHealth, b.BossHealth);
            Assert.AreEqual(a.BossMaxHealth, b.BossMaxHealth);
            Assert.AreEqual(a.DevicesPlaced, b.DevicesPlaced);
            Assert.AreEqual(a.DevicesDestroyed, b.DevicesDestroyed);
            Assert.AreEqual(a.DevicesExpired, b.DevicesExpired);
            Assert.AreEqual(a.Survivors.Count, b.Survivors.Count);
            for (int i = 0; i < a.Survivors.Count; i++) Assert.AreEqual(a.Survivors[i], b.Survivors[i]);
        }

        [Test]
        public void Codec_RoundTripsFromZeroToFourSurvivors()
        {
            for (int n = 0; n <= 4; n++)
            {
                MatchSummary s = Sample(n);
                AssertSame(s, MatchSummaryCodec.Read(MatchSummaryCodec.Write(s)));
            }
        }

        [Test]
        public void Codec_RoundTripsABossWithFullHealth()
        {
            MatchSummary s = Sample(2);
            s.BossHealth = 250000;
            AssertSame(s, MatchSummaryCodec.Read(MatchSummaryCodec.Write(s)));
        }

        [Test]
        public void Codec_FourSurvivorsTakeFortyNineBytes()
        {
            Assert.AreEqual(49, MatchSummaryCodec.Write(Sample(4)).Length);
        }

        [Test]
        public void Codec_BadPayloadsReadAsNoSummary()
        {
            byte[] good = MatchSummaryCodec.Write(Sample(3));
            var truncated = new byte[good.Length - 1];
            System.Array.Copy(good, truncated, truncated.Length);
            var extra = new byte[good.Length + 1];
            System.Array.Copy(good, extra, good.Length);

            Assert.IsNull(MatchSummaryCodec.Read(null));
            Assert.IsNull(MatchSummaryCodec.Read(new byte[0]));
            Assert.IsNull(MatchSummaryCodec.Read(new byte[5]));
            Assert.IsNull(MatchSummaryCodec.Read(truncated));
            Assert.IsNull(MatchSummaryCodec.Read(extra));
        }

        // ---- result screen texts ----

        [Test]
        public void Text_LengthIsMinutesAndSeconds()
        {
            Assert.AreEqual("12:34", MatchHud.FormatLength(754));
            Assert.AreEqual("0:05", MatchHud.FormatLength(5));
            Assert.AreEqual("15:00", MatchHud.FormatLength(900));
        }

        [Test]
        public void Text_BossPercentIsZeroOnlyWhenNothingIsLeft()
        {
            Assert.AreEqual(0, MatchHud.BossPercent(0, 250000));
            Assert.AreEqual(1, MatchHud.BossPercent(10, 250000));
            Assert.AreEqual(38, MatchHud.BossPercent(95000, 250000));
            Assert.AreEqual(100, MatchHud.BossPercent(250000, 250000));
            Assert.AreEqual(0, MatchHud.BossPercent(5, 0));
        }

        [Test]
        public void Text_SurvivorAndMatchLines()
        {
            var v = new SurvivorSummary { Role = 2, Character = CharacterIds.Marisa, Level = 7, Falls = 2, Revives = 1, Alive = false };
            Assert.AreEqual("마리사 (생존자 2)  Lv 7  쓰러짐 2  부활 1  쓰러짐", MatchHud.SurvivorLine(v));
            v.Alive = true;
            StringAssert.EndsWith("서 있음", MatchHud.SurvivorLine(v));

            var s = new MatchSummary { LengthSeconds = 754, BossHealth = 0, BossMaxHealth = 250000, DevicesPlaced = 9, DevicesDestroyed = 7 };
            Assert.AreEqual("판 길이 12:34   보스 남은 체력 0%   장치 설치 9 / 파괴 7", MatchHud.MatchLine(s));
        }
    }
}
