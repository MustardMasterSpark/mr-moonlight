using NUnit.Framework;

namespace MrMoonlight.Player
{
    /// <summary>
    /// Proves MRM-12's modifier stacking rule: Value = (BaseValue + sum of additive) * (product
    /// of multiplicative). This is the "genuinely hard part" the issue's Model note calls out, so
    /// it gets an actual test rather than just a play-mode check - see Docs/csharp-conventions.md's
    /// testing section for why this project otherwise has no unit-test suite. Owner: MRM-12
    /// </summary>
    public class StatModifierStackingTests
    {
        [Test]
        public void AdditiveAndMultiplicativeModifiers_StackToDocumentedResult()
        {
            // Base defense 1.0, a +0.2 additive modifier (difficulty) and a x1.5 multiplicative
            // modifier (boots) applied simultaneously -> (1.0 + 0.2) * 1.5 = 1.8. This is the
            // worked example from Stat's class doc comment.
            var defense = new Stat(1.0f);
            var difficultySource = new object();
            var bootsSource = new object();

            defense.AddModifier(new StatModifier(difficultySource, StatModifierType.Additive, 0.2f));
            defense.AddModifier(new StatModifier(bootsSource, StatModifierType.Multiplicative, 1.5f));

            Assert.AreEqual(1.8f, defense.Value, 0.0001f);
        }

        [Test]
        public void RemovingOneSource_LeavesTheOtherModifierIntact()
        {
            var defense = new Stat(1.0f);
            var difficultySource = new object();
            var bootsSource = new object();

            defense.AddModifier(new StatModifier(difficultySource, StatModifierType.Additive, 0.2f));
            defense.AddModifier(new StatModifier(bootsSource, StatModifierType.Multiplicative, 1.5f));

            defense.RemoveModifiersFromSource(bootsSource);

            Assert.AreEqual(1.2f, defense.Value, 0.0001f);
        }

        [Test]
        public void TwoMultiplicativeModifiers_MultiplyTheirFactorsTogether()
        {
            // x1.5 and x1.2 together are x1.8, not a summed x1.7 - the stacking rule multiplies
            // factors, it does not add percentages.
            var stat = new Stat(10f);

            stat.AddModifier(new StatModifier(new object(), StatModifierType.Multiplicative, 1.5f));
            stat.AddModifier(new StatModifier(new object(), StatModifierType.Multiplicative, 1.2f));

            Assert.AreEqual(18f, stat.Value, 0.0001f);
        }

        [Test]
        public void Value_ClampsToMaxEvenWhenModifiersWouldExceedIt()
        {
            var health = new Stat(90f, 0f, 100f);
            health.AddModifier(new StatModifier(new object(), StatModifierType.Additive, 50f));

            Assert.AreEqual(100f, health.Value);
        }

        [Test]
        public void Lock_BypassesModifiersUntilUnlocked()
        {
            var stamina = new Stat(100f, 0f, 100f);
            stamina.AddModifier(new StatModifier(new object(), StatModifierType.Additive, 50f));

            stamina.Lock(15f);
            Assert.AreEqual(15f, stamina.Value);

            stamina.Unlock();
            Assert.AreEqual(100f, stamina.Value, 0.0001f);
        }
    }
}
