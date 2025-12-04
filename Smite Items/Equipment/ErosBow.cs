using BepInEx.Configuration;
using R2API;
using RoR2;
using Smite_Items.Items;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using static RoR2.DotController;
using static RoR2.EquipmentSlot;
using static Smite_Items.Main;

//Small note: When switching equipment, eros bow is totally disabled and resets its target. Treat as intended.

namespace Smite_Items.Equipment
{
    internal class ErosBow : EquipmentBase<ErosBow>
    {
        public ConfigEntry<float> PercentMaxHpHeal;
        public override string EquipmentName => "Eros Bow";

        public override string EquipmentLangTokenName => "EROS_BOW_EQUIP";

        public override string EquipmentPickupDesc => "Heal on hit. Activate to direct healing to ally.";

        public override string EquipmentFullDescription => $"Whenever you deal damage, heals a friendly target for <style=cIsHealing>{PercentMaxHpHeal.Value*100}% of their maximum health</style>. Activating the equipment assigns a new target, or yourself if there are no targets available.";

        public override string EquipmentLore => "Equipment taken from Smite 2.";

        public override GameObject EquipmentModel => MainAssets.LoadAsset<GameObject>("ErosBowModel.prefab");

        public override Sprite EquipmentIcon => MainAssets.LoadAsset<Sprite>("Eros Bow Icon.png");

        public override float Cooldown => 15;

        private Dictionary<CharacterBody, CharacterBody> erosPairs = new Dictionary<CharacterBody, CharacterBody>();
        public override void Init(ConfigFile config)
        {
            CreateConfig(config);
            CreateLang();
            CreateTargetingIndicator();
            CreateEquipment();
            Hooks();
        }

        protected override void CreateConfig(ConfigFile config)
        {
            PercentMaxHpHeal = config.Bind<float>("Equipment " + EquipmentName, "Percent Max Health Heal", 0.01f, "What percentage of max health is healed to the target when the effect activates?");
        }

        protected override void CreateLang()
        {
            LanguageAPI.Add("EQUIPMENT_" + EquipmentLangTokenName + "_NAME", "Eros' Bow");
            LanguageAPI.Add("EQUIPMENT_" + EquipmentLangTokenName + "_PICKUP", EquipmentPickupDesc);
            LanguageAPI.Add("EQUIPMENT_" + EquipmentLangTokenName + "_DESCRIPTION", EquipmentFullDescription);
            LanguageAPI.Add("EQUIPMENT_" + EquipmentLangTokenName + "_LORE", EquipmentLore);
        }

        public override void Hooks()
        {
            On.RoR2.CharacterBody.OnInventoryChanged += GiveErosEffect;
            //On.RoR2.Inventory.SetEquipmentIndex_EquipmentIndex += HandleInitialErosEffect;
            //On.RoR2.EquipmentSlot.Start += HandleInitialErosEffect;
            On.RoR2.GlobalEventManager.OnHitEnemy += GiveHealth;
            //On.RoR2.EquipmentSlot.FixedUpdate += ErosUpdate;
            On.RoR2.EquipmentSlot.UpdateTargets += ErosTarget;

            On.RoR2.CharacterBody.OnDeathStart += CleanupOnDeath;
        }

        /*private void HandleInitialErosEffect(On.RoR2.Inventory.orig_SetEquipmentIndex_EquipmentIndex orig, Inventory self, EquipmentIndex newEquipmentIndex)
        {
            orig(self, newEquipmentIndex);
            if (!NetworkServer.active) return;
            Debug.Log(self.currentEquipmentIndex);
            Debug.Log(this.EquipmentDef.equipmentIndex);
            if (self.currentEquipmentIndex == this.EquipmentDef.equipmentIndex)
            {
                //itemActive = true;
                if (!erosPairs.ContainsKey(self.GetComponent<CharacterBody>()))
                {
                    erosPairs[self.GetComponent<CharacterBody>()] = self.GetComponent<CharacterBody>();
                }
            }
            else // Cleanup
            {
                //itemActive = false;
                erosPairs.Remove(self.GetComponent<CharacterBody>());
            }
        }

        private void HandleInitialErosEffect(On.RoR2.Inventory.orig_SetActiveEquipmentSlot orig, Inventory self, byte slotIndex)
        {
            orig(self, slotIndex);
            if (!NetworkServer.active) return;
            if (self.currentEquipmentIndex == this.EquipmentDef.equipmentIndex)
            {
                //itemActive = true;
                if (!erosPairs.ContainsKey(self.GetComponent<CharacterBody>()))
                {
                    erosPairs[self.GetComponent<CharacterBody>()] = self.GetComponent<CharacterBody>();
                }
            }
            else // Cleanup
            {
                //itemActive = false;
                erosPairs.Remove(self.GetComponent<CharacterBody>());
            }
        }

        private void HandleInitialErosEffect(On.RoR2.EquipmentSlot.orig_Start orig, EquipmentSlot self)
        {
            orig(self);
            if (!NetworkServer.active) return;
            if (self.equipmentIndex == this.EquipmentDef.equipmentIndex)
            {
                //itemActive = true;
                if (!erosPairs.ContainsKey(self.characterBody))
                {
                    erosPairs[self.characterBody] = self.characterBody;
                }
            }
            else // Cleanup
            {
                //itemActive = false;
                erosPairs.Remove(self.characterBody);
            }
        }*/

