using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class CameraFitTests
    {
        [Test]
        public void AWideWindowIsLimitedByTheArenaHeightAndANarrowOneByItsWidth()
        {
            Assert.AreEqual(ArenaConfig.Height * 0.5f, CameraFollow.SizeToFitArena(16f / 9f), 0.0001f, "16:9 is wider than the arena shape: the height decides");
            Assert.AreEqual(ArenaConfig.Height * 0.5f, CameraFollow.SizeToFitArena(3f), 0.0001f, "ultra-wide: the height decides");
            Assert.AreEqual(ArenaConfig.Width * 0.5f, CameraFollow.SizeToFitArena(1f), 0.0001f, "square: the width decides");
            Assert.AreEqual(ArenaConfig.Width * 0.5f / 0.5f, CameraFollow.SizeToFitArena(0.5f), 0.0001f, "portrait: the width decides");
        }

        [Test]
        public void TheWholeArenaFitsForEveryAspectRatio()
        {
            for (float aspect = 0.4f; aspect <= 4f; aspect += 0.05f)
            {
                float halfHeight = CameraFollow.SizeToFitArena(aspect);
                float halfWidth = halfHeight * aspect;
                Assert.GreaterOrEqual(halfHeight + 0.0001f, ArenaConfig.Height * 0.5f, $"aspect {aspect}: height fits");
                Assert.GreaterOrEqual(halfWidth + 0.0001f, ArenaConfig.Width * 0.5f, $"aspect {aspect}: width fits");
            }
        }

        [Test]
        public void ANonPositiveAspectDoesNotBreakTheRule()
        {
            Assert.IsFalse(float.IsNaN(CameraFollow.SizeToFitArena(0f)));
            Assert.IsFalse(float.IsInfinity(CameraFollow.SizeToFitArena(-1f)));
        }
    }
}
