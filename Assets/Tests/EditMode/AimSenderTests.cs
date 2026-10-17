using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class AimSenderTests
    {
        const float Frame = 1f / 60f;

        [Test]
        public void StandingStill_SendsNothing()
        {
            var sender = new AimSender();
            int sends = 0;
            for (int i = 0; i < 600; i++)
                if (sender.ShouldSend(i * Frame, 1000, false)) sends++;

            Assert.AreEqual(0, sends);
        }

        [Test]
        public void HoldingFireAtAFixedAim_SendsOnlyThePress()
        {
            var sender = new AimSender();
            int sends = 0;
            for (int i = 0; i < 600; i++)
                if (sender.ShouldSend(i * Frame, 1000, true)) sends++;

            Assert.AreEqual(1, sends);
        }

        [Test]
        public void ThePressIsSentWithoutDelayWhenNothingWasSentBefore()
        {
            var sender = new AimSender();
            Assert.IsTrue(sender.ShouldSend(0.001f, 5000, true));
        }

        [Test]
        public void SweepingTheMouseWhileFiring_NeverExceedsTwentyUpdatesPerSecond()
        {
            var sender = new AimSender();
            int sends = 0;
            // 5 degrees per frame for 5 seconds, far more movement than needed.
            for (int i = 0; i < 300; i++)
            {
                ushort angle = AimMath.QuantizeAngle(i * 5f * Mathf.Deg2Rad);
                if (sender.ShouldSend(i * Frame, angle, true)) sends++;
            }

            Assert.LessOrEqual(sends, 5 * 20 + 1);
            Assert.Greater(sends, 5 * 15, "still follows the aim closely");
        }

        [Test]
        public void MashingFire_NeverExceedsTwentyUpdatesPerSecond()
        {
            var sender = new AimSender();
            int sends = 0;
            for (int i = 0; i < 300; i++)
                if (sender.ShouldSend(i * Frame, 1000, i % 2 == 0)) sends++;

            Assert.LessOrEqual(sends, 5 * 20 + 1);
        }

        [Test]
        public void TheFinalReleaseIsAlwaysSentEventually()
        {
            var sender = new AimSender();
            sender.ShouldSend(0f, 1000, true);

            bool released = false;
            for (int i = 1; i < 10 && !released; i++)
                released = sender.ShouldSend(i * Frame, 1000, false);

            Assert.IsTrue(released, "fire is released within a tenth of a second");
        }

        [Test]
        public void TinyAimChanges_AreNotSent()
        {
            var sender = new AimSender();
            sender.ShouldSend(0f, 1000, true);

            int sends = 0;
            for (int i = 1; i < 120; i++)
                if (sender.ShouldSend(i * Frame, (ushort)(1000 + (i % 3)), true)) sends++;

            Assert.AreEqual(0, sends, "a few hundredths of a degree is not worth a message");
        }

        [Test]
        public void AimChangesOfAboutADegree_AreSent()
        {
            var sender = new AimSender();
            sender.ShouldSend(0f, 1000, true);

            Assert.IsTrue(sender.ShouldSend(0.2f, (ushort)(1000 + AimSender.AngleStep + 5), true));
        }

        [Test]
        public void TheAngleWrapsAroundCorrectly()
        {
            var sender = new AimSender();
            sender.ShouldSend(0f, 65530, true);

            Assert.IsFalse(sender.ShouldSend(0.2f, 4, true), "10 units apart across the wrap point is not a real change");
            Assert.IsTrue(sender.ShouldSend(0.4f, (ushort)(AimSender.AngleStep + 100), true));
        }

        [Test]
        public void Reset_SendsTheStateAgain()
        {
            var sender = new AimSender();
            sender.ShouldSend(0f, 1000, true);
            Assert.IsFalse(sender.ShouldSend(0.5f, 1000, true));

            sender.Reset();

            Assert.IsTrue(sender.ShouldSend(0.6f, 1000, true));
        }
    }
}
