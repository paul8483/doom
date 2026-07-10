using NUnit.Framework;

namespace Doom.Game.Tests
{
    public class HealthModelTests
    {
        [Test]
        public void New_model_starts_full_health_no_armor()
        {
            var h = new HealthModel();
            Assert.That(h.Health, Is.EqualTo(100));
            Assert.That(h.Armor, Is.EqualTo(0));
            Assert.That(h.ArmorType, Is.EqualTo(ArmorKind.None));
            Assert.That(h.IsDead, Is.False);
        }

        [Test]
        public void Damage_without_armor_reduces_health_fully()
        {
            var h = new HealthModel();
            h.ApplyDamage(30);
            Assert.That(h.Health, Is.EqualTo(70));
        }

        [Test]
        public void Health_clamps_at_zero_and_is_dead()
        {
            var h = new HealthModel();
            h.ApplyDamage(250);
            Assert.That(h.Health, Is.EqualTo(0));
            Assert.That(h.IsDead, Is.True);
        }

        [Test]
        public void Green_armor_absorbs_one_third()
        {
            var h = new HealthModel(100, 100, ArmorKind.Green);
            h.ApplyDamage(30);            // saved = 30/3 = 10
            Assert.That(h.Health, Is.EqualTo(80));
            Assert.That(h.Armor, Is.EqualTo(90));
        }

        [Test]
        public void Blue_armor_absorbs_one_half()
        {
            var h = new HealthModel(100, 100, ArmorKind.Blue);
            h.ApplyDamage(30);            // saved = 30/2 = 15
            Assert.That(h.Health, Is.EqualTo(85));
            Assert.That(h.Armor, Is.EqualTo(85));
        }

        [Test]
        public void Armor_runs_out_then_full_damage_to_health()
        {
            var h = new HealthModel(100, 5, ArmorKind.Green);
            h.ApplyDamage(30);            // saved would be 10, but only 5 armor left
            Assert.That(h.Armor, Is.EqualTo(0));
            Assert.That(h.ArmorType, Is.EqualTo(ArmorKind.None));
            Assert.That(h.Health, Is.EqualTo(75)); // 100 - (30 - 5)
        }

        [Test]
        public void Reset_restores_full_health_and_clears_armor()
        {
            var h = new HealthModel(10, 50, ArmorKind.Blue);
            h.Reset();
            Assert.That(h.Health, Is.EqualTo(100));
            Assert.That(h.Armor, Is.EqualTo(0));
            Assert.That(h.ArmorType, Is.EqualTo(ArmorKind.None));
        }

        [Test]
        public void GiveHealth_stim_caps_at_100()
        {
            var h = new HealthModel(90, 0, ArmorKind.None);
            Assert.That(h.GiveHealth(10, cap: 100), Is.True);
            Assert.That(h.Health, Is.EqualTo(100));
            Assert.That(h.GiveHealth(10, cap: 100), Is.False);
            Assert.That(h.Health, Is.EqualTo(100));
        }

        [Test]
        public void GiveHealth_bonus_allows_up_to_200()
        {
            var h = new HealthModel(100, 0, ArmorKind.None);
            Assert.That(h.GiveHealth(1, cap: 200), Is.True);
            Assert.That(h.Health, Is.EqualTo(101));
        }

        [Test]
        public void GiveArmor_green_and_blue_rules()
        {
            var h = new HealthModel();
            Assert.That(h.GiveArmor(ArmorKind.Green), Is.True);
            Assert.That(h.Armor, Is.EqualTo(100));
            Assert.That(h.ArmorType, Is.EqualTo(ArmorKind.Green));
            Assert.That(h.GiveArmor(ArmorKind.Green), Is.False);

            Assert.That(h.GiveArmor(ArmorKind.Blue), Is.True);
            Assert.That(h.Armor, Is.EqualTo(200));
            Assert.That(h.ArmorType, Is.EqualTo(ArmorKind.Blue));
            Assert.That(h.GiveArmor(ArmorKind.Green), Is.False);
            Assert.That(h.GiveArmor(ArmorKind.Blue), Is.False);
        }

        [Test]
        public void GiveArmorBonus_sets_green_if_none()
        {
            var h = new HealthModel();
            Assert.That(h.GiveArmorBonus(1), Is.True);
            Assert.That(h.Armor, Is.EqualTo(1));
            Assert.That(h.ArmorType, Is.EqualTo(ArmorKind.Green));
        }
    }
}
