using System;
using UnityEngine;

namespace BS
{
    /// <summary>
    /// Command-line switches for automated local testing (editor and development builds only):
    /// -bsHost [boss]   start a direct-IP host
    /// -bsHostRelay [boss] start a Relay host (the join code is written to the log)
    /// -bsJoin ip       join a direct-IP host
    /// -bsJoinCode code join a Relay room by its join code
    /// -bsMove x y      hold a fixed move input
    /// -bsQuit seconds  quit after the given time
    /// -bsLog           log the local avatar and camera positions once a second
    /// -bsMobLog        log mob counts, traffic and timing every 5 seconds
    /// -bsMobTarget n   automatic mob spawning target (capped at the hard cap)
    /// -bsMobDump       every 5 s log all mob positions (server: sent state, client: what is displayed)
    /// -bsFire          hold fire all the time
    /// -bsAim deg       aim at a fixed angle in degrees (0 = right, 90 = up)
    /// -bsAimSweep d    turn the aim by d degrees per second
    /// -bsCombatLog     log health, revive gauge and combat statistics
    /// -bsGrowthLog     log levels, experience, skill choices and skill results
    /// -bsXpPerKill n   experience per kill (host)
    /// -bsOfferSeconds s  time allowed to pick a skill (host)
    /// -bsDevXp t n     (host) give every survivor n experience t seconds after the room starts
    /// -bsAutoPick i d  (owner) pick choice i (1 to 3) d seconds after a choice opens; 0 never picks
    /// -bsAutoSkill q|e s  (owner) press that skill key every s seconds
    /// -bsSurvivorHealth n  survivor maximum health (host), to keep survivors alive in load tests
    /// -bsMobSpeed v    mob speed (host)
    /// -bsMobHealth n   mob maximum health (host); a high value keeps mobs alive under fire for load tests
    /// -bsCombatDump    log every combat event with its time (server and clients) for comparison
    /// -bsDevKill r t   (host) kill the survivor with role r (1 to 4) t seconds after the room starts
    /// -bsDevMeet a b t (host) move survivor a next to survivor b t seconds after the room starts
    /// -bsMobChurn s    host removes a random mob every s seconds (exercises removal over the network)
    /// -bsMobDropEvents client ignores spawn/remove events (exercises provisional mobs and stale removal)
    /// -bsDisplaceAt s  push the local (predicted) position 5 units away s seconds after spawn
    /// </summary>
    public static class DevArgs
    {
        public static bool Host { get; private set; }
        public static bool HostRelay { get; private set; }
        public static bool HostAsBoss { get; private set; }
        public static string Join { get; private set; }
        public static string JoinCode { get; private set; }
        public static Vector2? Move { get; private set; }
        public static float QuitAfter { get; private set; }
        public static bool Log { get; private set; }
        public static bool MobLog { get; private set; }
        public static bool MobDump { get; private set; }
        public static bool Fire { get; private set; }
        public static float? AimDegrees { get; private set; }
        public static float AimSweepDegreesPerSecond { get; private set; }
        public static bool CombatLog { get; private set; }
        public static bool GrowthLog { get; private set; }
        public static int XpPerKill { get; private set; }
        public static float OfferSeconds { get; private set; }
        public static float DevXpTime { get; private set; } = -1f;
        public static int DevXpAmount { get; private set; }
        /// <summary>1 to 3: owner picks that choice automatically; 0 means never pick (let the time run out).</summary>
        public static int AutoPickIndex { get; private set; }
        public static float AutoPickDelay { get; private set; }
        public static int AutoSkillSlot { get; private set; } = -1;
        public static float AutoSkillInterval { get; private set; } = 1f;
        public static int SurvivorHealth { get; private set; }
        public static float MobSpeed { get; private set; }
        public static int MobHealth { get; private set; }
        public static bool CombatDump { get; private set; }
        public static int DevKillRole { get; private set; } = -1;
        public static float DevKillTime { get; private set; }
        public static int DevMeetRoleA { get; private set; } = -1;
        public static int DevMeetRoleB { get; private set; } = -1;
        public static float DevMeetTime { get; private set; }
        public static float MobChurn { get; private set; }
        public static bool MobDropEvents { get; private set; }
        /// <summary>Automatic spawn target from -bsMobTarget; null when not given (0 turns automatic spawning off).</summary>
        public static int? MobTarget { get; private set; }
        public static float DisplaceAt { get; private set; } = -1f;

