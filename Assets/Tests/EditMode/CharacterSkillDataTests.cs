using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace BS.Tests
{
    public class CharacterSkillDataTests
    {
        static readonly byte[] AllCharacters =
            { CharacterIds.Reimu, CharacterIds.Marisa, CharacterIds.Sakuya, CharacterIds.Sanae };

        static int[] NoRanks() => new int[SkillCatalog.MaxSkillId + 1];

        [Test]
        public void EveryCharacterHasExactlyOneQAndOneESkill()
        {
            foreach (byte c in AllCharacters)
            {
                SkillDef q = SkillCatalog.ForSlot(0, c);
                SkillDef e = SkillCatalog.ForSlot(1, c);
                Assert.IsNotNull(q, $"character {c} Q");
                Assert.IsNotNull(e, $"character {c} E");
                Assert.AreNotSame(q, e);
                Assert.AreEqual(c, q.Character);
                Assert.AreEqual(c, e.Character);
                Assert.AreEqual(2, SkillCatalog.All.Count(d => d.IsActive && d.Character == c), "no extra active skills");
            }
        }

        [Test]
        public void TheEightSkillsAreTheOnesTheDesignNames()
        {
            Assert.AreEqual(SkillIds.Burst, SkillCatalog.ForSlot(0, CharacterIds.Reimu).Id);
            Assert.AreEqual(SkillIds.Barrier, SkillCatalog.ForSlot(1, CharacterIds.Reimu).Id);
            Assert.AreEqual(SkillIds.MasterSpark, SkillCatalog.ForSlot(0, CharacterIds.Marisa).Id);
            Assert.AreEqual(SkillIds.RapidFire, SkillCatalog.ForSlot(1, CharacterIds.Marisa).Id);
            Assert.AreEqual(SkillIds.TimeStop, SkillCatalog.ForSlot(0, CharacterIds.Sakuya).Id);
            Assert.AreEqual(SkillIds.KnifeBarrage, SkillCatalog.ForSlot(1, CharacterIds.Sakuya).Id);
            Assert.AreEqual(SkillIds.Miracle, SkillCatalog.ForSlot(0, CharacterIds.Sanae).Id);
            Assert.AreEqual(SkillIds.WindVeil, SkillCatalog.ForSlot(1, CharacterIds.Sanae).Id);

            Assert.AreEqual(SkillEffectKind.AreaDamage, SkillCatalog.Get(SkillIds.Burst).Effect);
            Assert.AreEqual(SkillEffectKind.Barrier, SkillCatalog.Get(SkillIds.Barrier).Effect);
            Assert.AreEqual(SkillEffectKind.Beam, SkillCatalog.Get(SkillIds.MasterSpark).Effect);
            Assert.AreEqual(SkillEffectKind.AttackSpeed, SkillCatalog.Get(SkillIds.RapidFire).Effect);
            Assert.AreEqual(SkillEffectKind.TimeStop, SkillCatalog.Get(SkillIds.TimeStop).Effect);
            Assert.AreEqual(SkillEffectKind.Volley, SkillCatalog.Get(SkillIds.KnifeBarrage).Effect);
            Assert.AreEqual(SkillEffectKind.Heal, SkillCatalog.Get(SkillIds.Miracle).Effect);
            Assert.AreEqual(SkillEffectKind.Push, SkillCatalog.Get(SkillIds.WindVeil).Effect);
        }

        [Test]
        public void NoCharacterAndUnknownCharactersHaveNoSkills()
        {
            Assert.IsNull(SkillCatalog.ForSlot(0, CharacterIds.None));
            Assert.IsNull(SkillCatalog.ForSlot(1, CharacterIds.None));
            Assert.IsNull(SkillCatalog.ForSlot(0, 99));
        }

        [Test]
        public void EveryActiveSkillHasACostACooldownAndAValueForEveryRank()
        {
            foreach (SkillDef def in SkillCatalog.All.Where(d => d.IsActive))
            {
                Assert.Greater(def.ManaCost, 0, def.Name);
                Assert.Greater(def.Cooldown, 0f, def.Name);
                Assert.Greater(def.MaxRank, 0, def.Name);
                Assert.AreNotEqual(SkillEffectKind.None, def.Effect, def.Name);

                switch (def.Effect)
                {
                    case SkillEffectKind.AreaDamage:
                        AssertRanks(def, def.Radius, def.Damage);
                        break;
                    case SkillEffectKind.Barrier:
                    case SkillEffectKind.TimeStop:
                        AssertRanks(def, def.Duration);
                        break;
                    case SkillEffectKind.Beam:
                        AssertRanks(def, def.Length, def.HalfWidth, def.Damage);
                        break;
                    case SkillEffectKind.AttackSpeed:
                        AssertRanks(def, def.Duration, def.CooldownMultiplier);
                        break;
                    case SkillEffectKind.Volley:
                        AssertRanks(def, def.Count, def.Damage);
                        break;
                    case SkillEffectKind.Heal:
                        AssertRanks(def, def.Radius, def.Amount);
                        break;
                    case SkillEffectKind.Push:
                        AssertRanks(def, def.Radius, def.Distance);
                        break;
                }
            }
        }

        static void AssertRanks(SkillDef def, params System.Array[] tables)
        {
            foreach (System.Array table in tables)
            {
                Assert.IsNotNull(table, def.Name);
                Assert.AreEqual(def.MaxRank, table.Length, def.Name);
            }
        }

        [Test]
        public void ValuesNeverShrinkAsTheRankGrows_WhereGrowingIsGood()
        {
            SkillDef burst = SkillCatalog.Get(SkillIds.Burst);
            Assert.That(burst.Damage, Is.Ordered);
            Assert.That(SkillCatalog.Get(SkillIds.MasterSpark).Damage, Is.Ordered);
            Assert.That(SkillCatalog.Get(SkillIds.Barrier).Duration, Is.Ordered);
            Assert.That(SkillCatalog.Get(SkillIds.TimeStop).Duration, Is.Ordered);
            Assert.That(SkillCatalog.Get(SkillIds.KnifeBarrage).Count, Is.Ordered);
            Assert.That(SkillCatalog.Get(SkillIds.Miracle).Amount, Is.Ordered);
            Assert.That(SkillCatalog.Get(SkillIds.WindVeil).Distance, Is.Ordered);
        }

        [Test]
        public void ThePassivesAreCommonToEveryCharacter()
        {
            foreach (SkillDef def in SkillCatalog.All.Where(d => !d.IsActive))
            {
                Assert.AreEqual(CharacterIds.None, def.Character, def.Name);
                foreach (byte c in AllCharacters)
                    Assert.IsTrue(SkillCatalog.IsOwnedBy(def, c), $"{def.Name} / {c}");
            }
        }

        [Test]
        public void AnActiveSkillBelongsToItsCharacterOnly()
        {
            SkillDef spark = SkillCatalog.Get(SkillIds.MasterSpark);
            Assert.IsTrue(SkillCatalog.IsOwnedBy(spark, CharacterIds.Marisa));
            foreach (byte c in AllCharacters.Where(c => c != CharacterIds.Marisa))
                Assert.IsFalse(SkillCatalog.IsOwnedBy(spark, c));
            Assert.IsFalse(SkillCatalog.IsOwnedBy(null, CharacterIds.Marisa));
        }

        [Test]
        public void ASkillOfAnotherCharacterIsNeverAvailable()
        {
            int[] ranks = NoRanks();
            SkillDef knives = SkillCatalog.Get(SkillIds.KnifeBarrage);

            Assert.IsTrue(SkillCatalog.IsAvailable(knives, ranks, CharacterIds.Sakuya));
            Assert.IsFalse(SkillCatalog.IsAvailable(knives, ranks, CharacterIds.Reimu));
            Assert.IsFalse(SkillCatalog.IsAvailable(knives, ranks, CharacterIds.None));
        }

        [Test]
        public void StardustStillNeedsHaste_ForMarisaOnly()
        {
            int[] ranks = NoRanks();
            SkillDef stardust = SkillCatalog.Get(SkillIds.RapidFire);

            Assert.IsFalse(SkillCatalog.IsAvailable(stardust, ranks, CharacterIds.Marisa), "needs Haste first");
            ranks[SkillIds.Haste] = 1;
            Assert.IsTrue(SkillCatalog.IsAvailable(stardust, ranks, CharacterIds.Marisa));
            Assert.IsFalse(SkillCatalog.IsAvailable(stardust, ranks, CharacterIds.Reimu), "not hers");
        }

        [Test]
        public void EachCharacterIsOfferedOnlyItsOwnActivesAndTheCommonPassives()
        {
            foreach (byte c in AllCharacters)
            {
                var sim = new LevelingSim(new System.Random(c));
                sim.Register(1, c);
                var offered = new HashSet<byte>();

                for (int round = 0; round < 40; round++)
                {
                    sim.AddXp(1, GrowthConfig.XpNeeded(1) + 5000);
                    foreach (byte id in sim.CurrentChoices(1))
                    {
                        offered.Add(id);
                        SkillDef def = SkillCatalog.Get(id);
                        Assert.IsTrue(def.Character == CharacterIds.None || def.Character == c, $"character {c} was offered {def.Name}");
                    }
                    sim.Pick(1, sim.CurrentOfferId(1), 0);
                    sim.DrainEvents(new List<GrowthEvent>());
                    if (!sim.HasOffer(1)) break;
                }
                Assert.IsTrue(offered.Count > 0);
            }
        }

        [Test]
        public void TwoCharactersAreOfferedDifferentActives()
        {
            var seen = new Dictionary<byte, HashSet<byte>>();
            foreach (byte c in new[] { CharacterIds.Reimu, CharacterIds.Sanae })
            {
                seen[c] = new HashSet<byte>();
                for (int seed = 0; seed < 80; seed++)
                {
                    var sim = new LevelingSim(new System.Random(seed));
                    sim.Register(1, c);
                    sim.AddXp(1, GrowthConfig.XpNeeded(1));
                    foreach (byte id in sim.CurrentChoices(1)) seen[c].Add(id);
                }
            }

            Assert.IsTrue(seen[CharacterIds.Reimu].Contains(SkillIds.Burst));
            Assert.IsFalse(seen[CharacterIds.Reimu].Contains(SkillIds.Miracle));
            Assert.IsTrue(seen[CharacterIds.Sanae].Contains(SkillIds.Miracle));
            Assert.IsFalse(seen[CharacterIds.Sanae].Contains(SkillIds.Burst));
            Assert.IsTrue(seen[CharacterIds.Reimu].Contains(SkillIds.Power) && seen[CharacterIds.Sanae].Contains(SkillIds.Power), "both can be offered the common passives");
        }

        [Test]
        public void ASurvivorWithoutACharacterIsOnlyOfferedPassives()
        {
            var sim = new LevelingSim(new System.Random(2));
            sim.Register(1, CharacterIds.None);
            sim.AddXp(1, GrowthConfig.XpNeeded(1));

            foreach (byte id in sim.CurrentChoices(1)) Assert.IsFalse(SkillCatalog.Get(id).IsActive);
        }

        [Test]
        public void SkillIdsStayWithinThePackedFormat_AndRanksOfNewSkillsRoundTrip()
        {
            foreach (SkillDef def in SkillCatalog.All) Assert.LessOrEqual(def.Id, SkillCatalog.MaxSkillId);

            int[] ranks = NoRanks();
            foreach (SkillDef def in SkillCatalog.All) ranks[def.Id] = def.MaxRank % (SkillCatalog.MaxPackedRank + 1);
            int[] back = NoRanks();
            SkillCatalog.Unpack(SkillCatalog.Pack(ranks), back);
            CollectionAssert.AreEqual(ranks, back);
        }
    }
}
