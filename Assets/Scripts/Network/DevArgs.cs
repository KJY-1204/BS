using System;
using UnityEngine;

namespace BS
{
    /// <summary>
    /// Command-line switches for automated local testing (editor and development builds only):
    /// -bsHost [boss]   start a direct-IP host
    /// -bsJoin ip       join a direct-IP host
    /// -bsJoinCode code join a Relay room by its join code
    /// -bsMove x y      hold a fixed move input
    /// -bsQuit seconds  quit after the given time
    /// -bsLog           log the local avatar and camera positions once a second
    /// -bsDisplaceAt s  push the local (predicted) position 5 units away s seconds after spawn
    /// </summary>
    public static class DevArgs
    {
        public static bool Host { get; private set; }
        public static bool HostAsBoss { get; private set; }
        public static string Join { get; private set; }
        public static string JoinCode { get; private set; }
        public static Vector2? Move { get; private set; }
        public static float QuitAfter { get; private set; }
        public static bool Log { get; private set; }
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
