using NUnit.Framework;

namespace BS.Tests
{
    public class LobbyFinishedTests
    {
        const ulong Host = 0, A = 1, B = 2, C = 3;
        const byte Reimu = CharacterIds.Reimu, Marisa = CharacterIds.Marisa, Sakuya = CharacterIds.Sakuya;

        /// <summary>A playing room: the host is the boss, A and B are survivors with characters.</summary>
        static LobbySim Playing()
        {
            var sim = new LobbySim(Host);
            sim.Join(Host, true, out _);
            sim.Join(A, false, out _);
            sim.Join(B, false, out _);
            sim.SelectCharacter(A, Reimu);
            sim.SelectCharacter(B, Marisa);
            Assert.AreEqual(LobbyResult.Ok, sim.TryStart(Host));
            return sim;
        }

        [Test]
        public void ARunningMatchCanFinish_AndOnlyOnce()
        {
            LobbySim sim = Playing();

            Assert.IsTrue(sim.Finish());
            Assert.AreEqual(RoomPhase.Finished, sim.Phase);
            Assert.IsFalse(sim.Finish(), "a finished room cannot finish again");
            Assert.AreEqual(RoomPhase.Finished, sim.Phase);
        }

        [Test]
        public void AWaitingRoomCannotFinish()
        {
            var sim = new LobbySim(Host);
            sim.Join(Host, false, out _);

            Assert.IsFalse(sim.Finish());
            Assert.AreEqual(RoomPhase.Waiting, sim.Phase);
        }

        [Test]
        public void AFinishedRoomRefusesNewPlayersAndLeavesTheOthersAlone()
        {
            LobbySim sim = Playing();
            sim.Finish();

            LobbyResult result = sim.Join(C, false, out _);

            Assert.AreEqual(LobbyResult.MatchRunning, result);
            Assert.IsFalse(sim.Contains(C));
            Assert.AreEqual(3, sim.Count);
        }

        [Test]
        public void ARoomThatIsFinishedCannotBeStartedOrChangeCharacters()
        {
            LobbySim sim = Playing();
            sim.Finish();

            Assert.AreEqual(LobbyResult.WrongPhase, sim.TryStart(Host));
            Assert.AreEqual(LobbyResult.WrongPhase, sim.SelectCharacter(A, Sakuya));
            Assert.AreEqual(Reimu, sim.CharacterOf(A));
            Assert.IsFalse(sim.IsReadyToStart, "a finished room is not ready to start");
            Assert.AreEqual(RoomPhase.Finished, sim.Phase);
        }

        [Test]
        public void TheHostReturnsAFinishedRoomToWaiting_WithEveryoneAndEveryChoice()
        {
            LobbySim sim = Playing();
            sim.Finish();

            Assert.AreEqual(LobbyResult.Ok, sim.TryEndMatch(Host));

            Assert.AreEqual(RoomPhase.Waiting, sim.Phase);
            Assert.AreEqual(3, sim.Count);
            Assert.AreEqual(Reimu, sim.CharacterOf(A));
            Assert.AreEqual(Marisa, sim.CharacterOf(B));
            Assert.AreEqual(LobbyResult.Ok, sim.SelectCharacter(A, Sakuya), "choices can be changed again");
        }

        [Test]
        public void AClientCannotReturnAFinishedRoom()
        {
            LobbySim sim = Playing();
            sim.Finish();

            Assert.AreEqual(LobbyResult.NotHost, sim.TryEndMatch(A));
            Assert.AreEqual(LobbyResult.NotHost, sim.TryEndMatch(B));
            Assert.AreEqual(RoomPhase.Finished, sim.Phase);
        }

        [Test]
        public void TheHostCanStillEndARunningMatchByHand()
        {
            LobbySim sim = Playing();

            Assert.AreEqual(LobbyResult.Ok, sim.TryEndMatch(Host));
            Assert.AreEqual(RoomPhase.Waiting, sim.Phase);
        }

        [Test]
        public void EndingAWaitingRoomIsStillRefused()
        {
            var sim = new LobbySim(Host);
            sim.Join(Host, false, out _);
            Assert.AreEqual(LobbyResult.WrongPhase, sim.TryEndMatch(Host));
        }

        [Test]
        public void ARoomCanHostAnotherMatchAfterAResult()
        {
            LobbySim sim = Playing();
            sim.Finish();
            sim.TryEndMatch(Host);

            Assert.AreEqual(LobbyResult.Ok, sim.Join(C, false, out _), "late joiners are accepted again while waiting");
            sim.SelectCharacter(C, Sakuya);
            Assert.AreEqual(LobbyResult.Ok, sim.TryStart(Host));
            Assert.AreEqual(RoomPhase.Playing, sim.Phase);
            Assert.IsTrue(sim.Finish(), "and it can finish again");
        }

        [Test]
        public void APlayerLeavingAFinishedRoomFreesTheCharacterAndKeepsTheResultState()
        {
            LobbySim sim = Playing();
            sim.Finish();

            Assert.IsTrue(sim.Leave(A));

            Assert.AreEqual(RoomPhase.Finished, sim.Phase);
            Assert.IsFalse(sim.HolderOf(Reimu, out _));
        }

        [Test]
        public void ClearEmptiesAFinishedRoomForTheNextOne()
        {
            LobbySim sim = Playing();
            sim.Finish();

            sim.Clear();

            Assert.AreEqual(0, sim.Count);
            Assert.AreEqual(RoomPhase.Waiting, sim.Phase);
        }

        [Test]
        public void ARandomSequenceOfRequestsNeverLeavesTheThreeStates()
        {
            var rng = new System.Random(5);
            var sim = new LobbySim(Host);
            sim.Join(Host, true, out _);
            ulong[] players = { Host, A, B, C };

            for (int i = 0; i < 3000; i++)
            {
                ulong who = players[rng.Next(players.Length)];
                switch (rng.Next(7))
                {
                    case 0: sim.Join(who, false, out _); break;
                    case 1: sim.Leave(who == Host ? A : who); break;
                    case 2: sim.SelectCharacter(who, (byte)rng.Next(0, 6)); break;
                    case 3: sim.TryStart(rng.Next(2) == 0 ? Host : A); break;
                    case 4: sim.TryEndMatch(rng.Next(2) == 0 ? Host : A); break;
                    case 5: sim.Finish(); break;
                    default: break;
                }

                Assert.IsTrue(sim.Phase == RoomPhase.Waiting || sim.Phase == RoomPhase.Playing || sim.Phase == RoomPhase.Finished);
                if (sim.Phase != RoomPhase.Waiting)
                {
                    int count = sim.Count;
                    sim.Join(99, false, out _);
                    Assert.AreEqual(count, sim.Count, "nobody joins a room that is not waiting");
                }
            }
        }
    }
}