        private void CleanupOnDeath(On.RoR2.CharacterBody.orig_OnDeathStart orig, CharacterBody self)
        {
            orig(self);
            //CharacterBody originBody = erosPairs.FirstOrDefault(x => x.Value == self).Key;
            var affectedKeys = erosPairs.Where(kvp => kvp.Value == self).Select(kvp => kvp.Key).ToList();
            foreach (var key in affectedKeys)
            {
                erosPairs[key] = key;
            }
            /*while (originBody != null) // Continually searches for instances of the dead character being the recipient of an eros bow.
            {
                erosPairs[originBody] = originBody; // Reset recipient to self when recipient dies
                originBody = erosPairs.FirstOrDefault(x => x.Value == self).Key;
            }*/
            erosPairs.Remove(self);
            
        }

        private void ErosTarget(On.RoR2.EquipmentSlot.orig_UpdateTargets orig, EquipmentSlot self, EquipmentIndex targetingEquipmentIndex, bool userShouldAnticipateTarget)
        {
            if (targetingEquipmentIndex != EquipmentDef.equipmentIndex)
            {
                orig(self, targetingEquipmentIndex, userShouldAnticipateTarget);
                return;
            }
            if (self.equipmentIndex == this.EquipmentDef.equipmentIndex)
            {
                
                //self.targetIndicator.visualizerPrefab = TargetingIndicatorPrefabBase;
                self.ConfigureTargetFinderForFriendlies();
                HurtBox hurtBox = self.targetFinder.GetResults().FirstOrDefault();
                if (hurtBox != null && self.stock > 0)
                {
                    //self.targetIndicator.visualizerPrefab = LegacyResourcesAPI.Load<GameObject>("Prefabs/WoodSpriteIndicator");
                    self.targetIndicator.visualizerPrefab = TargetingIndicatorPrefabBase;
                    self.targetIndicator.targetTransform = hurtBox.transform;
                    self.targetIndicator.active = true;
                    self.currentTarget = new UserTargetInfo(hurtBox);
                }
                else
                {
                    self.currentTarget = new UserTargetInfo(self.characterBody.mainHurtBox);
                    self.targetIndicator.active = false;
                }
            }
        }

        /*private void ErosUpdate(On.RoR2.EquipmentSlot.orig_FixedUpdate orig, EquipmentSlot self)
        {
            if (self.equipmentIndex == this.EquipmentDef.equipmentIndex)
            {
                //self.targetIndicator.visualizerPrefab = TargetingIndicatorPrefabBase;
                self.targetIndicator.visualizerPrefab = LegacyResourcesAPI.Load<GameObject>("Prefabs/WoodSpriteIndicator");
                self.ConfigureTargetFinderForFriendlies();
                HurtBox hurtBox = self.targetFinder.GetResults().FirstOrDefault();
                if (hurtBox != null)
                {
                    self.targetIndicator.targetTransform = hurtBox.transform;
                    self.targetIndicator.active = true;
                }
                else
                {
                    self.targetIndicator.active = false;
                }
            }
            orig(self);
        }*/

        private void GiveErosEffect(On.RoR2.CharacterBody.orig_OnInventoryChanged orig, CharacterBody self)
        {
            orig(self);
            if (!NetworkServer.active) return;
            //Debug.Log(self.inventory.GetActiveEquipment().equipmentDef);
            //Debug.Log(this.EquipmentDef);
            if (self.inventory.GetActiveEquipment().equipmentDef == this.EquipmentDef)
            {
                //itemActive = true;
                if (!erosPairs.ContainsKey(self))
                {
                    erosPairs[self] = self;
                }
            }
            else // Cleanup
            {
                //itemActive = false;
                erosPairs.Remove(self);
            }
        }

        /*private void ErosUpdate(On.RoR2.EquipmentSlot.orig_Update orig, EquipmentSlot self)
        {
            orig(self);
            if(self.equipmentIndex == this.EquipmentDef.equipmentIndex)
            {
                self.targetIndicator.visualizerPrefab = TargetingIndicatorPrefabBase;
                self.ConfigureTargetFinderForFriendlies();
                HurtBox hurtBox = self.targetFinder.GetResults().FirstOrDefault();
                if(hurtBox != null)
                {
                    self.targetIndicator.targetTransform = hurtBox.transform;
                    self.targetIndicator.active = true;
                }
                else
                {
                    self.targetIndicator.active = false;
                }
            }
        }*/

