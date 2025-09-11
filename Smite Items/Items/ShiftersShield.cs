using BepInEx.Configuration;
using R2API;
using RoR2;
using System;
using UnityEngine;
using static Smite_Items.Main;

namespace Smite_Items.Items
{
    public class ShiftersShield : ItemBase<ShiftersShield>
    {
        public ConfigEntry<float> PercentBonusDamage;
        public ConfigEntry<int> LowHealthArmor;
        public ConfigEntry<float> ShiftHealthThreshold;
        public override string ItemName => "Shifters Shield";

        public override string ItemLangTokenName => "SHIFTER_ITEM";

        public override string ItemPickupDesc => "Deal bonus damage while above half health, reduce incoming damage when below half health";

        public override string ItemFullDescription => $"While above <style=cIsHealth>{ShiftHealthThreshold.Value*100}% health</style>, increase damage by <style=cIsDamage>{PercentBonusDamage.Value*100}%</style> <style=cStack>(+{PercentBonusDamage.Value*100}% per stack)</style>." +
            $" While at or below <style=cIsHealth>{ShiftHealthThreshold.Value*100}% health</style>, <style=cIsHealing>increase armor</style> by <style=cIsHealing>{LowHealthArmor.Value}</style> <style=cStack>(+{LowHealthArmor.Value} per stack)</style>.";

        public override string ItemLore => "Item taken from Smite 2.";

        public override ItemTier Tier => ItemTier.Tier1;

        public override GameObject ItemModel => MainAssets.LoadAsset<GameObject>("ExampleItemPrefab.prefab");

        public override Sprite ItemIcon => MainAssets.LoadAsset<Sprite>("ExampleItemIcon.png");

        public override void Init(ConfigFile config)
        {
            CreateConfig(config);
            CreateLang();
            CreateItem();
            Hooks();
        }

        public override void CreateConfig(ConfigFile config)
        {
            PercentBonusDamage = config.Bind<float>("Item " + ItemName, "Percent bonus damage", 0.1f, "How much bonus damage is given when above the health threshold?");
            LowHealthArmor = config.Bind<int>("Item " + ItemName, "Low health armor", 10, "How much armor is given when below the health threshold?");
            ShiftHealthThreshold = config.Bind<float>("Item " + ItemName, "Shift health threshold", 0.5f, "What percentage of maximum health is treated as the threshold to swap conditions?");
        }

        public override ItemDisplayRuleDict CreateItemDisplayRules()
        {
            return new ItemDisplayRuleDict();
        }

        public override void Hooks()
        {
            RecalculateStatsAPI.GetStatCoefficients += RecalculateStats;
        }

        protected override void CreateLang()
        {
            LanguageAPI.Add("ITEM_" + ItemLangTokenName + "_NAME", "Shifter's Shield");
            LanguageAPI.Add("ITEM_" + ItemLangTokenName + "_PICKUP", ItemPickupDesc);
            LanguageAPI.Add("ITEM_" + ItemLangTokenName + "_DESCRIPTION", ItemFullDescription);
            LanguageAPI.Add("ITEM_" + ItemLangTokenName + "_LORE", ItemLore);
        }

        private void RecalculateStats(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            var itemCount = GetCount(sender);
            if(itemCount > 0 && sender.healthComponent != null)
            {
                //float health = sender.healthComponent.health;
                float maxHealth = sender.maxHealth;
                if (maxHealth > 0)
                {
                    float percentage_health = sender.healthComponent.combinedHealthFraction;
                    if (percentage_health > ShiftHealthThreshold.Value)
                    {
                        args.damageMultAdd += PercentBonusDamage.Value * itemCount;
                    }
                    else
                    {
                        args.armorAdd += LowHealthArmor.Value * itemCount;
                    }
                }
            }
        }
    }
}
