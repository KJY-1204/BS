using System.Collections.Generic;
using UnityEngine;

namespace BS
{
    /// <summary>What one run of the mobs' own actions did (for the server's statistics and for tests).</summary>
    public struct MobActionResult
    {
        public int ShotsFired;
        public int ShotsRefused;
        public int Blasts;
        public int SurvivorsHurt;
    }

    /// <summary>
    /// Turns what mobs did on their own into combat: a ranged mob's shot becomes a hostile projectile (a shot the projectile cap
    /// refuses gives the mob its cooldown back) and an exploding mob's blast hurts the survivors around it through the usual damage
    /// rule and becomes an explosion event for every client. Plain code, so the whole chain can be tested without a scene.
    /// </summary>
    public sealed class MobActionRunner
    {
        readonly List<MobShotRequest> shots = new List<MobShotRequest>();
        readonly List<MobBlast> blasts = new List<MobBlast>();

        /// <param name="blastEvents">Receives one explosion event per blast.</param>
        public MobActionResult Run(MobSim mobs, ProjectileSim projectiles, SurvivorSim survivors, IReadOnlyList<SurvivorBody> bodies,
                                   ulong shooterOwner, List<CombatEvent> blastEvents)
        {
            var result = new MobActionResult();

            shots.Clear();
            blasts.Clear();
            mobs.DrainActions(shots, blasts);

            for (int i = 0; i < shots.Count; i++)
            {
                MobShotRequest shot = shots[i];
                var spec = new ProjectileSpec
                {
                    Owner = shooterOwner, Origin = shot.Origin, Angle = shot.Angle, Hostile = true,
                    Damage = MobKinds.Get(shot.Kind).ShotDamage, Shape = MobShot.Shape,
                };
                if (projectiles.TryFire(spec, out _)) result.ShotsFired++;
                else
                {
                    mobs.RefundShot(shot.MobId);
                    result.ShotsRefused++;
                }
            }

            for (int i = 0; i < blasts.Count; i++)
            {
                MobBlast blast = blasts[i];
                result.SurvivorsHurt += survivors.ApplyBlast(blast.Position, blast.Radius, blast.Damage, bodies);
                result.Blasts++;
                blastEvents.Add(new CombatEvent
                {
                    Kind = CombatEventKind.Explosion, Position = blast.Position, Radius10 = CombatCodec.QuantizeRadius(blast.Radius),
                });
            }
            return result;
        }
    }
}
