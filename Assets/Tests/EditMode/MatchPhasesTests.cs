using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class MatchPhasesTests
    {
        static int OrderOf(Type type)
        {
            var attribute = type.GetCustomAttribute<DefaultExecutionOrder>();
            Assert.IsNotNull(attribute, type.Name);
            return attribute.order;
        }

        static Type ServerOf(MatchPhase phase)
        {
            switch (phase)
            {
                case MatchPhase.Mobs: return typeof(MobServer);
                case MatchPhase.Summon: return typeof(SummonServer);
                case MatchPhase.Combat: return typeof(CombatServer);
                case MatchPhase.Growth: return typeof(GrowthServer);
                case MatchPhase.Clock: return typeof(RoleTable);
                default: throw new ArgumentOutOfRangeException(nameof(phase));
            }
        }

        [Test]
        public void EveryServerComponentRunsAtTheOrderOfItsPhase()
        {
            for (int i = 0; i < MatchPhases.All.Count; i++)
            {
                MatchPhases.Entry entry = MatchPhases.All[i];
                Assert.AreEqual(entry.ExecutionOrder, OrderOf(ServerOf(entry.Phase)), entry.Phase.ToString());
            }
        }

        [Test]
        public void TheExecutionOrdersRiseInTheOrderTheStepRunsThePhases()
        {
            Assert.IsTrue(MatchPhases.IsConsistent(MatchPhases.All));
        }

        [Test]
        public void EveryPhaseIsListedOnceInTheEnumOrder()
        {
            Assert.AreEqual(Enum.GetValues(typeof(MatchPhase)).Length, MatchPhases.All.Count);
            for (int i = 0; i < MatchPhases.All.Count; i++) Assert.AreEqual((MatchPhase)i, MatchPhases.All[i].Phase);
        }

        [Test]
        public void ATableInTheWrongOrderIsCaught()
        {
            var reversed = new List<MatchPhases.Entry>(MatchPhases.All);
            reversed.Reverse();
            Assert.IsFalse(MatchPhases.IsConsistent(reversed));

            var swapped = new List<MatchPhases.Entry>(MatchPhases.All);
            (swapped[1], swapped[2]) = (swapped[2], swapped[1]);
            Assert.IsFalse(MatchPhases.IsConsistent(swapped));

            var tied = new List<MatchPhases.Entry>
            {
                new MatchPhases.Entry(MatchPhase.Mobs, -10), new MatchPhases.Entry(MatchPhase.Summon, -10),
            };
            Assert.IsFalse(MatchPhases.IsConsistent(tied));
        }

        [Test]
        public void TheStepRunsEveryPhaseOnceInTheListedOrder()
        {
            var world = new MatchWorld { MatchRunning = true };
            world.Trace = new List<MatchPhase>();

            world.Step(0.02f);

            Assert.AreEqual(MatchPhases.All.Count, world.Trace.Count);
            for (int i = 0; i < MatchPhases.All.Count; i++) Assert.AreEqual(MatchPhases.All[i].Phase, world.Trace[i]);
        }

        [Test]
        public void TheWorldHoldsEverySimulationTheServerUses()
        {
            var world = new MatchWorld();

            Assert.IsNotNull(world.Mobs);
            Assert.IsNotNull(world.Summon);
            Assert.IsNotNull(world.Survivors);
            Assert.IsNotNull(world.Attacks);
            Assert.IsNotNull(world.Projectiles);
            Assert.IsNotNull(world.Boss);
            Assert.IsNotNull(world.BossSkills);
            Assert.IsNotNull(world.MobActions);
            Assert.IsNotNull(world.Leveling);
            Assert.IsNotNull(world.Skills);
            Assert.IsNotNull(world.Clock);
        }
    }
}
