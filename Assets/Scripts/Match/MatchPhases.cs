using System.Collections.Generic;

namespace BS
{
    /// <summary>The parts of one fixed step of a match, in the order the server advances them.</summary>
    public enum MatchPhase : byte
    {
        /// <summary>Mobs move, make mobs (devices) and expire.</summary>
        Mobs = 0,
        /// <summary>The boss's summon energy and cooldowns run.</summary>
        Summon = 1,
        /// <summary>Contact damage and revival, what mobs do on their own, the boss's skills, shots and projectiles.</summary>
        Combat = 2,
        /// <summary>Kills turn into experience, skills are used, levels and mana advance.</summary>
        Growth = 3,
        /// <summary>The clock runs and the match may get a result.</summary>
        Clock = 4,
    }

    /// <summary>
    /// The order of a match step, kept in one place. The server runs each phase from a component whose Unity execution order is
    /// the constant of its phase, and the harness runs them with <see cref="MatchWorld.Step"/> in the order of <see cref="All"/>;
    /// a test checks that both agree.
    /// </summary>
    public static class MatchPhases
    {
        public const int MobsOrder = -100;
        public const int SummonOrder = -90;
        public const int CombatOrder = -50;
        public const int GrowthOrder = -40;
        public const int ClockOrder = 0;

        public readonly struct Entry
        {
            public readonly MatchPhase Phase;
            public readonly int ExecutionOrder;

            public Entry(MatchPhase phase, int executionOrder)
            {
                Phase = phase;
                ExecutionOrder = executionOrder;
            }
        }

        /// <summary>The phases in the order a step runs them, each with the execution order of the server component that runs it.</summary>
        public static readonly IReadOnlyList<Entry> All = new[]
        {
            new Entry(MatchPhase.Mobs, MobsOrder),
            new Entry(MatchPhase.Summon, SummonOrder),
            new Entry(MatchPhase.Combat, CombatOrder),
            new Entry(MatchPhase.Growth, GrowthOrder),
            new Entry(MatchPhase.Clock, ClockOrder),
        };

        /// <summary>True when the execution orders rise strictly along the listed phase order (a phase listed earlier runs earlier).</summary>
        public static bool IsConsistent(IReadOnlyList<Entry> entries)
        {
            for (int i = 1; i < entries.Count; i++)
                if (entries[i].ExecutionOrder <= entries[i - 1].ExecutionOrder) return false;
            return true;
        }
    }
}
