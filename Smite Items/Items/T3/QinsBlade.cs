using BepInEx.Configuration;
using R2API;
using RoR2;
using System;
using UnityEngine;
using UnityEngine.Networking;
using static Smite_Items.Main;

namespace Smite_Items.Items
{
    public class QinsBlade : ItemBase<QinsBlade>
    {
        public ConfigEntry<float> PercentHPBonusDamage;
        public ConfigEntry<float> PercentHPBonusDamagePerStack;
        public override string ItemName => "Qins Blade";

        public override string ItemLangTokenName => "QINS_BLADE_ITEM";

        public override string ItemPickupDesc => "Deal bonus damage based on enemy maximum health.";

        public override string ItemFullDescription => $"All attacks deal bonus damage equal to <style=cIsDamage>{PercentHPBonusDamage.Value*100}%</style> <style=cStack>(+{PercentHPBonusDamagePerStack.Value*100}% per stack)</style> <style=cIsDamage>of the enemy's Max Health</style>.";

        public override string ItemLore => "Item taken from Smite 2, based on a previous version of the item.";

        public override ItemTier Tier => ItemTier.Tier3;
        public override ItemTag[] ItemTags => new ItemTag[]
        {
            ItemTag.Damage
        };
        public override GameObject ItemModel => MainAssets.LoadAsset<GameObject>("QinsBladeModel.prefab");

        public override Sprite ItemIcon => MainAssets.LoadAsset<Sprite>("Qins Blade Icon.png");

        public override void Init(ConfigFile config)
        {
            CreateConfig(config);
            CreateLang();
            CreateItem();
            Hooks();
        }

        public override void CreateConfig(ConfigFile config)
        {
            PercentHPBonusDamage = config.Bind<float>("Item: " + ItemName, "Percent Max Health Bonus Damage", 0.01f, "What percentage of the targets maximum health is taken as bonus damage?");
            PercentHPBonusDamagePerStack = config.Bind<float>("Item: " + ItemName, "Percent Max Health Bonus Damage Added Per Additional Stack", 0.01f, "How much does each additional stack of the item increase the maximum health bonus damage by?");
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

        protected override void CreateLang()
        {
            LanguageAPI.Add("ITEM_" + ItemLangTokenName + "_NAME", "Qin's Blade");
            LanguageAPI.Add("ITEM_" + ItemLangTokenName + "_PICKUP", ItemPickupDesc);
            LanguageAPI.Add("ITEM_" + ItemLangTokenName + "_DESCRIPTION", ItemFullDescription);
            LanguageAPI.Add("ITEM_" + ItemLangTokenName + "_LORE", ItemLore);
        }
        public override void Hooks()
        {
            On.RoR2.HealthComponent.TakeDamage += QinsBonusDamage;
        }

        private void QinsBonusDamage(On.RoR2.HealthComponent.orig_TakeDamage orig, RoR2.HealthComponent self, RoR2.DamageInfo damageInfo)
        {
            if (damageInfo.attacker)
            {
                if (damageInfo.attacker.GetComponent<CharacterBody>())
                {
                    CharacterBody attackerBody = damageInfo.attacker.GetComponent<CharacterBody>();
                    if (attackerBody.inventory)
                    {
                        var stackCount = GetCount(attackerBody);

                        if (stackCount > 0)
                        {
                            if (damageInfo.procCoefficient != 0f)
                            {
                                if (damageInfo.attacker && damageInfo.attacker.GetComponent<CharacterBody>() && NetworkServer.active && self.body)
                                {
                                    var QinsDamage = new RoR2.DamageInfo { };
                                    QinsDamage.damage = (PercentHPBonusDamage.Value + ((stackCount - 1) * PercentHPBonusDamagePerStack.Value)) * self.body.maxHealth * damageInfo.procCoefficient;
                                    QinsDamage.damageColorIndex = DamageColorIndex.Item;
                                    QinsDamage.procCoefficient = 0f;
                                    QinsDamage.damageType = DamageType.Generic;
                                    QinsDamage.crit = false;
                                    QinsDamage.inflictor = damageInfo.inflictor;
                                    QinsDamage.attacker = damageInfo.attacker;
                                    QinsDamage.position = damageInfo.position;
                                    self.TakeDamage(QinsDamage);
                                }
                            }
                        }
                    }
                }
            }

            orig(self, damageInfo);
        }
    }
}
