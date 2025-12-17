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
    public class BreastplateValor : ItemBase<BreastplateValor>
    {
        public ConfigEntry<float> CooldownsReducedPerActivation;
        public ConfigEntry<float> PercentHealthLostToTrigger;
        public override string ItemName => "Breastplate of Valor";

        public override string ItemLangTokenName => "VALOR_BREASTPLATE_ITEM";

        public override string ItemPickupDesc => "Reduce skill cooldowns after losing enough health";

        public override string ItemFullDescription => $"For every <style=cIsHealth>{PercentHealthLostToTrigger.Value*100}%</style> of your max health you lose your <style=cIsUtility>skill cooldowns<style=cIsUtility> are <style=cIsUtility>reduced</style> by <style=cIsUtility>{CooldownsReducedPerActivation.Value}</style> <style=cStack>(+{CooldownsReducedPerActivation.Value} per stack)</style> seconds.";

        public override string ItemLore => "Item taken from Smite 2.";

        public override ItemTier Tier => ItemTier.Tier2;

        public override ItemTag[] ItemTags => new ItemTag[]
        {
            ItemTag.Utility
        };
        public override GameObject ItemModel => MainAssets.LoadAsset<GameObject>("BreastplateModel.prefab");

        public override Sprite ItemIcon => MainAssets.LoadAsset<Sprite>("Breastplate Icon.png");

        public static BuffDef breastplateHealthTracker; // Tracks how much percent health has been lost. One stack per 0.1% health lost, procs at 250 stacks and resets
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
            breastplateHealthTracker = ScriptableObject.CreateInstance<BuffDef>();
            breastplateHealthTracker.canStack = true;
            breastplateHealthTracker.isDebuff = false;
            breastplateHealthTracker.name = "breastplateHealthTracker";
            breastplateHealthTracker.isCooldown = false;
            breastplateHealthTracker.isHidden = false;
            breastplateHealthTracker.iconSprite = MainAssets.LoadAsset<Sprite>("Breastplate Icon.png");
            ContentAddition.AddBuffDef(breastplateHealthTracker);
        }

        public override void CreateConfig(ConfigFile config)
        {
            CooldownsReducedPerActivation = config.Bind<float>("Item " + ItemName, "Ability cooldowns reduced per activation", 1, "How many seconds removed from each ability cooldown per item proc?");
            PercentHealthLostToTrigger = config.Bind<float>("Item " + ItemName, "Percent health lost to trigger item", 0.25f, "How much of a characters max health needs to be lost to trigger the item?");
        }

        public override ItemDisplayRuleDict CreateItemDisplayRules()
        {
            var mpp = ItemModel.AddComponent<ModelPanelParameters>();
            GameObject focusPoint = new GameObject("FocusPoint");
            focusPoint.transform.SetParent(ItemModel.transform);
            focusPoint.transform.localPosition = Vector3.zero; // Center of model
            focusPoint.transform.localRotation = Quaternion.identity;

            // Create camera position transform (defines viewing angle)
            GameObject cameraPosition = new GameObject("CameraPosition");
            cameraPosition.transform.SetParent(ItemModel.transform);
            cameraPosition.transform.localPosition = new Vector3(1f, 0f, 0f); // Offset from focus point
            cameraPosition.transform.localRotation = Quaternion.identity;
            mpp.focusPointTransform = focusPoint.transform; //ItemModel.transform.Find("Target");
            mpp.cameraPositionTransform = cameraPosition.transform; //ItemModel.transform.Find("Source");
            mpp.minDistance = 100f;
            mpp.maxDistance = 200f;
            mpp.modelRotation = Quaternion.Euler(new Vector3(0, 90, 0));
            mpp.modelPositionOffset = new Vector3(0, 50, 0);
            return new ItemDisplayRuleDict();
        }

        public override void Hooks()
        {
            On.RoR2.HealthComponent.TakeDamage += CalculateHealthLost;
        }

        private void CalculateHealthLost(On.RoR2.HealthComponent.orig_TakeDamage orig, RoR2.HealthComponent self, RoR2.DamageInfo damageInfo)
        {
            orig(self, damageInfo);
            if (!NetworkServer.active) return;
            if(self && self.alive && self.body && self.body.inventory) // Basic sanity checks
            {
                var stackCount = GetCount(self.body);
                if (!self.body.skillLocator) return;
                    if (stackCount > 0)
                {
                    float percentDamageTaken = damageInfo.damage / self.body.maxHealth;
                    //int buffsToAdd = (int)(percentDamageTaken * 1000);
                    int buffsToAdd = Mathf.RoundToInt(percentDamageTaken * 1000);
                    if(buffsToAdd > 0)
                    {
                        int buffStacks = self.body.GetBuffCount(breastplateHealthTracker);
                        int newBuffStacks = buffsToAdd + buffStacks;
                        int procCount = Mathf.FloorToInt(newBuffStacks / (PercentHealthLostToTrigger.Value * 1000));
                        if (procCount > 0)
                        {
                            self.body.skillLocator.DeductCooldownFromAllSkillsServer(CooldownsReducedPerActivation.Value * stackCount * procCount);
                            newBuffStacks = newBuffStacks % 250;
                        }
                        /*while (newBuffStacks > 250)
                        {
                            newBuffStacks = newBuffStacks - 250;
                            if (self.body.skillLocator)
                            {
                                self.body.skillLocator.DeductCooldownFromAllSkillsServer(CooldownsReducedPerActivation.Value * stackCount);
                            }
                        }*/
                        self.body.SetBuffCount(breastplateHealthTracker.buffIndex, newBuffStacks);
                    }
                }
            }
        }
    }
}
