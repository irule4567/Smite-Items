using BepInEx.Configuration;
using R2API;
using RoR2;
using Smite_Items.Utils;
using System;
using UnityEngine;
using static Smite_Items.Main;

namespace Smite_Items.Items
{
    public class Ichaival : ItemBase<Ichaival>
    {
        public ConfigEntry<float> DamageBuff;
        public ConfigEntry<float> DamageDebuff;
        public ConfigEntry<int> HardMaxDamageDebuffStacks;
        public ConfigEntry<int> MaxBuffStacks;
        public ConfigEntry<int> MaxDebuffStacks;
        public ConfigEntry<int> AdditionalMaxBuffStacksPerStack;
        public ConfigEntry<int> AdditionalMaxDebuffStacksPerStack;
        public ConfigEntry<int> BuffDuration;
        public ConfigEntry<int> DebuffDuration;

        public override string ItemName => "Ichaival";

        public override string ItemLangTokenName => "ICHAIVAL_ITEM";

        public override string ItemPickupDesc => "Steal damage from enemies";

        public override string ItemFullDescription => $"Dealing damage increases your damage by <style=cIsDamage>{DamageBuff.Value*100}%</style> up to <style=cIsUtility>{MaxBuffStacks.Value}</style> <style=cStack>(+{AdditionalMaxBuffStacksPerStack.Value} per stack)</style>, for <style=cIsUtility>{BuffDuration.Value}s</style>. " +
            $"The enemy hit has their damage reduced by <style=cIsDamage>{DamageDebuff.Value*100}%</style> up to <style=cIsUtility>{MaxDebuffStacks.Value}</style> <style=cStack>(+{AdditionalMaxDebuffStacksPerStack.Value} per stack)</style>, for <style=cIsUtility>{DebuffDuration.Value}s</style>. Maximum damage reduction through this method is <style=cIsDamage>{HardMaxDamageDebuffStacks.Value * DamageDebuff.Value * 100}%</style>.";

        public override string ItemLore => "Item taken from Smite 1.";

        public override ItemTier Tier => ItemTier.Tier1;

        public override GameObject ItemModel => MainAssets.LoadAsset<GameObject>("IchaivalModel.prefab");

        public override Sprite ItemIcon => MainAssets.LoadAsset<Sprite>("Ichaival Icon.png");

        public static BuffDef ichDamageBuff;
        public static BuffDef ichDamageDebuff;

        public override void Init(ConfigFile config)
        {
            CreateConfig(config);
            CreateLang();
            CreateItem();
            CreateBuff();
            Hooks();
        }

        public void CreateBuff()
        {
            ichDamageBuff = ScriptableObject.CreateInstance<BuffDef>();
            ichDamageBuff.canStack = true;
            ichDamageBuff.isDebuff = false;
            ichDamageBuff.name = "ichDamageBuff";
            ichDamageBuff.isCooldown = false;
            ichDamageBuff.iconSprite = MainAssets.LoadAsset<Sprite>("Chronos Pendant Icon.png");
            ContentAddition.AddBuffDef(ichDamageBuff);
            ichDamageDebuff = ScriptableObject.CreateInstance<BuffDef>();
            ichDamageDebuff.canStack = true;
            ichDamageDebuff.isDebuff = true;
            ichDamageDebuff.name = "ichDamageDebuff";
            ichDamageDebuff.isCooldown = false;
            ichDamageDebuff.iconSprite = MainAssets.LoadAsset<Sprite>("Chronos Pendant Icon.png");
            ContentAddition.AddBuffDef(ichDamageDebuff);
        }

