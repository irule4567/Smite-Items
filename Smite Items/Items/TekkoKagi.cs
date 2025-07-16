using BepInEx.Configuration;
using R2API;
using RoR2;
using Smite_Items.Utils;
using System;
using UnityEngine;
using static Smite_Items.Main;

namespace Smite_Items.Items
{
    public class TekkoKagi : ItemBase<TekkoKagi>
    {
        public ConfigEntry<int> MaxStacks;
        public ConfigEntry<float> MoveSpeedPerStack;
        public ConfigEntry<int> StackDuration;
        public override string ItemName => "Tekko-Kagi";

        public override string ItemLangTokenName => "TEKKOKAGI_ITEM";

        public override string ItemPickupDesc => "Gain movement speed on skill use.";

        public override string ItemFullDescription => $"After using a non-primary skill, gain a stack of <style=cIsUtility>{MoveSpeedPerStack.Value * 100}%</style> movement speed up to <style=cIsUtility>{MoveSpeedPerStack.Value}</style> <style=cStack>(+{MoveSpeedPerStack.Value} per stack)</style> that lasts <style=cIsUtility>{StackDuration.Value}</style> seconds.";

        public override string ItemLore => "Item taken from Smite 2.";

        public override ItemTier Tier => ItemTier.Tier1;

        public override GameObject ItemModel => MainAssets.LoadAsset<GameObject>("ExampleItemPrefab.prefab");

        public override Sprite ItemIcon => MainAssets.LoadAsset<Sprite>("ExampleItemIcon.png");

        public static BuffDef tekkoMoveSpeed;

        private SkillLocator skillLocator;

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
            tekkoMoveSpeed = ScriptableObject.CreateInstance<BuffDef>();
            tekkoMoveSpeed.canStack = true;
            tekkoMoveSpeed.isDebuff = false;
            tekkoMoveSpeed.name = "tekkoMoveSpeed";
            tekkoMoveSpeed.isCooldown = false;
            tekkoMoveSpeed.iconSprite = MainAssets.LoadAsset<Sprite>("ExampleItemIcon.png");
            ContentAddition.AddBuffDef(tekkoMoveSpeed);
        }

        public override void CreateConfig(ConfigFile config)
        {
            MaxStacks = config.Bind<int>("Item " + ItemName, "Maximum Stacks", 3, "What is the maximum number of stacks of movement speed?");
            MoveSpeedPerStack = config.Bind<float>("Item " + ItemName, "Percent movement speed increase per stack", 0.08f, "How much movement speed does the character get per stack of the buff?");
            StackDuration = config.Bind<int>("Item " + ItemName, "Duration of movement speed stacks in seconds", 10, "How long do movement speed stacks last before reseting?");

        }

        public override ItemDisplayRuleDict CreateItemDisplayRules()
        {
            return new ItemDisplayRuleDict();
        }

        public override void Hooks()
        {
            On.RoR2.CharacterBody.OnSkillActivated += AddMoveStack;
            RecalculateStatsAPI.GetStatCoefficients += RecalculateStats;
            //On.RoR2.CharacterBody.RecalculateStats += RecalculateStats;
        }

        private void RecalculateStats(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            int buffCount = sender.GetBuffCount(tekkoMoveSpeed);
            if (buffCount > 0) // Set movement speed based on stacks of buffs
            {
                args.moveSpeedMultAdd += buffCount * MoveSpeedPerStack.Value;
            }
        }

        /*private void RecalculateStats(On.RoR2.CharacterBody.orig_RecalculateStats orig, CharacterBody self)
        {
            orig(self);

            int buffCount = self.GetBuffCount(tekkoMoveSpeed);
            if (buffCount > 0) // Set movement speed based on stacks of buffs
            {
                self.baseMoveSpeedAdd = buffCount * MoveSpeedPerStack.Value;
            }
        }*/

        private void AddMoveStack(On.RoR2.CharacterBody.orig_OnSkillActivated orig, RoR2.CharacterBody self, RoR2.GenericSkill skill)
        {
            orig(self, skill);
            var inventoryCount = GetCount(self);
            if (inventoryCount > 0)
            {
                bool isPrimary = (self.skillLocator.primary.skillDef == skill.skillDef);
                //Debug.Log("Is Primary skill: " + isPrimary);
                // Add movement speed buff if skill wasn't primary and buff count is under max
                if (!isPrimary && self.GetBuffCount(tekkoMoveSpeed) < MaxStacks.Value * inventoryCount)
                {
                    self.AddTimedBuff(tekkoMoveSpeed, StackDuration.Value);
                    ItemHelpers.RefreshTimedBuffs(self, tekkoMoveSpeed, StackDuration.Value);
                }
            }
        }
    }
}
