using NUnit.Framework;

namespace BS.Tests
{
    public class LobbySimTests
    {
        const ulong Host = 0, A = 1, B = 2, C = 3, D = 4, E = 5;
        const byte Reimu = CharacterIds.Reimu, Marisa = CharacterIds.Marisa, Sakuya = CharacterIds.Sakuya, Sanae = CharacterIds.Sanae;

        static LobbySim Room(bool hostBoss = false, params ulong[] others)
        {
            var sim = new LobbySim(Host);
            sim.Join(Host, hostBoss, out _);
            foreach (ulong id in others) sim.Join(id, false, out _);
            return sim;
        }

        // ---- 2.1 joining ----

        [Test]
        public void ANewRoomIsWaitingAndEmpty()
        {
            var sim = new LobbySim(Host);
            Assert.AreEqual(RoomPhase.Waiting, sim.Phase);
            Assert.AreEqual(0, sim.Count);
        }

        [Test]
        public void RolesFollowTheExistingRules_HostAsSurvivor()
        {
            LobbySim sim = Room(false, A, B, C, D);

            Assert.AreEqual(1, sim.RoleOf(Host));
            Assert.AreEqual(2, sim.RoleOf(A));
            Assert.AreEqual(3, sim.RoleOf(B));
            Assert.AreEqual(4, sim.RoleOf(C));
            Assert.AreEqual(LobbySim.BossRole, sim.RoleOf(D), "the fourth joiner takes the boss role");
        }

        [Test]
        public void RolesFollowTheExistingRules_HostAsBoss()
        {
            LobbySim sim = Room(true, A, B, C, D);

            Assert.AreEqual(LobbySim.BossRole, sim.RoleOf(Host));
            Assert.AreEqual(1, sim.RoleOf(A));
            Assert.AreEqual(4, sim.RoleOf(D));
        }

        [Test]
        public void ASixthPlayerIsRefused_AndNothingChanges()
        {
            LobbySim sim = Room(false, A, B, C, D);

            LobbyResult result = sim.Join(E, false, out _);

            Assert.AreEqual(LobbyResult.RoomFull, result);
            Assert.AreEqual(5, sim.Count);
        }

        [Test]
        public void JoiningTwiceChangesNothing()
        {
            LobbySim sim = Room(false, A);
            Assert.AreEqual(LobbyResult.NoChange, sim.Join(A, false, out byte role));
            Assert.AreEqual(2, role);
            Assert.AreEqual(2, sim.Count);
        }

        [Test]
        public void NobodyHasACharacterWhenTheyJoin()
        {
            LobbySim sim = Room(false, A);
            Assert.AreEqual(CharacterIds.None, sim.CharacterOf(Host));
            Assert.AreEqual(CharacterIds.None, sim.CharacterOf(A));
        }

        // ---- 2.1 choosing ----

        [Test]
        public void ChoosingAFreeCharacter_HoldsIt()
        {
            LobbySim sim = Room(false, A);

            Assert.AreEqual(LobbyResult.Ok, sim.SelectCharacter(A, Marisa));

            Assert.AreEqual(Marisa, sim.CharacterOf(A));
            Assert.IsTrue(sim.HolderOf(Marisa, out ulong holder));
            Assert.AreEqual(A, holder);
        }

        [Test]
        public void TheFirstRequestWins_TheSecondIsToldItIsTaken()
        {
            LobbySim sim = Room(false, A);

            Assert.AreEqual(LobbyResult.Ok, sim.SelectCharacter(Host, Reimu));
            Assert.AreEqual(LobbyResult.CharacterTaken, sim.SelectCharacter(A, Reimu));

            Assert.AreEqual(Reimu, sim.CharacterOf(Host));
            Assert.AreEqual(CharacterIds.None, sim.CharacterOf(A));
        }

