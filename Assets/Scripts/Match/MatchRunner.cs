using System;

namespace BS
{
    /// <summary>Plays harness matches in plain C#: one match from a seed, with a controller that decides what everyone does.</summary>
    public static class MatchRunner
    {
        /// <summary>Makes the controller of a match. The default one plays every character with the bots of the settings' skill profile.</summary>
        public static Func<MatchSession, IMatchController> DefaultControllerFactory { get; set; } =
            session => new BotController(session, SkillProfiles.Find(session.Settings.profile));

        /// <summary>
        /// Plays one match until it has a result and returns its row. The boss kind the game uses is set for the match and put back
        /// afterwards, whatever happens.
        /// </summary>
        public static MatchRow RunOne(HarnessSettings settings, int seed, Func<MatchSession, IMatchController> controllerFactory = null)
        {
            byte previousKind = BossCatalog.CurrentKind;
            try
            {
                MatchSession session = MatchSession.Start(settings, seed);
                IMatchController controller = (controllerFactory ?? DefaultControllerFactory)(session);
                var recorder = new MatchRecorder(seed);

                // The clock ends every match; the limit only guards against a match that could never end.
                float limit = session.BossData.MatchSeconds + 10f;
                int maxSteps = (int)(limit / session.Step) + 1;
                for (int step = 0; step < maxSteps; step++)
                {
                    controller.Decide(session, session.Step);
                    bool going = session.Advance();
                    recorder.Observe(session);
                    if (!going) break;
                }

                return recorder.Finish(session);
            }
            finally
            {
                BossCatalog.SetCurrentKind(previousKind);
            }
        }
    }
}
