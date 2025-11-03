using BepInEx.Configuration;
using R2API;
using RoR2;
using Smite_Items.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using static Smite_Items.Main;

namespace Smite_Items.Items
{
    public class DraconicScale : ItemBase<DraconicScale>
    {
        public ConfigEntry<float> ScaleArmorFlat;
        public ConfigEntry<float> ScaleArmorPercent;
        public ConfigEntry<float> BuffDuration;
        public ConfigEntry<int> MaxStacks;
        public override string ItemName => "Draconic Scale";

        public override string ItemLangTokenName => "SCALE_ITEM";

        public override string ItemPickupDesc => "Gain temporary armor after taking damage";

        public override string ItemFullDescription => $"Each time you take damage, gain a buff that grants <style=cIsHealing>{ScaleArmorFlat.Value}</style> armor and increases armor by <style=cIsHealing>{ScaleArmorPercent.Value * 100}%</style> for <style=cIsUtility>{BuffDuration.Value}</style> seconds up to a maximum of <style=cIsUtility>{MaxStacks.Value}</style> <style=cStack>(+{MaxStacks.Value} per stack)</style> <style=cIsUtility>times</style>. This buff decays by 1 stack at a time.";

        public override string ItemLore => "Item taken from Smite 2.";

        public override ItemTier Tier => ItemTier.Tier2;

        public override GameObject ItemModel => MainAssets.LoadAsset<GameObject>("DraconicScaleModel.prefab");

        public override Sprite ItemIcon => MainAssets.LoadAsset<Sprite>("Draconic Scale Icon.png");

        public static BuffDef scaleArmorBuff;

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
            scaleArmorBuff = ScriptableObject.CreateInstance<BuffDef>();
            scaleArmorBuff.canStack = true;
            scaleArmorBuff.isDebuff = false;
            scaleArmorBuff.name = "scaleArmorBuff";
            scaleArmorBuff.isCooldown = false;
            scaleArmorBuff.iconSprite = MainAssets.LoadAsset<Sprite>("ExampleItemIcon.png");
            ContentAddition.AddBuffDef(scaleArmorBuff);
        }

        public override void CreateConfig(ConfigFile config)
        {
            ScaleArmorFlat = config.Bind<float>("Item " + ItemName, "Flat armor per scale stack", 2f, "How much flat armor is given per stack of Draconic Scale buff?");
            ScaleArmorPercent = config.Bind<float>("Item " + ItemName, "Percent armor boost per scale stack", 0.03f, "By what percentage does each stack of Draconic Scale buff increase armor?");
            BuffDuration = config.Bind<float>("Item " + ItemName, "Duration of buff stacks", 1.5f, "How long does each stack of the Draconic Scale buff last?");
            MaxStacks = config.Bind<int>("Item " + ItemName, "Maximum scale buff stacks", 7, "What is the maximum amount of buff stacks Draconic Scale can apply?");
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
            On.RoR2.CharacterBody.OnInventoryChanged += CharacterBody_OnInventoryChanged;
            On.RoR2.HealthComponent.TakeDamage += AddArmor;
            RecalculateStatsAPI.GetStatCoefficients += CalculateScaleArmor;
        }

        private void CharacterBody_OnInventoryChanged(On.RoR2.CharacterBody.orig_OnInventoryChanged orig, CharacterBody self)
        {
            orig(self);

            self.AddItemBehavior<DraconicScaleBehavior>(GetCount(self));
        }

        private void CalculateScaleArmor(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if(sender && sender.inventory)
            {
                int buffStacks = sender.GetBuffCount(scaleArmorBuff);
                int stackCount = sender.inventory.GetItemCount(ItemBase<DraconicScale>.instance.ItemDef);
                if (buffStacks > 0)
                {
                    args.armorAdd += ScaleArmorFlat.Value * buffStacks;
                    args.armorTotalMult += ScaleArmorPercent.Value * buffStacks;
                }
            }
        }

        private void AddArmor(On.RoR2.HealthComponent.orig_TakeDamage orig, RoR2.HealthComponent self, RoR2.DamageInfo damageInfo)
        {
            orig(self, damageInfo);
            if(self && self.body && self.body.inventory)
            {
                var stackCount = GetCount(self.body);
                if(stackCount > 0)
                {
                    ItemHelpers.RefreshTimedBuffs(self.body, scaleArmorBuff, BuffDuration.Value);
                    if (self.body.GetBuffCount(scaleArmorBuff) < MaxStacks.Value*GetCount(self.body))
                    {
                        self.body.AddBuff(scaleArmorBuff);
                        //self.body.AddTimedBuff(scaleArmorBuff, BuffDuration.Value, MaxStacks.Value); 
                        // Possible idea for making stacks deplete one at a time by having each stack increase in duration for each other stack
                        // Wouldn't work if stacks partially deplete before refreshing
                        //* (self.body.GetBuffCount(scaleArmorBuff) + 1));
                    }
                }
            }
        }
        public class DraconicScaleBehavior : CharacterBody.ItemBehavior
        {
            private float StackCooldownTimer;
            /*void Start()
            {
                body = GetComponent<CharacterBody>();
                StackCooldownTimer = 0f;
            }*/

            private void OnDisable()
            {
                while (body && body.HasBuff(DraconicScale.scaleArmorBuff))
                {
                    body.RemoveBuff(DraconicScale.scaleArmorBuff);
                }
            }

            void FixedUpdate()
            {
                if (!body)
                    return;

                if (!NetworkServer.active)
                    return;

                if(body.HasBuff(DraconicScale.scaleArmorBuff))
                {
                    StackCooldownTimer += Time.deltaTime;
                    if(StackCooldownTimer >= DraconicScale.instance.BuffDuration.Value)
                    {
                        body.RemoveBuff(scaleArmorBuff);
                        StackCooldownTimer = 0f;
                    }
                }
            }
        }
    }
}