        /*private void ErosUpdate(On.RoR2.EquipmentSlot.orig_UpdateTargets orig, EquipmentSlot self, EquipmentIndex targetingEquipmentIndex, bool userShouldAnticipateTarget)
        {
            orig(self, targetingEquipmentIndex, userShouldAnticipateTarget);
            if (targetingEquipmentIndex == this.EquipmentDef.equipmentIndex)
            {
                self.ConfigureTargetFinderForFriendlies();
            }

        }*/

        private void GiveHealth(On.RoR2.GlobalEventManager.orig_OnHitEnemy orig, GlobalEventManager self, DamageInfo damageInfo, GameObject victim)
        {
            orig(self, damageInfo, victim);
            if (damageInfo.attacker && damageInfo.attacker.GetComponent<CharacterBody>())
            {
                CharacterBody attackerBody = damageInfo.attacker.GetComponent<CharacterBody>();
                if ((bool)attackerBody.equipmentSlot && attackerBody.equipmentSlot.equipmentIndex == this.EquipmentDef.equipmentIndex)
                {
                    if (erosPairs.ContainsKey(attackerBody) && erosPairs[attackerBody] != null)
                    {
                        var targetBody = erosPairs[attackerBody];
                        if (targetBody.healthComponent && targetBody.healthComponent.alive)
                        {
                            targetBody.healthComponent.HealFraction(PercentMaxHpHeal.Value, default(ProcChainMask));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// An example targeting indicator implementation. We clone the woodsprite's indicator, but we edit it to our liking. We'll use this in our activate equipment.
        /// We shouldn't need to network this as this only shows for the player with the Equipment.
        /// </summary>
        private void CreateTargetingIndicator()
        {
            //TargetingIndicatorPrefabBase = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/PassiveHealing/WoodSpriteIndicator.prefab").WaitForCompletion();
            TargetingIndicatorPrefabBase = PrefabAPI.InstantiateClone(Resources.Load<GameObject>("Prefabs/WoodSpriteIndicator"), "ErosBowIndicator", false);
            //TargetingIndicatorPrefabBase.GetComponentInChildren<SpriteRenderer>().sprite = MainAssets.LoadAsset<Sprite>("ExampleReticuleIcon.png");
            TargetingIndicatorPrefabBase.GetComponentInChildren<SpriteRenderer>().color = new Color(1, 0.753f, 0.797f); // Pink for reticle color
            TargetingIndicatorPrefabBase.GetComponentInChildren<SpriteRenderer>().transform.rotation = Quaternion.identity;
            TargetingIndicatorPrefabBase.GetComponentInChildren<TMPro.TextMeshPro>().color = new Color(1, 0.714f, 0.757f); // Light pink for text color
        }

        public override ItemDisplayRuleDict CreateItemDisplayRules()
        {
            var mpp = EquipmentModel.AddComponent<ModelPanelParameters>();
            mpp.focusPointTransform = EquipmentModel.transform.Find("Target");
            mpp.cameraPositionTransform = EquipmentModel.transform.Find("Source");
            mpp.minDistance = 4f;
            mpp.maxDistance = 8f;
            mpp.modelRotation = Quaternion.Euler(new Vector3(0, 90, 0));
            return new ItemDisplayRuleDict();
        }

        protected override bool ActivateEquipment(EquipmentSlot slot)
        {
            if (!NetworkServer.active) { return false; }
            //We check for the characterbody, and if that has an inputbank that we'll be getting our aimray from. If we don't have it, we don't continue.
            if (!slot.characterBody || !slot.characterBody.inputBank) { return false; }


            //slot.ConfigureTargetFinderForFriendlies();
            //Check for our targeting controller that we attach to the object if we have "Use Targeting" enabled.
            //var targetComponent = slot.GetComponent<TargetingControllerComponent>();

            //Ensure we have a target component, and that component is reporting that we have an object targeted.
            //if (targetComponent && targetComponent.TargetObject)
            //{
            //var chosenHurtbox = targetComponent.TargetFinder.GetResults().First();

            //Here we would use said hurtbox for something. Could be anything from firing a projectile towards it, applying a buff/debuff to it. Anything you can think of.

            //CharacterBody targetBody = GetTargetedAlly(slot);
            CharacterBody targetBody = slot.currentTarget.rootObject?.GetComponent<CharacterBody>() ?? slot.characterBody;
            if (targetBody != null && targetBody != slot.characterBody)
            {
                erosPairs[slot.characterBody] = targetBody;
            }
            else // Set target to self if no target
            {
                erosPairs[slot.characterBody] = slot.characterBody;
            }

                //}
            return true;
        }

        /*private CharacterBody GetTargetedAlly(EquipmentSlot slot)
        {
            slot.ConfigureTargetFinderForFriendlies();
            HurtBox hurtBox = slot.targetFinder.GetResults().FirstOrDefault();

            if (hurtBox != null)
            {
                return hurtBox.GetComponent<CharacterBody>();
            }
            return null;
        }*/
    }
}
