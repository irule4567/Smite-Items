using BepInEx.Configuration;
using R2API;
using RoR2;
using Smite_Items.Utils;
using System;
using UnityEngine;
using static Smite_Items.Main;

namespace Smite_Items.Items
{
    public class Musashis : ItemBase<Musashis>
    {
        public ConfigEntry<float> baseCritChance;
        public ConfigEntry<float> moveSpeedPerStack;
        public ConfigEntry<float> stackDuration;
        public ConfigEntry<int> baseStackCount;
        public ConfigEntry<int> stackCountPerAdditionalItem;
        public override string ItemName => "Musashis Dual Swords";

        public override string ItemLangTokenName => "MUSASHIS_DUAL_SWORDS";

        public override string ItemPickupDesc => "'Critical Strikes' increase movement speed. Stacks 3 times.";

        public override string ItemFullDescription => $"Gain <style=cIsDamage>{baseCritChance.Value}% critical chance</style>.<style=cIsDamage> Critical strikes</style> increase <style=cIsUtility>movement speed</style> by <style=cIsUtility>{moveSpeedPerStack.Value * 100}%</style>. " +
            $"Maximum cap of <style=cIsUtility>{baseStackCount.Value*moveSpeedPerStack.Value * 100}%</style> <style=cStack>(+{stackCountPerAdditionalItem.Value*moveSpeedPerStack.Value * 100}% per stack)</style> <style=cIsUtility>movement speed</style>.";

        public override string ItemLore => "Item taken from Smite 2.";

        public override ItemTier Tier => ItemTier.Tier2;

        public override GameObject ItemModel => MainAssets.LoadAsset<GameObject>("ExampleItemPrefab.prefab");

        public override Sprite ItemIcon => MainAssets.LoadAsset<Sprite>("ExampleItemIcon.png");

        public static BuffDef musashiMoveSpeed;

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
            musashiMoveSpeed = ScriptableObject.CreateInstance<BuffDef>();
            musashiMoveSpeed.canStack = true;
            musashiMoveSpeed.isDebuff = false;
            musashiMoveSpeed.name = "musashiMoveSpeed";
            musashiMoveSpeed.isCooldown = false;
            musashiMoveSpeed.iconSprite = MainAssets.LoadAsset<Sprite>("ExampleItemIcon.png");
            ContentAddition.AddBuffDef(musashiMoveSpeed);
        }

        public override void CreateConfig(ConfigFile config)
        {
            baseCritChance = config.Bind<float>("Item " + ItemName, "Base critical hit chance", 5, "How much critical chance is granted on the first item stack?");
            moveSpeedPerStack = config.Bind<float>("Item " + ItemName, "Percentage movement speed increase per critical strike", 0.12f, "How much movement speed is granted for each critical strike?");
            stackDuration = config.Bind<float>("Item " + ItemName, "Movement speed duration", 3, "How many seconds does the movement speed buff last?");
            baseStackCount = config.Bind<int>("Item " + ItemName, "Base maximum movement speed stacks", 3, "What is the maximum number of stacks of the movement speed buff the first stack of the item allows?");
            stackCountPerAdditionalItem = config.Bind<int>("Item " + ItemName, "Additional movement speed stacks allowed per additional item stack", 2, "By how many stacks does the maximum stack count of the movement speed buff increase per additional item?");
        }
        protected override void CreateLang()
        {
            LanguageAPI.Add("ITEM_" + ItemLangTokenName + "_NAME", "Musashi's Dual Swords");
            LanguageAPI.Add("ITEM_" + ItemLangTokenName + "_PICKUP", ItemPickupDesc);
            LanguageAPI.Add("ITEM_" + ItemLangTokenName + "_DESCRIPTION", ItemFullDescription);
            LanguageAPI.Add("ITEM_" + ItemLangTokenName + "_LORE", ItemLore);
        }

        public override ItemDisplayRuleDict CreateItemDisplayRules()
        {
            return new ItemDisplayRuleDict();
        }

        public override void Hooks()
        {
            On.RoR2.HealthComponent.TakeDamage += CheckAndApplyMusashis;
            RecalculateStatsAPI.GetStatCoefficients += RecalculateStats;
            RecalculateStatsAPI.GetStatCoefficients += AddBaseCrit;
        }

        private void AddBaseCrit(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (GetCount(sender) > 0)
            {
                args.critAdd += baseCritChance.Value;
            }
        }

        private void RecalculateStats(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            int buffCount = sender.GetBuffCount(musashiMoveSpeed);
            if (buffCount > 0) // Set movement speed based on stacks of buffs
            {
                args.moveSpeedMultAdd += buffCount * moveSpeedPerStack.Value;
            }
        }

        private void CheckAndApplyMusashis(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo damageInfo)
        {
            if(damageInfo.crit && damageInfo.attacker) // Check that attack was a crit
            {
                CharacterBody attackerBody = damageInfo.attacker.GetComponent<CharacterBody>();
                if(attackerBody && attackerBody.inventory) // Check for item
                {
                    var stackCount = GetCount(attackerBody);
                    if (stackCount > 0)
                    {
                        if(attackerBody.GetBuffCount(musashiMoveSpeed) < (baseStackCount.Value + (stackCount-1) * stackCountPerAdditionalItem.Value))
                        {
                            attackerBody.AddTimedBuff(musashiMoveSpeed, stackDuration.Value);
                            ItemHelpers.RefreshTimedBuffs(attackerBody, musashiMoveSpeed, stackDuration.Value);
                        }
                    }
                }
            }
            orig(self, damageInfo);
        }
    }
}