        [Test]
        public void ARefusedChoiceKeepsThePreviousChoice()
        {
            LobbySim sim = Room(false, A);
            sim.SelectCharacter(Host, Reimu);
            sim.SelectCharacter(A, Marisa);

            Assert.AreEqual(LobbyResult.CharacterTaken, sim.SelectCharacter(A, Reimu));

            Assert.AreEqual(Marisa, sim.CharacterOf(A));
            Assert.AreEqual(Reimu, sim.CharacterOf(Host));
        }

        [Test]
        public void SwitchingFreesTheOldCharacterAtOnce()
        {
            LobbySim sim = Room(false, A);
            sim.SelectCharacter(Host, Reimu);

            Assert.AreEqual(LobbyResult.Ok, sim.SelectCharacter(Host, Sakuya));

            Assert.AreEqual(Sakuya, sim.CharacterOf(Host));
            Assert.AreEqual(LobbyResult.Ok, sim.SelectCharacter(A, Reimu), "the left-behind character is free for anyone");
        }

        [Test]
        public void ReleasingLeavesTheSurvivorWithNothing()
        {
            LobbySim sim = Room(false);
            sim.SelectCharacter(Host, Reimu);

            Assert.AreEqual(LobbyResult.Ok, sim.SelectCharacter(Host, CharacterIds.None));

            Assert.AreEqual(CharacterIds.None, sim.CharacterOf(Host));
            Assert.IsFalse(sim.HolderOf(Reimu, out _));
            Assert.IsFalse(sim.IsReadyToStart);
        }

        [Test]
        public void ChoosingTheSameCharacterAgainChangesNothing()
        {
            LobbySim sim = Room(false);
            sim.SelectCharacter(Host, Reimu);
            Assert.AreEqual(LobbyResult.NoChange, sim.SelectCharacter(Host, Reimu));
            Assert.AreEqual(Reimu, sim.CharacterOf(Host));

            sim.SelectCharacter(Host, CharacterIds.None);
            Assert.AreEqual(LobbyResult.NoChange, sim.SelectCharacter(Host, CharacterIds.None), "releasing nothing changes nothing");
        }

        [Test]
        public void AnUnknownCharacterNumberIsIgnored()
        {
            LobbySim sim = Room(false);
            Assert.AreEqual(LobbyResult.UnknownCharacter, sim.SelectCharacter(Host, 9));
            Assert.AreEqual(LobbyResult.UnknownCharacter, sim.SelectCharacter(Host, 255));
            Assert.AreEqual(CharacterIds.None, sim.CharacterOf(Host));
        }

        [Test]
        public void TheBossCannotChoose()
        {
            LobbySim sim = Room(true, A);
            Assert.AreEqual(LobbyResult.NotSurvivor, sim.SelectCharacter(Host, Reimu));
            Assert.AreEqual(CharacterIds.None, sim.CharacterOf(Host));
        }

        [Test]
        public void SomeoneWhoIsNotInTheRoomCannotChoose()
        {
            LobbySim sim = Room(false);
            Assert.AreEqual(LobbyResult.UnknownPlayer, sim.SelectCharacter(99, Reimu));
            Assert.IsFalse(sim.HolderOf(Reimu, out _));
        }

        [Test]
        public void ChoosingWhilePlayingIsIgnored()
        {
            LobbySim sim = Room(false);
            sim.SelectCharacter(Host, Reimu);
            sim.TryStart(Host);

            Assert.AreEqual(LobbyResult.WrongPhase, sim.SelectCharacter(Host, Marisa));
            Assert.AreEqual(Reimu, sim.CharacterOf(Host));
        }

        [Test]
        public void AllFourCharactersCanBeHeldAtOnce_ByDifferentPlayers()
        {
            LobbySim sim = Room(false, A, B, C);
            sim.SelectCharacter(Host, Reimu);
            sim.SelectCharacter(A, Marisa);
            sim.SelectCharacter(B, Sakuya);
            sim.SelectCharacter(C, Sanae);

            foreach (byte c in new[] { Reimu, Marisa, Sakuya, Sanae })
                Assert.IsTrue(sim.HolderOf(c, out _));
        }

