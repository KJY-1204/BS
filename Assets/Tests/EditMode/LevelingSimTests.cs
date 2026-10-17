using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class LevelingSimTests
    {
        const ulong A = 1, B = 2, C = 3;
        const float Dt = 0.02f;

        static LevelingSim New(int seed = 1, params ulong[] clients)
        {
            var sim = new LevelingSim(new System.Random(seed));
            foreach (ulong id in clients) sim.Register(id);
            return sim;
        }

        static GrowthBody Body(ulong id, float x, float y, bool alive = true) =>
            new GrowthBody { ClientId = id, Position = new Vector2(x, y), Alive = alive };

        static List<GrowthEvent> Drain(LevelingSim sim)
        {
            var events = new List<GrowthEvent>();
            sim.DrainEvents(events);
            return events;
        }

        static void Run(LevelingSim sim, float seconds)
        {
            for (int i = 0; i < Mathf.RoundToInt(seconds / Dt); i++) sim.Step(Dt);
        }

        // ---- 3.1 sharing a kill ----

        [Test]
        public void ANearbyPairBothGetTheFullAmount()
        {
            LevelingSim sim = New(1, A, B);
            var bodies = new List<GrowthBody> { Body(A, 4, 0), Body(B, 0, 6) };

            int recipients = sim.AwardKill(Vector2.zero, bodies);

            Assert.AreEqual(2, recipients);
            Assert.AreEqual(GrowthConfig.XpPerKill, sim.Xp(A));
            Assert.AreEqual(GrowthConfig.XpPerKill, sim.Xp(B));
        }

        [Test]
        public void ASurvivorBeyondTheShareDistanceGetsNothing()
        {
            LevelingSim sim = New(1, A, C);
            var bodies = new List<GrowthBody> { Body(A, 3, 0), Body(C, GrowthConfig.XpShareDistance + 1f, 0) };

            sim.AwardKill(Vector2.zero, bodies);

            Assert.AreEqual(GrowthConfig.XpPerKill, sim.Xp(A));
            Assert.AreEqual(0, sim.Xp(C));
        }

        [Test]
        public void TheShareDistanceIsInclusive()
        {
            LevelingSim sim = New(1, A);

            sim.AwardKill(Vector2.zero, new List<GrowthBody> { Body(A, GrowthConfig.XpShareDistance, 0) });

            Assert.AreEqual(GrowthConfig.XpPerKill, sim.Xp(A));
        }

        [Test]
        public void TheKillerFarFromTheMobGetsNothingAndSomeoneNearItDoes()
        {
            // The shooter is at x = -20; the mob died at x = 8 right next to the other survivor.
            LevelingSim sim = New(1, A, B);
            var bodies = new List<GrowthBody> { Body(A, -20, 0), Body(B, 7, 0) };

            sim.AwardKill(new Vector2(8, 0), bodies);

            Assert.AreEqual(0, sim.Xp(A));
            Assert.AreEqual(GrowthConfig.XpPerKill, sim.Xp(B));
        }

        [Test]
        public void TheDeadAndUnknownSurvivorsGetNothing()
        {
            LevelingSim sim = New(1, A, B);
            var bodies = new List<GrowthBody> { Body(A, 1, 0, alive: false), Body(B, 1, 1), Body(99, 1, 2) };

            int recipients = sim.AwardKill(Vector2.zero, bodies);

            Assert.AreEqual(1, recipients, "the boss is never listed, so it cannot be a recipient");
            Assert.AreEqual(0, sim.Xp(A));
            Assert.AreEqual(GrowthConfig.XpPerKill, sim.Xp(B));
        }

        [Test]
        public void ManyRecipients_AllGetTheSameAmount()
        {
            LevelingSim sim = New(1, A, B, C);
            var bodies = new List<GrowthBody> { Body(A, 0, 0), Body(B, 1, 1), Body(C, 2, 2) };

            sim.AwardKill(Vector2.zero, bodies);

            Assert.AreEqual(sim.Xp(A), sim.Xp(B));
            Assert.AreEqual(sim.Xp(B), sim.Xp(C));
        }

        // ---- 3.2 levels ----

        [Test]
        public void ReachingTheThreshold_RaisesTheLevelAndKeepsTheSurplus()
        {
            LevelingSim sim = New(1, A);

            sim.AddXp(A, GrowthConfig.XpNeeded(1) + 7);

            Assert.AreEqual(2, sim.Level(A));
            Assert.AreEqual(7, sim.Xp(A));
        }

        [Test]
        public void JustBelowTheThreshold_StaysOnTheSameLevel()
        {
            LevelingSim sim = New(1, A);

            sim.AddXp(A, GrowthConfig.XpNeeded(1) - 1);

            Assert.AreEqual(1, sim.Level(A));
            Assert.AreEqual(GrowthConfig.XpNeeded(1) - 1, sim.Xp(A));
        }

        [Test]
        public void ALargeGrant_GainsTwoLevelsOneAfterTheOther()
        {
            LevelingSim sim = New(1, A);

            sim.AddXp(A, GrowthConfig.XpNeeded(1) + GrowthConfig.XpNeeded(2) + 3);

            Assert.AreEqual(3, sim.Level(A));
            Assert.AreEqual(3, sim.Xp(A));
            List<GrowthEvent> levelUps = Drain(sim).Where(e => e.Kind == GrowthEventKind.LevelUp).ToList();
            CollectionAssert.AreEqual(new[] { 2, 3 }, levelUps.Select(e => e.Level).ToArray());
        }

        [Test]
        public void AtTheCap_FurtherExperienceIsIgnored()
        {
            LevelingSim sim = New(1, A);
            sim.MaxLevel = 3;
            sim.AddXp(A, 100000);
            Assert.AreEqual(3, sim.Level(A));
            Assert.AreEqual(0, sim.Xp(A));
            Drain(sim);

            sim.AddXp(A, 500);

            Assert.AreEqual(3, sim.Level(A));
            Assert.AreEqual(0, sim.Xp(A));
            Assert.AreEqual(0, Drain(sim).Count);
            Assert.AreEqual(0, sim.XpNeeded(A));
        }

        [Test]
        public void ZeroNegativeAndUnknown_AreIgnored()
        {
            LevelingSim sim = New(1, A);

            sim.AddXp(A, 0);
            sim.AddXp(A, -10);
            Assert.DoesNotThrow(() => sim.AddXp(99, 50));

            Assert.AreEqual(0, sim.Xp(A));
            Assert.AreEqual(1, sim.Level(A));
        }

        // ---- 3.3 offers and picks ----

        [Test]
        public void ALevelUp_OpensAChoiceOfThreeDifferentSkills()
        {
            LevelingSim sim = New(1, A);

            sim.AddXp(A, GrowthConfig.XpNeeded(1));

            Assert.IsTrue(sim.HasOffer(A));
            byte[] choices = sim.CurrentChoices(A);
            Assert.AreEqual(3, choices.Length);
            Assert.AreEqual(3, choices.Distinct().Count());
            foreach (byte id in choices) Assert.IsTrue(SkillCatalog.IsAvailable(SkillCatalog.Get(id), sim.Ranks(A)));

            GrowthEvent opened = Drain(sim).Single(e => e.Kind == GrowthEventKind.OfferOpened);
            Assert.AreEqual(A, opened.ClientId);
            CollectionAssert.AreEqual(choices, opened.Choices);
            Assert.AreEqual(GrowthConfig.OfferSeconds, opened.Seconds);
            Assert.AreNotEqual(0, opened.OfferId);
        }

        [Test]
        public void TheOfferNeverContainsASkillWhosePrerequisiteIsMissing()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                LevelingSim sim = New(seed, A);
                sim.AddXp(A, GrowthConfig.XpNeeded(1));
                CollectionAssert.DoesNotContain(sim.CurrentChoices(A), SkillIds.RapidFire, $"seed {seed}");
            }
        }

        [Test]
        public void AValidPick_GivesTheRankAndClosesTheChoice()
        {
            LevelingSim sim = New(1, A);
            sim.AddXp(A, GrowthConfig.XpNeeded(1));
            byte offer = sim.CurrentOfferId(A);
            byte second = sim.CurrentChoices(A)[1];
            Drain(sim);

            Assert.IsTrue(sim.Pick(A, offer, 1));

            Assert.AreEqual(1, sim.RankOf(A, second));
            Assert.IsFalse(sim.HasOffer(A));
            GrowthEvent closed = Drain(sim).Single();
            Assert.AreEqual(GrowthEventKind.OfferClosed, closed.Kind);
            Assert.AreEqual(second, closed.Skill);
            Assert.IsFalse(closed.Auto);
            Assert.AreEqual(offer, closed.OfferId);
        }

        [Test]
        public void FewSkillsLeft_OffersJustThose()
        {
            LevelingSim sim = New(1, A);
            // Max out everything except Power and Vitality.
            foreach (SkillDef def in SkillCatalog.All)
                if (def.Id != SkillIds.Power && def.Id != SkillIds.Vitality)
                    ((int[])sim.Ranks(A))[def.Id] = def.MaxRank;

            sim.AddXp(A, GrowthConfig.XpNeeded(1));

            byte[] choices = sim.CurrentChoices(A);
            Assert.AreEqual(2, choices.Length);
            CollectionAssert.AreEquivalent(new[] { SkillIds.Power, SkillIds.Vitality }, choices);
        }

        [Test]
        public void NothingLeft_NoChoiceOpens()
        {
            LevelingSim sim = New(1, A);
            foreach (SkillDef def in SkillCatalog.All) ((int[])sim.Ranks(A))[def.Id] = def.MaxRank;

            sim.AddXp(A, GrowthConfig.XpNeeded(1));

            Assert.AreEqual(2, sim.Level(A), "the level still rises");
            Assert.IsFalse(sim.HasOffer(A));
            Assert.AreEqual(0, sim.PendingOffers(A));
            Assert.IsFalse(Drain(sim).Any(e => e.Kind == GrowthEventKind.OfferOpened));
        }

        [Test]
        public void InvalidPicksAreIgnoredAndTheChoiceStaysOpen()
        {
            LevelingSim sim = New(1, A, B);
            sim.AddXp(A, GrowthConfig.XpNeeded(1));
            byte offer = sim.CurrentOfferId(A);

            Assert.IsFalse(sim.Pick(A, offer, 7), "number out of range");
            Assert.IsFalse(sim.Pick(A, offer, -1));
            Assert.IsFalse(sim.Pick(A, (byte)(offer + 1), 0), "not the choice being answered");
            Assert.IsFalse(sim.Pick(A, 0, 0));
            Assert.IsFalse(sim.Pick(B, offer, 0), "someone else has no such choice");
            Assert.IsFalse(sim.Pick(99, offer, 0));

            Assert.IsTrue(sim.HasOffer(A));
            Assert.IsTrue(sim.Pick(A, offer, 0), "the right pick still works afterwards");
        }

        [Test]
        public void ADoublePick_OnlyTheFirstCounts()
        {
            LevelingSim sim = New(1, A);
            sim.AddXp(A, GrowthConfig.XpNeeded(1));
            byte offer = sim.CurrentOfferId(A);
            byte first = sim.CurrentChoices(A)[0];

            Assert.IsTrue(sim.Pick(A, offer, 0));
            Assert.IsFalse(sim.Pick(A, offer, 0));

            Assert.AreEqual(1, sim.RankOf(A, first));
            Assert.AreEqual(1, sim.Ranks(A).Sum());
        }

        [Test]
        public void APickWithNoOpenChoiceIsIgnored()
        {
            LevelingSim sim = New(1, A);
            Assert.IsFalse(sim.Pick(A, 1, 0));
        }

        // ---- 3.4 time limit, queue, death, disconnect ----

        [Test]
        public void WhenTimeRunsOut_OneOfTheOfferedSkillsIsChosenAtRandom()
        {
            LevelingSim sim = New(1, A);
            sim.AddXp(A, GrowthConfig.XpNeeded(1));
            byte[] choices = sim.CurrentChoices(A);
            Drain(sim);

            Run(sim, GrowthConfig.OfferSeconds - 0.5f);
            Assert.IsTrue(sim.HasOffer(A), "still open just before the limit");

            Run(sim, 1f);

            Assert.IsFalse(sim.HasOffer(A));
            GrowthEvent closed = Drain(sim).Single();
            Assert.IsTrue(closed.Auto);
            CollectionAssert.Contains(choices, closed.Skill);
            Assert.AreEqual(1, sim.RankOf(A, closed.Skill));
        }

        [Test]
        public void TheRandomChoiceReallyVariesAcrossTheOffer()
        {
            var seen = new HashSet<int>();
            for (int seed = 0; seed < 60; seed++)
            {
                LevelingSim sim = New(seed, A);
                sim.AddXp(A, GrowthConfig.XpNeeded(1));
                byte[] choices = sim.CurrentChoices(A);
                Drain(sim);
                Run(sim, GrowthConfig.OfferSeconds + 0.5f);
                GrowthEvent closed = Drain(sim).Single();
                seen.Add(System.Array.IndexOf(choices, closed.Skill));
            }
            Assert.AreEqual(3, seen.Count, "each of the three positions gets picked sometimes");
        }

        [Test]
        public void ALatePick_AfterTheTimeoutIsIgnored()
        {
            LevelingSim sim = New(1, A);
            sim.AddXp(A, GrowthConfig.XpNeeded(1));
            byte offer = sim.CurrentOfferId(A);
            Run(sim, GrowthConfig.OfferSeconds + 0.5f);
            int ranksAfterTimeout = sim.Ranks(A).Sum();

            Assert.IsFalse(sim.Pick(A, offer, 0));
            Assert.AreEqual(ranksAfterTimeout, sim.Ranks(A).Sum());
        }

        [Test]
        public void TwoLevelsAtOnce_ChoicesOpenInTurnEachWithItsOwnLimit()
        {
            LevelingSim sim = New(1, A);
            sim.AddXp(A, GrowthConfig.XpNeeded(1) + GrowthConfig.XpNeeded(2));
            byte first = sim.CurrentOfferId(A);
            Assert.AreEqual(1, sim.PendingOffers(A));
            Assert.AreEqual(1, Drain(sim).Count(e => e.Kind == GrowthEventKind.OfferOpened), "only the first is opened");

            Assert.IsTrue(sim.Pick(A, first, 0));

            Assert.IsTrue(sim.HasOffer(A), "the second opens when the first closes");
            Assert.AreNotEqual(first, sim.CurrentOfferId(A));
            Assert.AreEqual(0, sim.PendingOffers(A));
            Assert.AreEqual(GrowthConfig.OfferSeconds, sim.OfferTimeLeft(A), 1e-4f);
        }

        [Test]
        public void WhenTheFirstTimesOut_TheSecondStartsWithAFullLimit()
        {
            LevelingSim sim = New(1, A);
            sim.AddXp(A, GrowthConfig.XpNeeded(1) + GrowthConfig.XpNeeded(2));
            Run(sim, GrowthConfig.OfferSeconds + 0.5f);

            Assert.IsTrue(sim.HasOffer(A));
            Assert.Greater(sim.OfferTimeLeft(A), GrowthConfig.OfferSeconds - 1f);

            Run(sim, GrowthConfig.OfferSeconds + 0.5f);
            Assert.IsFalse(sim.HasOffer(A));
            Assert.AreEqual(2, sim.Ranks(A).Sum(), "both choices ended up applied");
        }

        [Test]
        public void ADeadSurvivorKeepsTheOpenChoiceAndItStillCloses()
        {
            // Death only stops experience being handed out; the sim keeps the choice and its countdown.
            LevelingSim sim = New(1, A);
            sim.AddXp(A, GrowthConfig.XpNeeded(1));
            byte offer = sim.CurrentOfferId(A);

            sim.AwardKill(Vector2.zero, new List<GrowthBody> { Body(A, 0, 0, alive: false) });
            Assert.IsTrue(sim.HasOffer(A));
            Assert.AreEqual(0, sim.Xp(A));

            Assert.IsTrue(sim.Pick(A, offer, 2));
            Assert.IsFalse(sim.HasOffer(A));
        }

        [Test]
        public void ADeadSurvivorsChoiceStillTimesOut()
        {
            LevelingSim sim = New(1, A);
            sim.AddXp(A, GrowthConfig.XpNeeded(1));

            Run(sim, GrowthConfig.OfferSeconds + 0.5f);

            Assert.IsFalse(sim.HasOffer(A));
            Assert.AreEqual(1, sim.Ranks(A).Sum());
        }

        [Test]
        public void ADisconnect_DropsTheStateWithoutTouchingOthers()
        {
            LevelingSim sim = New(1, A, B);
            sim.AddXp(A, GrowthConfig.XpNeeded(1) + GrowthConfig.XpNeeded(2));
            sim.AddXp(B, GrowthConfig.XpNeeded(1));
            byte offerB = sim.CurrentOfferId(B);

            sim.Unregister(A);

            Assert.IsFalse(sim.Contains(A));
            Assert.AreEqual(1, sim.Level(A), "unknown survivors read as the defaults");
            Assert.AreEqual(0, sim.PendingOffers(A));
            Assert.DoesNotThrow(() => Run(sim, 1f));
            Assert.IsTrue(sim.Pick(B, offerB, 0));
            Assert.AreEqual(2, sim.Level(B));
        }

        [Test]
        public void Clear_ResetsEveryone()
        {
            LevelingSim sim = New(1, A, B);
            sim.AddXp(A, 500);

            sim.Clear();
            sim.Register(A);

            Assert.AreEqual(1, sim.Level(A));
            Assert.AreEqual(0, sim.Xp(A));
            Assert.IsFalse(sim.HasOffer(A));
            Assert.AreEqual(0, sim.Ranks(A).Sum());
            Assert.IsFalse(sim.Contains(B));
            Assert.AreEqual(0, Drain(sim).Count);
        }

        [Test]
        public void OfferIds_AreNeverZeroAndChangeEveryTime()
        {
            LevelingSim sim = New(3, A);
            sim.MaxLevel = 100;
            var seen = new List<byte>();

            for (int i = 0; i < 300; i++)
            {
                sim.AddXp(A, GrowthConfig.XpNeeded(sim.Level(A)));
                if (!sim.HasOffer(A)) break; // every skill maxed out, no more choices
                byte id = sim.CurrentOfferId(A);
                Assert.AreNotEqual(0, id);
                if (seen.Count > 0) Assert.AreNotEqual(seen[seen.Count - 1], id);
                seen.Add(id);
                sim.Pick(A, id, 0);
            }
            Assert.Greater(seen.Count, 10);
        }

        [Test]
        public void ARandomPlaythrough_NeverBreaksTheRules()
        {
            var random = new System.Random(77);
            LevelingSim sim = New(5, A, B, C);
            sim.MaxLevel = 12;
            ulong[] ids = { A, B, C };

            for (int step = 0; step < 4000; step++)
            {
                if (step % 7 == 0) sim.AddXp(ids[random.Next(3)], random.Next(1, 40));
                if (step % 13 == 0)
                {
                    ulong id = ids[random.Next(3)];
                    if (sim.HasOffer(id)) sim.Pick(id, sim.CurrentOfferId(id), random.Next(-1, 5));
                }
                sim.Step(0.05f);

                foreach (ulong id in ids)
                {
                    Assert.LessOrEqual(sim.Level(id), sim.MaxLevel);
                    foreach (SkillDef def in SkillCatalog.All)
                    {
                        int rank = sim.RankOf(id, def.Id);
                        Assert.LessOrEqual(rank, def.MaxRank, $"{def.Name} above its maximum");
                        if (def.RequiresSkill != 0 && rank > 0)
                            Assert.GreaterOrEqual(sim.RankOf(id, def.RequiresSkill), def.RequiresRank, $"{def.Name} learned without its prerequisite");
                    }
                }
            }
        }
    }
}
