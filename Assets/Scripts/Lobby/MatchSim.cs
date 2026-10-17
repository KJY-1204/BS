using System;

namespace BS
{
    public enum MatchWinner : byte
    {
        None = 0,
        Survivors = 1,
        Boss = 2,
    }

    public enum MatchReason : byte
    {
        None = 0,
        BossDefeated = 1,
        BossLeft = 2,
        AllSurvivorsFallen = 3,
        TimeUp = 4,
    }

    /// <summary>What the outcome needs to know each step.</summary>
    public struct MatchInput
    {
        /// <summary>The boss has been defeated (health reached zero).</summary>
        public bool BossFell;
        /// <summary>The boss player disconnected during the match.</summary>
        public bool BossLeft;
        /// <summary>How many survivors are alive; fallen survivors and survivors who left do not count.</summary>
        public int SurvivorsAlive;
    }

    /// <summary>
    /// The time limit and the winner of a match, as plain data. A match with a boss is a race: the survivors win by
    /// defeating the boss, the boss wins by defeating every survivor or by outlasting the clock. A match without a
    /// boss (practice) has no clock and no winner. Once decided the result never changes.
    /// </summary>
    public sealed class MatchSim
    {
        /// <summary>False for a practice match: no countdown and no result.</summary>
        public bool Enabled { get; private set; }

        public float TimeLimit { get; private set; }
        public float TimeLeft { get; private set; }

        public MatchWinner Winner { get; private set; }
        public MatchReason Reason { get; private set; }

        public bool IsDecided => Winner != MatchWinner.None;

        /// <summary>Starts a match. A time limit of zero or less means a practice match with no clock.</summary>
        public void Start(bool withBoss, float timeLimitSeconds)
        {
            Enabled = withBoss && timeLimitSeconds > 0f;
            TimeLimit = Enabled ? timeLimitSeconds : 0f;
            TimeLeft = TimeLimit;
            Winner = MatchWinner.None;
            Reason = MatchReason.None;
        }

        /// <summary>No match (the room is waiting).</summary>
        public void Clear()
        {
            Enabled = false;
            TimeLimit = 0f;
            TimeLeft = 0f;
            Winner = MatchWinner.None;
            Reason = MatchReason.None;
        }

        /// <summary>
        /// Advances the clock and decides the winner if it is decided this step. The order matters and is fixed:
        /// the boss falling beats the boss leaving, which beats every survivor falling, which beats the time running out.
        /// Returns true on the step that decides the match.
        /// </summary>
        public bool Step(float dt, in MatchInput input)
        {
            if (!Enabled || IsDecided) return false;

            TimeLeft = Math.Max(0f, TimeLeft - dt);

            if (input.BossFell) return Decide(MatchWinner.Survivors, MatchReason.BossDefeated);
            if (input.BossLeft) return Decide(MatchWinner.Survivors, MatchReason.BossLeft);
            if (input.SurvivorsAlive <= 0) return Decide(MatchWinner.Boss, MatchReason.AllSurvivorsFallen);
            if (TimeLeft <= 0f) return Decide(MatchWinner.Boss, MatchReason.TimeUp);
            return false;
        }

        bool Decide(MatchWinner winner, MatchReason reason)
        {
            Winner = winner;
            Reason = reason;
            return true;
        }

        /// <summary>The time left in whole seconds, rounded up, for showing and replicating.</summary>
        public int SecondsLeft => (int)Math.Ceiling(TimeLeft);
    }
}