        [Test]
        public void ALeavingPlayerGivesUpTheCharacter()
        {
            LobbySim sim = Room(false, A);
            sim.SelectCharacter(A, Sakuya);

            Assert.IsTrue(sim.Leave(A));

            Assert.IsFalse(sim.HolderOf(Sakuya, out _));
            Assert.AreEqual(LobbyResult.Ok, sim.SelectCharacter(Host, Sakuya));
            Assert.IsFalse(sim.Leave(A), "leaving twice is harmless");
        }

        // ---- 2.2 starting ----

        [Test]
        public void TheHostStartsOnceEverySurvivorHasChosen()
        {
            LobbySim sim = Room(false, A);
            sim.SelectCharacter(Host, Reimu);
            sim.SelectCharacter(A, Marisa);

            Assert.IsTrue(sim.IsReadyToStart);
            Assert.AreEqual(LobbyResult.Ok, sim.TryStart(Host));
            Assert.AreEqual(RoomPhase.Playing, sim.Phase);
        }

        [Test]
        public void ASurvivorWithoutACharacterBlocksTheStart()
        {
            LobbySim sim = Room(false, A);
            sim.SelectCharacter(Host, Reimu);

            Assert.IsFalse(sim.IsReadyToStart);
            Assert.AreEqual(LobbyResult.NotReady, sim.TryStart(Host));
            Assert.AreEqual(RoomPhase.Waiting, sim.Phase);
        }

        [Test]
        public void AClientCannotStartTheMatch()
        {
            LobbySim sim = Room(false, A);
            sim.SelectCharacter(Host, Reimu);
            sim.SelectCharacter(A, Marisa);

            Assert.AreEqual(LobbyResult.NotHost, sim.TryStart(A));
            Assert.AreEqual(RoomPhase.Waiting, sim.Phase);
        }

        [Test]
        public void ARoomWithOnlyTheBossCannotStart()
        {
            LobbySim sim = Room(true);
            Assert.IsFalse(sim.IsReadyToStart);
            Assert.AreEqual(LobbyResult.NotReady, sim.TryStart(Host));
        }

        [Test]
        public void TheBossNeedsNoCharacterToStart()
        {
            LobbySim sim = Room(true, A);
            sim.SelectCharacter(A, Reimu);

            Assert.IsTrue(sim.IsReadyToStart);
            Assert.AreEqual(LobbyResult.Ok, sim.TryStart(Host));
        }

        [Test]
        public void StartingTwiceIsRefused()
        {
            LobbySim sim = Room(false);
            sim.SelectCharacter(Host, Reimu);
            sim.TryStart(Host);

            Assert.AreEqual(LobbyResult.WrongPhase, sim.TryStart(Host));
            Assert.IsFalse(sim.IsReadyToStart, "a running match is not ready to start");
        }

        // ---- 2.2 a running match ----

        [Test]
        public void ARunningMatchRefusesNewPlayers_AndNothingChangesForTheOthers()
        {
            LobbySim sim = Room(false, A);
            sim.SelectCharacter(Host, Reimu);
            sim.SelectCharacter(A, Marisa);
            sim.TryStart(Host);

            Assert.AreEqual(LobbyResult.MatchRunning, sim.Join(B, false, out _));

            Assert.AreEqual(2, sim.Count);
            Assert.IsFalse(sim.Contains(B));
        }

        [Test]
        public void ARunningMatchKeepsGoingWhenAClientLeaves_AndThatClientCannotRejoin()
        {
            LobbySim sim = Room(false, A, B);
            sim.SelectCharacter(Host, Reimu);
            sim.SelectCharacter(A, Marisa);
            sim.SelectCharacter(B, Sakuya);
            sim.TryStart(Host);

            sim.Leave(A);

            Assert.AreEqual(RoomPhase.Playing, sim.Phase);
            Assert.IsFalse(sim.Contains(A));
            Assert.AreEqual(LobbyResult.MatchRunning, sim.Join(A, false, out _));
        }

