using System;
using System.Collections.Generic;

namespace BS
{
    /// <summary>The outcome and the numbers of one harness match: one row of the report.</summary>
    [Serializable]
    public class MatchRow
    {
        public int seed;
        /// <summary>survivors, boss or none (the match ran out of steps without a result, which should not happen).</summary>
        public string winner = "none";
        public string reason = "none";
        public float lengthSeconds;
        /// <summary>When the boss's health first fell to 50%, 25% and 0% of its maximum; -1 when it never did.</summary>
        public float bossT50 = -1f;
        public float bossT25 = -1f;
        public float bossT0 = -1f;
        /// <summary>When the match entered phase 2 and phase 3; -1 when it never did (or the phases were off).</summary>
        public float phaseT2 = -1f;
        public float phaseT3 = -1f;
        public float bossHealthEnd = 1f;
        /// <summary>When the first survivor fell; -1 when none did.</summary>
        public float firstFall = -1f;
        public int falls;
        public int revives;
        public int survivorsAliveEnd;
        public int maxLevel;
        public float meanLevel;
        public int devicesPlaced;
        public int devicesDestroyed;
        public int devicesExpired;
        /// <summary>A number that comes from the final state of the world (boss and survivor health, mobs, experience). The same seed gives the same number.</summary>
        public int stateHash;
    }

    /// <summary>
    /// Watches a match step by step and writes down what the report needs. It only reads the world: the boss's health, who is down, the
    /// levels and the devices. Call <see cref="Observe"/> after every step and <see cref="Finish"/> once at the end.
    /// </summary>
    public sealed class MatchRecorder
    {
        readonly Dictionary<ulong, bool> wasAlive = new Dictionary<ulong, bool>();
        readonly MatchRow row = new MatchRow();

        public MatchRecorder(int seed)
        {
            row.seed = seed;
        }

        public void Observe(MatchSession session)
        {
            MatchWorld world = session.World;
            float t = session.Time;

            if (world.Boss.Exists && world.Boss.MaxHealth > 0)
            {
                float fraction = world.Boss.Health / (float)world.Boss.MaxHealth;
                if (row.bossT50 < 0f && fraction <= 0.5f) row.bossT50 = t;
                if (row.bossT25 < 0f && fraction <= 0.25f) row.bossT25 = t;
                if (row.bossT0 < 0f && world.Boss.Health <= 0) row.bossT0 = t;
                if (row.phaseT2 < 0f && world.BossSkills.Phase >= BossPhases.Second) row.phaseT2 = t;
                if (row.phaseT3 < 0f && world.BossSkills.Phase >= BossPhases.Third) row.phaseT3 = t;
            }

            for (int i = 0; i < session.SurvivorIds.Count; i++)
            {
                ulong id = session.SurvivorIds[i];
                bool alive = world.Survivors.IsAlive(id);
                if (wasAlive.TryGetValue(id, out bool before))
                {
                    if (before && !alive)
                    {
                        row.falls++;
                        if (row.firstFall < 0f) row.firstFall = t;
                    }
                    else if (!before && alive)
                    {
                        row.revives++;
                    }
                }
                wasAlive[id] = alive;
            }
        }

        static int StateHash(MatchSession session)
        {
            MatchWorld w = session.World;
            unchecked
            {
                int h = 17;
                h = h * 31 + w.Boss.Health;
                for (int i = 0; i < session.SurvivorIds.Count; i++)
                {
                    ulong id = session.SurvivorIds[i];
                    h = h * 31 + w.Survivors.Health(id);
                    h = h * 31 + w.Leveling.Level(id);
                    h = h * 31 + w.Leveling.Xp(id);
                }
                h = h * 31 + w.Mobs.Count;
                for (int i = 0; i < w.Mobs.Count; i++)
                {
                    h = h * 31 + w.Mobs.IdAt(i);
                    h = h * 31 + (int)(w.Mobs.PositionAt(i).x * 1000f);
                    h = h * 31 + (int)(w.Mobs.PositionAt(i).y * 1000f);
                }
                return h;
            }
        }

        /// <summary>Writes the end of the match into the row and returns it.</summary>
        public MatchRow Finish(MatchSession session)
        {
            MatchWorld world = session.World;

            switch (world.Clock.Winner)
            {
                case MatchWinner.Survivors: row.winner = "survivors"; break;
                case MatchWinner.Boss: row.winner = "boss"; break;
                default: row.winner = "none"; break;
            }
            row.reason = world.Clock.Reason == MatchReason.None ? "none" : world.Clock.Reason.ToString();
            row.lengthSeconds = session.Time;
            row.bossHealthEnd = world.Boss.Exists && world.Boss.MaxHealth > 0 ? world.Boss.Health / (float)world.Boss.MaxHealth : 0f;
            row.survivorsAliveEnd = world.Survivors.AliveCount;

            int total = 0;
            for (int i = 0; i < session.SurvivorIds.Count; i++)
            {
                int level = world.Leveling.Level(session.SurvivorIds[i]);
                total += level;
                if (level > row.maxLevel) row.maxLevel = level;
            }
            row.meanLevel = session.SurvivorIds.Count > 0 ? total / (float)session.SurvivorIds.Count : 0f;

            row.devicesPlaced = world.Mobs.DevicesPlaced;
            row.devicesDestroyed = world.Mobs.DevicesDestroyed;
            row.devicesExpired = world.Mobs.DevicesExpired;
            row.stateHash = StateHash(session);
            return row;
        }
    }
}
