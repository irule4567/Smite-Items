using BepInEx.Configuration;
using R2API;
using RoR2;
using System;
using System.Collections.Generic;
using UnityEngine;
using static Smite_Items.Main;

namespace Smite_Items.Items
{
    public class SacrificialShroud : ItemBase<SacrificialShroud>
    {
        public ConfigEntry<float> SkillBonusDamage;
        public ConfigEntry<float> SkillBonusDamagePerStack;
        public ConfigEntry<float> PercentHealthCostPerSecond;
        public ConfigEntry<float> PercentHealthCostPerSecondPerStack;


        public override string ItemName => "Sacrificial Shroud";

        public override string ItemLangTokenName => "SACRIFICIAL_SHROUD_ITEM";

        public override string ItemPickupDesc => $"Skills deal bonus damage...<style=cIsHealth> BUT cost health to use.</style>";

        public override string ItemFullDescription => $"All <style=cIsUtility>non-primary skills</style> deal <style=cIsDamage>{SkillBonusDamage.Value*100}%</style> <style=cStack>(+{SkillBonusDamagePerStack.Value*100}% per stack)</style> <style=cIsDamage>bonus damage</style>. Activating a <style=cIsUtility>non-primary skill</style> deals <style=cIsDamage>{PercentHealthCostPerSecond.Value*100}%</style> <style=cStack>(+{PercentHealthCostPerSecondPerStack.Value*100}% per stack)</style> of your max health per second of the skill's base cooldown to you.";

        public override string ItemLore => "Item taken from Smite 1.";

        public override ItemTier Tier => ItemTier.Lunar;

        public override ItemTag[] ItemTags => new ItemTag[]
        {
            ItemTag.Damage
        };

        public override GameObject ItemModel => MainAssets.LoadAsset<GameObject>("SacrificalShroudModel.prefab");

        public override Sprite ItemIcon => MainAssets.LoadAsset<Sprite>("Sacrificial Shroud Icon.png");
        private Dictionary<CharacterBody, float> lastPrimaryUseTime = new Dictionary<CharacterBody, float>();
        private const float PRIMARY_SKILL_WINDOW = 0.5f;

        public override void Init(ConfigFile config)
        {
            CreateConfig(config);
            CreateLang();
            CreateItem();
            Hooks();
        }

        public override void CreateConfig(ConfigFile config)
        {
            SkillBonusDamage = config.Bind<float>("Item " + ItemName, "Percent skill bonus damage", 0.5f, "By what percent is skill damage increased by?");
            SkillBonusDamagePerStack = config.Bind<float>("Item " + ItemName, "Percent skill bonus damage per item stack", 0.5f, "By what percent is skill damage increased by per additional item stack?");
            PercentHealthCostPerSecond = config.Bind<float>("Item " + ItemName, "Percent maximum health cost per skill cooldown second", 0.01f, "What percentage of maximum health is taken as damage per second of cooldown of a skill?");
            PercentHealthCostPerSecondPerStack = config.Bind<float>("Item " + ItemName, "Additional percent maximum health cost per skill cooldown second per item stack", 0.01f, "What percentage of maximum health is taken as damage per second of cooldown of a skill per additional stack of the item?");
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
            On.RoR2.CharacterBody.OnSkillActivated += ApplySacrificeSelfDamage;
            On.RoR2.HealthComponent.TakeDamage += ApplySacrificeBonusDamage;
            On.RoR2.CharacterBody.OnSkillActivated += TrackPrimarySkillUse;
        }
        private void TrackPrimarySkillUse(On.RoR2.CharacterBody.orig_OnSkillActivated orig, CharacterBody self, GenericSkill skill)
        {
            orig(self, skill);
            bool isPrimary = (self.skillLocator.primary.skillDef == skill.skillDef);
            if (isPrimary)
            {
                lastPrimaryUseTime[self] = Time.time;
            }
        }

        private void ApplySacrificeBonusDamage(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo damageInfo)
        {
            if (damageInfo.attacker)
            {
                CharacterBody attackerBody = damageInfo.attacker.GetComponent<CharacterBody>();
                if (attackerBody.inventory)
                {
                    var stackCount = GetCount(attackerBody);

                    if (stackCount > 0)
                    {
                        if (damageInfo.attacker && damageInfo.attacker.GetComponent<CharacterBody>())
                        {
                            bool isPrimaryAttack = false;
                            if (lastPrimaryUseTime.ContainsKey(attackerBody))
                            {
                                isPrimaryAttack = (Time.time - lastPrimaryUseTime[attackerBody]) < PRIMARY_SKILL_WINDOW;
                            }
                            if (damageInfo.damageType.IsDamageSourceSkillBased && !isPrimaryAttack)
                            {
                                // If damage is from a skill, multiply it accordingly
                                damageInfo.damage *= 1 + (SkillBonusDamage.Value + SkillBonusDamagePerStack.Value * (stackCount - 1));
                            }
                        }
                    }
                }
            }

            orig(self, damageInfo);
        }

        private void ApplySacrificeSelfDamage(On.RoR2.CharacterBody.orig_OnSkillActivated orig, CharacterBody self, GenericSkill skill)
        {
            orig(self, skill);
            // If skill use wasn't primary fire, apply self damage
            var inventoryCount = GetCount(self);
            if (inventoryCount > 0)
            {
                bool isPrimary = (self.skillLocator.primary.skillDef == skill.skillDef);
                if (!isPrimary && skill.baseRechargeInterval > 0f) // Check that skill has a cooldown and isn't primary
                {
                    // Deal damage to player equal to a percentage of maximum health, scaling based on both number of stacks of the item and skill cooldown
                    // Doubled to account for friendly fire damage being naturally halved
                    float hurt = self.maxHealth * 2 * (PercentHealthCostPerSecond.Value + PercentHealthCostPerSecondPerStack.Value*(inventoryCount-1)) * skill.baseRechargeInterval;
                    var SacDamage = new DamageInfo { };
                    SacDamage.damage = hurt;
                    SacDamage.damageColorIndex = DamageColorIndex.Item;
                    SacDamage.procCoefficient = 0f;
                    SacDamage.damageType = DamageType.NonLethal;
                    SacDamage.crit = false;
                    SacDamage.position = self.corePosition;
                    SacDamage.inflictor = self.gameObject;
                    SacDamage.attacker = self.gameObject;
                    self.healthComponent.TakeDamage(SacDamage);
                }
            }
        }
    }
}
