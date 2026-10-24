using NUnit.Framework;

namespace BS.Tests
{
    public class SkillPackingTests
    {
        static int[] NoRanks() => new int[SkillCatalog.MaxSkillId + 1];

        [Test]
        public void ThePackedValuesHoldSixtyThreeSkills()
        {
            Assert.AreEqual(3, SkillCatalog.PackedValues);
            Assert.AreEqual(21, SkillCatalog.IdsPerPackedValue);
            Assert.AreEqual(63, SkillCatalog.MaxSkillId);
            Assert.AreEqual(7, SkillCatalog.MaxPackedRank);
        }

        [Test]
        public void EveryRankOfEveryIdFromOneToSixtyThreeComesBackAsSet()
        {
            int[] ranks = NoRanks();
            for (int id = 1; id <= SkillCatalog.MaxSkillId; id++) ranks[id] = id % 8; // all eight values, a different one next door

            int[] back = NoRanks();
            SkillCatalog.Unpack(SkillCatalog.PackAll(ranks), back);

            CollectionAssert.AreEqual(ranks, back);
        }

        [Test]
        public void EachIdAloneSetsNothingElse()
        {
            for (int id = 1; id <= SkillCatalog.MaxSkillId; id++)
            {
                int[] ranks = NoRanks();
                ranks[id] = 7;
                int[] back = NoRanks();

                SkillCatalog.Unpack(SkillCatalog.PackAll(ranks), back);

                for (int other = 0; other <= SkillCatalog.MaxSkillId; other++)
                    Assert.AreEqual(other == id ? 7 : 0, back[other], $"id {id} set, id {other} read");
            }
        }

        [Test]
        public void TheEdgesBetweenTheThreeValuesKeepTheirNeighboursApart()
        {
            foreach ((int a, int b) in new[] { (21, 22), (42, 43) })
            {
                int[] ranks = NoRanks();
                ranks[a] = 5;
                ranks[b] = 2;
                int[] back = NoRanks();

                SkillCatalog.Unpack(SkillCatalog.PackAll(ranks), back);

                Assert.AreEqual(5, back[a]);
                Assert.AreEqual(2, back[b]);
            }
        }

        [Test]
        public void EachValueHoldsItsOwnTwentyOneIds()
        {
            int[] ranks = NoRanks();
            ranks[1] = 1;   // first value, lowest bits
            ranks[21] = 1;  // first value, highest ids
            ranks[22] = 1;  // second value, lowest bits
            ranks[43] = 1;  // third value, lowest bits
            ranks[63] = 1;  // third value, last id

            Assert.AreEqual(1UL | (1UL << 60), SkillCatalog.PackLow(ranks));
            Assert.AreEqual(1UL, SkillCatalog.PackHigh(ranks));
            Assert.AreEqual(1UL | (1UL << 60), SkillCatalog.PackThird(ranks));
            Assert.AreEqual(SkillCatalog.PackLow(ranks), SkillCatalog.Pack(ranks, 0));
            Assert.AreEqual(SkillCatalog.PackThird(ranks), SkillCatalog.Pack(ranks, 2));
        }

        [Test]
        public void ARankAboveSevenIsCutToThreeBitsAndNeverSpillsIntoTheNextId()
        {
            int[] ranks = NoRanks();
            ranks[10] = 9; // 9 & 7 = 1
            ranks[11] = 0;
            int[] back = NoRanks();

            SkillCatalog.Unpack(SkillCatalog.PackAll(ranks), back);

            Assert.AreEqual(1, back[10]);
            Assert.AreEqual(0, back[11]);
        }

        [Test]
        public void IdsOutsideTheRangeAreIgnoredAndTheirSlotsReadZero()
        {
            int[] ranks = new int[SkillCatalog.MaxSkillId + 10];
            for (int id = 0; id < ranks.Length; id++) ranks[id] = 3;

            ulong[] values = SkillCatalog.PackAll(ranks);
            int[] back = new int[SkillCatalog.MaxSkillId + 10];
            SkillCatalog.Unpack(values, back);

            Assert.AreEqual(0, back[0], "id 0 is no skill");
            for (int id = 1; id <= SkillCatalog.MaxSkillId; id++) Assert.AreEqual(3, back[id]);
            for (int id = SkillCatalog.MaxSkillId + 1; id < back.Length; id++) Assert.AreEqual(0, back[id], $"id {id} is beyond the packing");
            Assert.AreEqual(0UL, SkillCatalog.Pack(ranks, -1));
            Assert.AreEqual(0UL, SkillCatalog.Pack(ranks, SkillCatalog.PackedValues));
        }

        [Test]
        public void AShortOrMissingListOfValuesReadsZeroForWhatItDoesNotHold()
        {
            int[] ranks = NoRanks();
            ranks[5] = 4;
            ranks[50] = 6;
            int[] back = NoRanks();

            SkillCatalog.Unpack(new[] { SkillCatalog.PackLow(ranks) }, back);
            Assert.AreEqual(4, back[5]);
            Assert.AreEqual(0, back[50]);

            SkillCatalog.Unpack(null, back);
            Assert.AreEqual(0, back[5]);
        }

        [Test]
        public void ARankArrayShorterThanTheRangeIsPackedAsFarAsItGoes()
        {
            int[] ranks = new int[10];
            ranks[9] = 2;

            Assert.AreEqual(2UL << 24, SkillCatalog.PackLow(ranks));
            Assert.AreEqual(0UL, SkillCatalog.PackThird(ranks));
        }

        [Test]
        public void TheServersThreeValuesUnpackToTheServersRanks()
        {
            var leveling = new LevelingSim(new System.Random(1));
            leveling.Register(1, CharacterIds.Marisa);
            leveling.GrantRank(1, SkillIds.GreatMiracle, 2);
            leveling.GrantRank(1, SkillIds.MasterSpark, 3);
            leveling.GrantRank(1, 63, 5);

            int[] back = NoRanks();
            SkillCatalog.Unpack(SkillCatalog.PackAll(leveling.Ranks(1)), back);

            for (int id = 1; id <= SkillCatalog.MaxSkillId; id++) Assert.AreEqual(leveling.RankOf(1, (byte)id), back[id], $"id {id}");
            Assert.AreEqual(5, back[63]);
        }
    }
}