        public override void CreateConfig(ConfigFile config)
        {
            DamageBuff = config.Bind<float>("Item " + ItemName, "Damage buff per buff stack", 0.025f, "By how much does each stack of the Ichaival damage buff increase damage?");
            DamageDebuff = config.Bind<float>("Item " + ItemName, "Damage debuff per debuff stack", 0.025f, "By how much does each stack of the Ichaival damage debuff decrease damage?");
            HardMaxDamageDebuffStacks = config.Bind<int>("Item " + ItemName, "Maximum number of damage debuff stacks from Ichaival that can be afflicted at once", 20, "What is the hard limit on the number of Ichaival damage debuff stacks that can be applied to a single character?");
            MaxBuffStacks = config.Bind<int>("Item " + ItemName, "Maximum number of buff stacks", 4, "What is the maximum number of buff stacks that one stack of Ichaival can apply to a single character?");
            MaxDebuffStacks = config.Bind<int>("Item " + ItemName, "Maximum number of debuff stacks", 4, "What is the maximum number of debuff stacks that one stack of Ichaival can apply to a single character?");
            AdditionalMaxBuffStacksPerStack = config.Bind<int>("Item " + ItemName, "Increased maximum buff stacks per item stack", 4, "How many additional buff stacks can be applied per additional stack of Ichaival?");
            AdditionalMaxDebuffStacksPerStack = config.Bind<int>("Item " + ItemName, "Increased maximum debuff stacks per item stack", 4, "How many additional debuff stacks can be applied per additional stack of Ichaival?");
            BuffDuration = config.Bind<int>("Item " + ItemName, "Duration in seconds of Ichaival buff stacks", 6, "How long (in seconds) does the Ichaival damage buff last?");
            DebuffDuration = config.Bind<int>("Item " + ItemName, "Duration in seconds of Ichaival debuff stacks", 6, "How long (in seconds) does the Ichaival damage debuff last?");
        }

        public override ItemDisplayRuleDict CreateItemDisplayRules()
        {
            var mpp = ItemModel.AddComponent<ModelPanelParameters>();
            mpp.focusPointTransform = ItemModel.transform.Find("Target");
            mpp.cameraPositionTransform = ItemModel.transform.Find("Source");
            mpp.minDistance = 4f;
            mpp.maxDistance = 8f;
            mpp.modelRotation = Quaternion.Euler(new Vector3(0, 90, 0));
            return new ItemDisplayRuleDict();
        }

        public override void Hooks()
        {
            On.RoR2.HealthComponent.TakeDamage += ApplyBuffAndDebuff;
            RecalculateStatsAPI.GetStatCoefficients += CalculateIchBuff;
            RecalculateStatsAPI.GetStatCoefficients += CalculateIchDebuff;
        }

        private void CalculateIchBuff(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            int buffCount = sender.GetBuffCount(ichDamageBuff);
            if (buffCount > 0) // Set bonus damage based on stacks of buffs
            {
                args.damageMultAdd += buffCount * DamageBuff.Value;
            }
        }

        private void CalculateIchDebuff(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            int buffCount = sender.GetBuffCount(ichDamageDebuff);
            if (buffCount > 0) // Set bonus damage based on stacks of buffs
            {
                args.damageMultAdd -= buffCount * DamageDebuff.Value;
            }
        }

        private void ApplyBuffAndDebuff(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo damageInfo)
        {
            
            if (damageInfo.attacker && damageInfo.attacker.GetComponent<CharacterBody>())
            {
                CharacterBody attackerBody = damageInfo.attacker.GetComponent<CharacterBody>();
                if (attackerBody.inventory)
                {
                    var stackCount = GetCount(attackerBody);
                    if (stackCount > 0)
                    {
                        if (attackerBody.GetBuffCount(ichDamageBuff) < (MaxBuffStacks.Value + ((stackCount - 1) * AdditionalMaxBuffStacksPerStack.Value)))
                        {
                            attackerBody.AddTimedBuff(ichDamageBuff, BuffDuration.Value);
                            ItemHelpers.RefreshTimedBuffs(attackerBody, ichDamageBuff, BuffDuration.Value);
                        }
                        if (self.body)
                        {
                            if (self.body.GetBuffCount(ichDamageDebuff) < (MaxDebuffStacks.Value + ((stackCount - 1) * AdditionalMaxDebuffStacksPerStack.Value)) && self.body.GetBuffCount(ichDamageDebuff) < HardMaxDamageDebuffStacks.Value)
                            {
                                self.body.AddTimedBuff(ichDamageDebuff, DebuffDuration.Value);
                                ItemHelpers.RefreshTimedBuffs(self.body, ichDamageDebuff, DebuffDuration.Value);
                            }
                        }
                    }
                }
            }
            orig(self, damageInfo);
        }
    }
}
