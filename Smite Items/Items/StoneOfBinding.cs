using BepInEx.Configuration;
using R2API;
using RoR2;
using Smite_Items.Utils;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using static Smite_Items.Main;

namespace Smite_Items.Items
{
    public class StoneOfBinding : ItemBase<StoneOfBinding>
    {
        public ConfigEntry<float> ArmorReduction;
        public ConfigEntry<float> ArmorReductionDuration;
        public ConfigEntry<float> ArmorReductionDurationPerStack;
        public ConfigEntry<int> MaxDebuffStacks;
        public override string ItemName => "Stone of Binding";

        public override string ItemLangTokenName => "STONE_OF_BINDING_ITEM";

        public override string ItemPickupDesc => "Applying debuffs reduces enemy armor.";

        public override string ItemFullDescription => $"After applying a new debuff to an enemy, reduce their armor by <style=cIsDamage>{ArmorReduction.Value}</style> for <style=cIsDamage>{ArmorReductionDuration.Value}</style> <style=cStack>(+{ArmorReductionDurationPerStack.Value} per stack)</style> seconds, up to a maximum of <style=cIsUtility>{MaxDebuffStacks.Value}</style> times.";

        public override string ItemLore => "Item taken from Smite 2";

        public override ItemTier Tier => ItemTier.Tier2;

        public override GameObject ItemModel => MainAssets.LoadAsset<GameObject>("ExampleItemPrefab.prefab");

        public override Sprite ItemIcon => MainAssets.LoadAsset<Sprite>("ExampleItemIcon.png");

        public static BuffDef bindingArmorReduction;

        public override void Init(ConfigFile config)
        {
            CreateConfig(config);
            CreateLang();
            CreateItem();
            CreateBuff();
            Hooks();
        }

        private void CreateBuff()
        {
            bindingArmorReduction = ScriptableObject.CreateInstance<BuffDef>();
            bindingArmorReduction.canStack = true;
            bindingArmorReduction.isDebuff = true;
            bindingArmorReduction.name = "bindingArmorReduction";
            bindingArmorReduction.isCooldown = false;
            bindingArmorReduction.iconSprite = MainAssets.LoadAsset<Sprite>("ExampleItemIcon.png");
            ContentAddition.AddBuffDef(bindingArmorReduction);
        }

        public override void CreateConfig(ConfigFile config)
        {
            ArmorReduction = config.Bind<float>("Item " + ItemName, "Armor Reduction", 10f, "How much armor is reduced by per debuff stack?");
            ArmorReductionDuration = config.Bind<float>("Item " + ItemName, "Armor Reduction Duration", 4f, "How long does the armor reduction debuff last?");
            ArmorReductionDurationPerStack = config.Bind<float>("Item " + ItemName, "Armor Reduction Duration per Stack", 4f, "How much longer does each stack of the item make the debuff?");
            MaxDebuffStacks = config.Bind<int>("Item " + ItemName, "Max Armor Reduction Stacks", 4, "What is the maximum amount of instances of the armor reduction debuff?");
        }

        public override ItemDisplayRuleDict CreateItemDisplayRules()
        {
            return new ItemDisplayRuleDict();
        }

        public override void Hooks()
        {
            On.RoR2.HealthComponent.TakeDamage += ApplyBinding;
            RecalculateStatsAPI.GetStatCoefficients += CalculateBindingDebuff;
        }

        private void CalculateBindingDebuff(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (sender && sender.inventory)
            {
                int buffStacks = sender.GetBuffCount(bindingArmorReduction);
                if (buffStacks > 0)
                {
                    args.armorAdd -= ArmorReduction.Value * buffStacks;
                }
            }
        }

        private void ApplyBinding(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo damageInfo)
        {
            orig(self, damageInfo);
            if (damageInfo.attacker && damageInfo.attacker.GetComponent<CharacterBody>())
            {
                CharacterBody attackerBody = damageInfo.attacker.GetComponent<CharacterBody>();
                if (attackerBody.inventory)
                {
                    var stackCount = GetCount(attackerBody);
                    if (stackCount > 0)
                    {
                        int debuffCount = 0;
                        int existingBindingDebuffCount = self.body.GetBuffCount(bindingArmorReduction);
                        BuffIndex[] debuffBuffIndices = BuffCatalog.debuffBuffIndices;
                        foreach (BuffIndex buffType in debuffBuffIndices)
                        {
                            // Check for debuffs as well as excluding stone of binding debuff and capping that debuff
                            if (self.body.HasBuff(buffType) && buffType != bindingArmorReduction.buffIndex)
                            {
                                debuffCount++;
                                //self.body.AddTimedBuff(bindingArmorReduction, ArmorReductionDuration.Value);
                                //ItemHelpers.RefreshTimedBuffs(self.body, bindingArmorReduction, ArmorReductionDuration.Value);
                            }
                        }
                        DotController dotController = DotController.FindDotController(self.gameObject);
                        if ((bool)dotController)
                        {
                            for (DotController.DotIndex dotIndex = DotController.DotIndex.Bleed; dotIndex < DotController.DotIndex.Count; dotIndex++)
                            {
                                if (dotController.HasDotActive(dotIndex))
                                {
                                    debuffCount++;
                                    //self.body.AddTimedBuff(bindingArmorReduction, ArmorReductionDuration.Value);
                                    //ItemHelpers.RefreshTimedBuffs(self.body, bindingArmorReduction, ArmorReductionDuration.Value);
                                }
                            }
                        }
                        if (debuffCount > 0)
                        {
                            if (debuffCount > existingBindingDebuffCount) // Only add more binding debuff if there aren't yet enough binding debuff for the existing debuffs
                            {
                                for(int i = 0; i < debuffCount - existingBindingDebuffCount; i++)
                                {
                                    if(self.body.GetBuffCount(bindingArmorReduction) < MaxDebuffStacks.Value)
                                    {
                                        self.body.AddTimedBuff(bindingArmorReduction, ArmorReductionDuration.Value + ArmorReductionDurationPerStack.Value*(stackCount-1));
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
