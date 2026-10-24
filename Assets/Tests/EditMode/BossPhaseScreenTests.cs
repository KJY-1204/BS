using NUnit.Framework;

namespace BS.Tests
{
    public class BossPhaseScreenTests
    {
        // ---- what is replicated ----

        [Test]
        public void ACooldownIsReplicatedInTenthsOfASecondRoundedUpAndCappedAtOneByte()
        {
            Assert.AreEqual(0, BossControls.CooldownByte(0f));
            Assert.AreEqual(0, BossControls.CooldownByte(-2f));
            Assert.AreEqual(1, BossControls.CooldownByte(0.04f));
            Assert.AreEqual(1, BossControls.CooldownByte(0.1f));
            Assert.AreEqual(2, BossControls.CooldownByte(0.11f));
            Assert.AreEqual(140, BossControls.CooldownByte(14f));
            Assert.AreEqual(255, BossControls.CooldownByte(25.5f));
            Assert.AreEqual(255, BossControls.CooldownByte(300f));
        }

        [Test]
        public void ThePhaseFitsInTheReplicatedByte()
        {
            Assert.LessOrEqual(BossPhases.Third, byte.MaxValue);
            Assert.AreEqual(BossPhases.First, (byte)BossPhases.First);
        }

        // ---- the development key list ----

        [Test]
        public void TheAutoSkillKeysNameQEAndFInAnyCombination()
        {
            Assert.AreEqual(1, BossControls.SlotMaskOfKeys("q"));
            Assert.AreEqual(2, BossControls.SlotMaskOfKeys("e"));
            Assert.AreEqual(4, BossControls.SlotMaskOfKeys("f"));
            Assert.AreEqual(3, BossControls.SlotMaskOfKeys("qe"), "qe still means Q and E");
            Assert.AreEqual(7, BossControls.SlotMaskOfKeys("qef"));
            Assert.AreEqual(5, BossControls.SlotMaskOfKeys("FQ"));
            Assert.AreEqual(0, BossControls.SlotMaskOfKeys("x"));
            Assert.AreEqual(0, BossControls.SlotMaskOfKeys(""));
            Assert.AreEqual(0, BossControls.SlotMaskOfKeys(null));
        }

        // ---- the boss's screen ----

        [Test]
        public void ASlotShowsTheKindsSkillAndTheThirdSlotItsPhaseSkill()
        {
            foreach (BossDef kind in BossCatalog.All)
            {
                Assert.AreSame(kind.Skills[0], SummonHud.SkillOfSlot(kind, 0));
                Assert.AreSame(kind.Skills[1], SummonHud.SkillOfSlot(kind, 1));
                Assert.AreSame(kind.PhaseSkill, SummonHud.SkillOfSlot(kind, BossSkills.PhaseSlot));
                Assert.IsNull(SummonHud.SkillOfSlot(kind, BossSkills.SlotCount));
                Assert.IsNull(SummonHud.SkillOfSlot(kind, -1));
            }
            Assert.IsNull(SummonHud.SkillOfSlot(null, 0));
        }

        [Test]
        public void ASkillCellSaysReadyTheSecondsLeftOrWhatOpensIt()
        {
            BossSkillDef beam = BossCatalog.Get(BossCatalog.Standard).PhaseSkill;

            Assert.AreEqual("F 저격선 (준비)", SummonHud.SkillCellText(beam, 0f, false));
            Assert.AreEqual("F 저격선 (3.5초)", SummonHud.SkillCellText(beam, 3.5f, false));
            Assert.AreEqual("F 저격선 (국면 2에 열림)", SummonHud.SkillCellText(beam, 0f, true));
            BossSkillDef q = BossCatalog.Get(BossCatalog.Standard).Skills[0];
            Assert.AreEqual($"Q {q.Name} (준비)", SummonHud.SkillCellText(q, 0f, false));
        }

        [Test]
        public void EveryCellTextIsShortEnoughForItsCell()
        {
            foreach (BossDef kind in BossCatalog.All)
                for (int slot = 0; slot < BossSkills.SlotCount; slot++)
                {
                    BossSkillDef s = SummonHud.SkillOfSlot(kind, slot);
                    Assert.LessOrEqual(SummonHud.SkillCellText(s, 12.3f, false).Length, 16, $"{kind.Name} {slot}");
                    Assert.LessOrEqual(SummonHud.SkillCellText(s, 0f, true).Length, 18, $"{kind.Name} {slot} locked");
                }
        }

        // ---- everyone's screen ----

        [Test]
        public void TheBossBarNamesThePhaseAndTheBannerAnnouncesIt()
        {
            Assert.AreEqual("보스  95000 / 190000   국면 2", MatchHud.BossBarText("보스", 95000, 190000, 2));
            Assert.AreEqual("국면 3", MatchHud.PhaseBannerText(3));
        }
    }
}
