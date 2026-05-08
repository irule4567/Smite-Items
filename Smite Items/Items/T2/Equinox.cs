using BepInEx.Configuration;
using R2API;
using RoR2;
using Smite_Items.Utils;
using System;
using UnityEngine;
using static Smite_Items.Main;

namespace Smite_Items.Items
{
    public class Equinox : ItemBase<Equinox>
    {
        public ConfigEntry<int> FrontHeal;
        public ConfigEntry<int> FrontHealPerStack;
        public ConfigEntry<float> BackDamageBonus;
        public ConfigEntry<float> BackDamageBonusPerStack;
        public override string ItemName => "Equinox";

        public override string ItemLangTokenName => "EQUINOX_ITEM";

        public override string ItemPickupDesc => "Attacks heal from the front and deal bonus damage from behind.";

        public override string ItemFullDescription => "";

        public override string ItemLore => "Item taken from Smite 1.";

        public override ItemTier Tier => ItemTier.Tier1;

        public override GameObject ItemModel => MainAssets.LoadAsset<GameObject>("DraconicScaleModel.prefab");

        public override Sprite ItemIcon => MainAssets.LoadAsset<Sprite>("Draconic Scale Icon.png");

        public override void Init(ConfigFile config)
        {
            CreateConfig(config);
            CreateLang();
            CreateItem();
            Hooks();
        }

        public override void CreateConfig(ConfigFile config)
        {
            FrontHeal = config.Bind<int>("Item: " + ItemName, "Health restored on front attack", 1, "How much health is restored on a front attack?");
            FrontHealPerStack = config.Bind<int>("Item: " + ItemName, "Health restored on front attack per stack", 1, "How much health is restored on a front attack per additional stack of the item?");
            BackDamageBonus = config.Bind<float>("Item: " + ItemName, "Back attack damage bonus", 0.3f, "What percentage of bonus damage does a back attack deal?");
            BackDamageBonusPerStack = config.Bind<float>("Item: " + ItemName, "Back attack damage bonus per stack", 0.3f, "What percentage of bonus damage does a back attack deal per additional stack of the item?");
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
            On.RoR2.HealthComponent.TakeDamage += HandleEquinox;
        }

        private void HandleEquinox(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo damageInfo)
        {
            if (damageInfo.attacker) // Check that attack was valid
            {
                CharacterBody attackerBody = damageInfo.attacker.GetComponent<CharacterBody>();
                if (attackerBody && attackerBody.inventory) // Check for item
                {
                    var stackCount = GetCount(attackerBody);
                    if (stackCount > 0)
                    {
                        if (damageInfo.procCoefficient != 0f)
                        {
                            if (RoR2.BackstabManager.IsBackstab(-(attackerBody.corePosition - damageInfo.position), self.body))
                            {
                                var EquinoxDamage = new DamageInfo { };
                                EquinoxDamage.damage = attackerBody.baseDamage * (BackDamageBonus.Value + (BackDamageBonusPerStack.Value * (stackCount - 1))); // Add bonus damage from backstab
                                EquinoxDamage.damageColorIndex = DamageColorIndex.Item;
                                EquinoxDamage.procCoefficient = 0f;
                                EquinoxDamage.damageType = DamageType.Generic;
                                EquinoxDamage.crit = false;
                                EquinoxDamage.position = damageInfo.position;
                                //damageInfo.damageType = DamageType.Generic;
                                EquinoxDamage.inflictor = damageInfo.inflictor;
                                EquinoxDamage.attacker = damageInfo.attacker;
                                self.TakeDamage(EquinoxDamage);
                            }
                            else
                            {
                                var healValue = FrontHeal.Value + (FrontHealPerStack.Value * (stackCount - 1));
                                attackerBody.healthComponent.Heal(healValue, default(ProcChainMask), true);
                            }
                        }
                    }
                }
            }
            orig(self, damageInfo);
        }
    }
}