        [Test]
        public void TheHostEndsTheMatch_EveryoneStaysAndEveryChoiceStays()
        {
            LobbySim sim = Room(false, A);
            sim.SelectCharacter(Host, Reimu);
            sim.SelectCharacter(A, Marisa);
            sim.TryStart(Host);

            Assert.AreEqual(LobbyResult.Ok, sim.TryEndMatch(Host));

            Assert.AreEqual(RoomPhase.Waiting, sim.Phase);
            Assert.AreEqual(2, sim.Count);
            Assert.AreEqual(Reimu, sim.CharacterOf(Host));
            Assert.AreEqual(Marisa, sim.CharacterOf(A));
            Assert.AreEqual(LobbyResult.Ok, sim.SelectCharacter(A, Sanae), "choices can be changed again");
        }

        [Test]
        public void AClientCannotEndTheMatch()
        {
            LobbySim sim = Room(false, A);
            sim.SelectCharacter(Host, Reimu);
            sim.SelectCharacter(A, Marisa);
            sim.TryStart(Host);

            Assert.AreEqual(LobbyResult.NotHost, sim.TryEndMatch(A));
            Assert.AreEqual(RoomPhase.Playing, sim.Phase);
        }

        [Test]
        public void EndingAWaitingRoomIsRefused()
        {
            LobbySim sim = Room(false);
            Assert.AreEqual(LobbyResult.WrongPhase, sim.TryEndMatch(Host));
        }

        [Test]
        public void ASecondMatchCanStartAfterReturning_AndLateJoinersAreAcceptedAgain()
        {
            LobbySim sim = Room(false, A);
            sim.SelectCharacter(Host, Reimu);
            sim.SelectCharacter(A, Marisa);
            sim.TryStart(Host);
            sim.TryEndMatch(Host);

            Assert.AreEqual(LobbyResult.Ok, sim.Join(B, false, out _));
            sim.SelectCharacter(B, Sakuya);
            Assert.AreEqual(LobbyResult.Ok, sim.TryStart(Host));
            Assert.AreEqual(3, sim.SurvivorCount);
        }

        [Test]
        public void ClearEmptiesTheRoomForTheNextOne()
        {
            LobbySim sim = Room(false, A);
            sim.SelectCharacter(Host, Reimu);
            sim.TryStart(Host);

            sim.Clear();

            Assert.AreEqual(0, sim.Count);
            Assert.AreEqual(RoomPhase.Waiting, sim.Phase);
        }

        [Test]
        public void ARandomSequenceOfRequestsNeverBreaksTheOneHolderRule()
        {
            var rng = new System.Random(7);
            LobbySim sim = Room(false, A, B, C);
            ulong[] players = { Host, A, B, C, 77 };

            for (int i = 0; i < 2000; i++)
            {
                ulong who = players[rng.Next(players.Length)];
                switch (rng.Next(5))
                {
                    case 0: sim.SelectCharacter(who, (byte)rng.Next(0, 7)); break;
                    case 1: sim.Leave(who); break;
                    case 2: sim.Join(who, false, out _); break;
                    case 3: sim.TryStart(rng.Next(2) == 0 ? Host : A); break;
                    default: sim.TryEndMatch(rng.Next(2) == 0 ? Host : A); break;
                }

                var seen = new System.Collections.Generic.HashSet<byte>();
                foreach (LobbySlot slot in sim.Slots)
                {
                    if (slot.Character == CharacterIds.None) continue;
                    Assert.IsTrue(seen.Add(slot.Character), "a character is held by at most one player");
                    Assert.IsTrue(CharacterCatalog.IsValid(slot.Character));
                    Assert.AreNotEqual(LobbySim.BossRole, slot.Role, "the boss never holds a character");
                }
            }
        }
    }
}