        static DevArgs()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-bsHost":
                        Host = true;
                        HostAsBoss = i + 1 < args.Length && args[i + 1] == "boss";
                        break;
                    case "-bsHostRelay":
                        HostRelay = true;
                        HostAsBoss = i + 1 < args.Length && args[i + 1] == "boss";
                        break;
                    case "-bsJoin" when i + 1 < args.Length:
                        Join = args[i + 1];
                        break;
                    case "-bsJoinCode" when i + 1 < args.Length:
                        JoinCode = args[i + 1];
                        break;
                    case "-bsMove" when i + 2 < args.Length:
                        if (float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x) &&
                            float.TryParse(args[i + 2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y))
                            Move = new Vector2(x, y);
                        break;
                    case "-bsLog":
                        Log = true;
                        break;
                    case "-bsMobLog":
                        MobLog = true;
                        break;
                    case "-bsMobDump":
                        MobDump = true;
                        break;
                    case "-bsFire":
                        Fire = true;
                        break;
                    case "-bsCombatDump":
                        CombatDump = true;
                        break;
                    case "-bsSurvivorHealth" when i + 1 < args.Length:
                        int.TryParse(args[i + 1], out int sh);
                        SurvivorHealth = sh;
                        break;
                    case "-bsMobHealth" when i + 1 < args.Length:
                        int.TryParse(args[i + 1], out int mh);
                        MobHealth = mh;
                        break;
                    case "-bsMobSpeed" when i + 1 < args.Length:
                        float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ms);
                        MobSpeed = ms;
                        break;
                    case "-bsDevKill" when i + 2 < args.Length:
                        int.TryParse(args[i + 1], out int killRole);
                        float.TryParse(args[i + 2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float killTime);
                        DevKillRole = killRole;
                        DevKillTime = killTime;
                        break;
                    case "-bsDevMeet" when i + 3 < args.Length:
                        int.TryParse(args[i + 1], out int meetA);
                        int.TryParse(args[i + 2], out int meetB);
                        float.TryParse(args[i + 3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float meetTime);
                        DevMeetRoleA = meetA;
                        DevMeetRoleB = meetB;
                        DevMeetTime = meetTime;
                        break;
                    case "-bsCombatLog":
                        CombatLog = true;
                        break;
                    case "-bsGrowthLog":
                        GrowthLog = true;
                        break;
                    case "-bsXpPerKill" when i + 1 < args.Length:
                        if (int.TryParse(args[i + 1], out int xpk)) XpPerKill = xpk;
                        break;
                    case "-bsOfferSeconds" when i + 1 < args.Length:
                        if (float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float os)) OfferSeconds = os;
                        break;
                    case "-bsDevXp" when i + 2 < args.Length:
                        float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float dxt);
                        int.TryParse(args[i + 2], out int dxa);
                        DevXpTime = dxt;
                        DevXpAmount = dxa;
                        break;
                    case "-bsAutoPick" when i + 2 < args.Length:
                        int.TryParse(args[i + 1], out int api);
                        float.TryParse(args[i + 2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float apd);
                        AutoPickIndex = api;
                        AutoPickDelay = apd;
                        break;
                    case "-bsAutoSkill" when i + 2 < args.Length:
                        AutoSkillSlot = args[i + 1].ToLowerInvariant() == "e" ? 1 : 0;
                        float.TryParse(args[i + 2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float asi);
                        AutoSkillInterval = Mathf.Max(0.05f, asi);
                        break;
                    case "-bsAim" when i + 1 < args.Length:
                        if (float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float aim))
                            AimDegrees = aim;
                        break;
                    case "-bsAimSweep" when i + 1 < args.Length:
                        if (float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float sweep))
                            AimSweepDegreesPerSecond = sweep;
                        break;
                    case "-bsMobDropEvents":
                        MobDropEvents = true;
                        break;
                    case "-bsMobChurn" when i + 1 < args.Length:
                        if (float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float churn))
                            MobChurn = churn;
                        break;
                    case "-bsMobTarget" when i + 1 < args.Length:
                        if (int.TryParse(args[i + 1], out int mt)) MobTarget = mt;
                        break;
                    case "-bsDisplaceAt" when i + 1 < args.Length:
                        if (float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float d))
                            DisplaceAt = d;
                        break;
                    case "-bsQuit" when i + 1 < args.Length:
                        float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float q);
                        QuitAfter = q;
                        break;
                }
            }
#endif
        }
    }
}
