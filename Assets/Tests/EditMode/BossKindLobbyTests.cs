using NUnit.Framework;

namespace BS.Tests
{
    public class BossKindLobbyTests
    {
        const ulong Host = 0, A = 1, B = 2, C = 3, D = 4;

        /// <summary>A room with the boss role held by <see cref="D"/> (the fourth joiner) and three survivors.</summary>
        static LobbySim RoomWithBoss()
        {
            var sim = new LobbySim(Host);
            sim.Join(Host, false, out _);
            sim.Join(A, false, out _);
            sim.Join(B, false, out _);
            sim.Join(C, false, out _);
            sim.Join(D, false, out byte role);
            Assert.AreEqual(LobbySim.BossRole, role);
            return sim;
        }

        static byte KindOfBossSlot(LobbySim sim)
        {
            foreach (LobbySlot slot in sim.Slots) if (slot.Role == LobbySim.BossRole) return slot.BossKind;
            return 255;
        }

        [Test]
        public void TheBossPlayerChoosesAndChangesTheKind()
        {
            LobbySim sim = RoomWithBoss();

            Assert.AreEqual(LobbyResult.Ok, sim.SelectBossKind(D, BossCatalog.Sniper));
            Assert.AreEqual(BossCatalog.Sniper, KindOfBossSlot(sim));
            Assert.AreEqual(BossCatalog.Sniper, sim.BossKindOf());

            Assert.AreEqual(LobbyResult.Ok, sim.SelectBossKind(D, BossCatalog.Giant));
            Assert.AreEqual(BossCatalog.Giant, sim.BossKindOf());
        }

        [Test]
        public void NothingChosenMeansTheStandardKind()
        {
            LobbySim sim = RoomWithBoss();
            Assert.AreEqual(0, KindOfBossSlot(sim));
            Assert.AreEqual(BossCatalog.Standard, sim.BossKindOf());

            sim.SelectBossKind(D, BossCatalog.Giant);
            Assert.AreEqual(LobbyResult.Ok, sim.SelectBossKind(D, 0), "the choice can be released");
            Assert.AreEqual(BossCatalog.Standard, sim.BossKindOf());
        }

        [Test]
        public void ARoomWithNoBossAsksForTheStandardKind()
        {
            var sim = new LobbySim(Host);
            sim.Join(Host, false, out _);
            sim.Join(A, false, out _);
            Assert.AreEqual(BossCatalog.Standard, sim.BossKindOf());
        }

        [Test]
        public void ASurvivorsRequestIsRefusedAndNothingChanges()
        {
            LobbySim sim = RoomWithBoss();
            sim.SelectBossKind(D, BossCatalog.Giant);

            Assert.AreEqual(LobbyResult.NotBoss, sim.SelectBossKind(A, BossCatalog.Sniper));
            Assert.AreEqual(LobbyResult.NotBoss, sim.SelectBossKind(Host, BossCatalog.Sniper));
            Assert.AreEqual(BossCatalog.Giant, sim.BossKindOf());
            foreach (LobbySlot slot in sim.Slots) if (slot.Role != LobbySim.BossRole) Assert.AreEqual(0, slot.BossKind, "survivors never hold a kind");
        }

        [Test]
        public void AnUnknownKindOrAnUnknownPlayerIsRefusedAndTheEarlierChoiceStays()
        {
            LobbySim sim = RoomWithBoss();
            sim.SelectBossKind(D, BossCatalog.Sniper);

            Assert.AreEqual(LobbyResult.UnknownKind, sim.SelectBossKind(D, 4));
            Assert.AreEqual(LobbyResult.UnknownKind, sim.SelectBossKind(D, 200));
            Assert.AreEqual(LobbyResult.UnknownPlayer, sim.SelectBossKind(99, BossCatalog.Giant));
            Assert.AreEqual(BossCatalog.Sniper, sim.BossKindOf());
        }

        [Test]
        public void AskingForTheSameKindAgainChangesNothing()
        {
            LobbySim sim = RoomWithBoss();
            sim.SelectBossKind(D, BossCatalog.Giant);
            Assert.AreEqual(LobbyResult.NoChange, sim.SelectBossKind(D, BossCatalog.Giant));
        }

        [Test]
        public void AMatchInProgressRefusesTheRequestAndKeepsItsKind()
        {
            LobbySim sim = RoomWithBoss();
            sim.SelectBossKind(D, BossCatalog.Giant);
            sim.SelectCharacter(Host, CharacterIds.Reimu);
            sim.SelectCharacter(A, CharacterIds.Marisa);
            sim.SelectCharacter(B, CharacterIds.Sakuya);
            sim.SelectCharacter(C, CharacterIds.Sanae);
            Assert.AreEqual(LobbyResult.Ok, sim.TryStart(Host));

            Assert.AreEqual(LobbyResult.WrongPhase, sim.SelectBossKind(D, BossCatalog.Sniper));
            Assert.AreEqual(BossCatalog.Giant, sim.BossKindOf());
        }

        [Test]
        public void TheChoiceStaysWhenTheRoomReturnsToWaitingAndCanThenChange()
        {
            LobbySim sim = RoomWithBoss();
            sim.SelectBossKind(D, BossCatalog.Giant);
            sim.SelectCharacter(Host, CharacterIds.Reimu);
            sim.SelectCharacter(A, CharacterIds.Marisa);
            sim.SelectCharacter(B, CharacterIds.Sakuya);
            sim.SelectCharacter(C, CharacterIds.Sanae);
            sim.TryStart(Host);
            sim.TryEndMatch(Host);

            Assert.AreEqual(BossCatalog.Giant, sim.BossKindOf(), "the same boss player keeps the choice");
            Assert.AreEqual(LobbyResult.Ok, sim.SelectBossKind(D, BossCatalog.Sniper));
            Assert.AreEqual(BossCatalog.Sniper, sim.BossKindOf(), "the second match uses the new choice");
        }

        [Test]
        public void ANewBossPlayerDoesNotInheritTheLeaversChoice()
        {
            LobbySim sim = RoomWithBoss();
            sim.SelectBossKind(D, BossCatalog.Giant);
            sim.Leave(D);
            Assert.AreEqual(BossCatalog.Standard, sim.BossKindOf(), "nobody holds the boss role now");

            sim.Join(5, true, out byte role);
            Assert.AreEqual(LobbySim.BossRole, role);
            Assert.AreEqual(0, KindOfBossSlot(sim));
            Assert.AreEqual(BossCatalog.Standard, sim.BossKindOf());
        }

        [Test]
        public void TheBossKindDoesNotAffectWhenTheHostMayStart()
        {
            LobbySim sim = RoomWithBoss();
            Assert.IsFalse(sim.IsReadyToStart, "survivors still need characters");
            sim.SelectCharacter(Host, CharacterIds.Reimu);
            sim.SelectCharacter(A, CharacterIds.Marisa);
            sim.SelectCharacter(B, CharacterIds.Sakuya);
            sim.SelectCharacter(C, CharacterIds.Sanae);
            Assert.IsTrue(sim.IsReadyToStart, "a boss that chose nothing does not block the start");
        }

        [Test]
        public void ThePlayerSlotThatIsReplicatedCarriesTheKindAndComparesByIt()
        {
            var a = new PlayerSlot { ClientId = 1, Role = 0, Character = 0, BossKind = 2 };
            var b = new PlayerSlot { ClientId = 1, Role = 0, Character = 0, BossKind = 3 };
            Assert.IsFalse(a.Equals(b));
            Assert.IsTrue(a.Equals(new PlayerSlot { ClientId = 1, Role = 0, Character = 0, BossKind = 2 }));
        }
    }
}
