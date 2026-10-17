using System.Linq;
using NUnit.Framework;

namespace BS.Tests
{
    public class CharacterAttackDataTests
    {
        static int Degrees(ushort angle) => UnityEngine.Mathf.RoundToInt(angle / 65536f * 360f) % 360;

        [Test]
        public void EveryAttackValueIsUsable()
        {
            foreach (CharacterDef c in CharacterCatalog.All)
            {
                AttackDef a = c.Attack;
                Assert.Greater(a.CooldownSeconds, 0f, c.Name);
                Assert.GreaterOrEqual(a.Shots, 1, c.Name);
                Assert.GreaterOrEqual(a.SpreadDegrees, 0f, c.Name);
                Assert.Less(a.SpreadDegrees, 360f, c.Name);
                Assert.Greater(a.DamageMultiplier, 0f, c.Name);
                Assert.Greater(a.Shape.Speed, 0f, c.Name);
                Assert.Greater(a.Shape.Lifetime, 0f, c.Name);
                Assert.Greater(a.Shape.Radius, 0f, c.Name);
                Assert.GreaterOrEqual(a.Shape.Pierce, 1, c.Name);
            }
        }

        [Test]
        public void TheFourAttacksDiffer()
        {
            var distinct = CharacterCatalog.All
                .Select(c => (c.Attack.CooldownSeconds, c.Attack.Shots, c.Attack.SpreadDegrees, c.Attack.DamageMultiplier,
                              c.Attack.Shape.Speed, c.Attack.Shape.Lifetime, c.Attack.Shape.Radius, c.Attack.Shape.Pierce))
                .Distinct().Count();
            Assert.AreEqual(4, distinct);
        }

        [Test]
        public void EachCharacterAttacksTheWayItIsMeantTo()
        {
            AttackDef reimu = CharacterCatalog.Get(CharacterIds.Reimu).Attack;
            AttackDef marisa = CharacterCatalog.Get(CharacterIds.Marisa).Attack;
            AttackDef sakuya = CharacterCatalog.Get(CharacterIds.Sakuya).Attack;
            AttackDef sanae = CharacterCatalog.Get(CharacterIds.Sanae).Attack;

            foreach (CharacterDef c in CharacterCatalog.All.Where(c => c.Id != CharacterIds.Reimu))
                Assert.Less(reimu.CooldownSeconds, c.Attack.CooldownSeconds, "Reimu attacks most often");
            Assert.Less(reimu.DamageMultiplier, 1f, "and each shot is weaker");

            Assert.Greater(marisa.Shape.Pierce, 1, "Marisa passes through several mobs");
            Assert.Greater(sakuya.Shots, 1, "Sakuya fires several shots");
            Assert.Greater(sakuya.SpreadDegrees, 0f);
            Assert.Less(sakuya.Shape.Range, marisa.Shape.Range, "with a short range");

            Assert.Greater(sanae.Shape.Radius, reimu.Shape.Radius * 2f, "Sanae's shot is large");
            Assert.Less(sanae.Shape.Speed, reimu.Shape.Speed, "and slow");
            Assert.Greater(sanae.DamageMultiplier, 1f, "and heavy");
        }

        [Test]
        public void TheDefaultAttackIsTheOldSingleShot()
        {
            AttackDef d = AttackDef.Default;
            Assert.AreEqual(CombatConfig.FireCooldownSeconds, d.CooldownSeconds);
            Assert.AreEqual(1, d.Shots);
            Assert.AreEqual(1f, d.DamageMultiplier);
            Assert.AreEqual(CombatConfig.ProjectileSpeed, d.Shape.Speed);
            Assert.AreEqual(CombatConfig.ProjectileLifetimeSeconds, d.Shape.Lifetime);
            Assert.AreEqual(CombatConfig.ProjectileRadius, d.Shape.Radius);
            Assert.AreEqual(1, d.Shape.Pierce);
        }

        [Test]
        public void FiveShotsOverFortyDegreesAimedAtNinety_LeaveAtSeventyToOneTen()
        {
            ushort aim = AimMath.QuantizeAngle(UnityEngine.Mathf.PI / 2f);
            var angles = Enumerable.Range(0, 5).Select(i => Degrees(AimMath.ShotAngle(aim, 5, 40f, i))).ToArray();
            CollectionAssert.AreEqual(new[] { 70, 80, 90, 100, 110 }, angles);
        }

        [Test]
        public void ASingleShotLeavesExactlyAlongTheAim()
        {
            for (int a = 0; a < 65536; a += 4099)
            {
                Assert.AreEqual((ushort)a, AimMath.ShotAngle((ushort)a, 1, 40f, 0));
                Assert.AreEqual((ushort)a, AimMath.ShotAngle((ushort)a, 1, 0f, 0));
            }
        }

        [Test]
        public void TheSpreadIsSymmetricAroundTheAim()
        {
            ushort aim = 12345;
            ushort first = AimMath.ShotAngle(aim, 5, 40f, 0);
            ushort last = AimMath.ShotAngle(aim, 5, 40f, 4);
            int below = (aim - first) & 0xFFFF;
            int above = (last - aim) & 0xFFFF;
            Assert.LessOrEqual(System.Math.Abs(below - above), 1);
            Assert.AreEqual(aim, AimMath.ShotAngle(aim, 5, 40f, 2));
        }

        [Test]
        public void AnglesWrapAroundAFullTurn()
        {
            Assert.AreEqual(Degrees(AimMath.Offset(AimMath.QuantizeAngle(0f), -20f)), 340);
            Assert.AreEqual(Degrees(AimMath.Offset(AimMath.QuantizeAngle(UnityEngine.Mathf.PI * 2f - 0.1f), 20f)), 14);
            Assert.AreEqual(Degrees(AimMath.Offset(0, 360f)), 0);
        }

        [Test]
        public void ARingOfShotsCoversTheWholeTurnEvenly()
        {
            int n = 12;
            var angles = Enumerable.Range(0, n).Select(i => (ushort)(i * 65536 / n)).Select(Degrees).ToArray();
            for (int i = 1; i < n; i++) Assert.AreEqual(30, angles[i] - angles[i - 1]);
        }
    }
}
