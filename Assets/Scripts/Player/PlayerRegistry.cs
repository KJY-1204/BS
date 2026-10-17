using System.Collections.Generic;
using UnityEngine;

namespace BS
{
    /// <summary>Server-side list of spawned player avatars, used to find the survivors mobs chase.</summary>
    public static class PlayerRegistry
    {
        static readonly List<PlayerAvatar> Avatars = new List<PlayerAvatar>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Avatars.Clear();

        public static int Count => Avatars.Count;

        public static void Register(PlayerAvatar avatar)
        {
            if (!Avatars.Contains(avatar)) Avatars.Add(avatar);
        }

        public static void Unregister(PlayerAvatar avatar) => Avatars.Remove(avatar);

        /// <summary>Fills the list with every survivor (the boss is never a default mob target).</summary>
        public static void CollectSurvivors(List<SurvivorInfo> into)
        {
            into.Clear();
            for (int i = 0; i < Avatars.Count; i++)
            {
                PlayerAvatar avatar = Avatars[i];
                if (avatar == null || !avatar.IsSpawned) continue;
                if (avatar.Role == RoleTable.BossRole) continue;

                into.Add(new SurvivorInfo
                {
                    ClientId = avatar.OwnerClientId,
                    Position = avatar.transform.position,
                    // Health and death arrive with combat; until then every survivor counts as alive.
                    Alive = true,
                });
            }
        }
    }
}
