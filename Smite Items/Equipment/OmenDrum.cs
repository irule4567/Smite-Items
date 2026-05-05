using BepInEx.Configuration;
using R2API;
using RoR2;
using System;
using System.Collections.Generic;
using UnityEngine;
using static Smite_Items.Main;

namespace Smite_Items.Equipment
{
    public class OmenDrum : EquipmentBase<OmenDrum>
    {
        public ConfigEntry<float> Duration;
        public ConfigEntry<float> PercentEchoedDamage;
        public override string EquipmentName => "Omen Drum";

        public override string EquipmentLangTokenName => "OMEN_DRUM_EQUIP";

        public override string EquipmentPickupDesc => "Mark all enemies hit for 5 seconds. Afterwards, deal a fraction of damage dealt to all marked targets to all marked targets.";

        public override string EquipmentFullDescription => $"All enemies hit for the next <style=cIsUtility>{Duration.Value} seconds</style> are marked, with the mark duplicating <style=cIsDamage>{PercentEchoedDamage.Value*100}% of all damage taken</style> and sending it to a global damage pool. When the duration expires, ALL marked enemies suffer <style=cIsDamage>100% pooled damage</style>.";

        public override string EquipmentLore => "Item taken from Smite 2.";

        public override GameObject EquipmentModel => MainAssets.LoadAsset<GameObject>("OmenDrumModel.prefab");

        public override Sprite EquipmentIcon => MainAssets.LoadAsset<Sprite>("Omen Drum Icon.png");

        public override float Cooldown => 90;
        private List<CharacterBody> MarkedEnemies = new List<CharacterBody>();
        private float storedDamage;
        public static BuffDef omenBuff;
        public override void Init(ConfigFile config)
        {
            CreateConfig(config);
            CreateLang();
            CreateBuff();
            CreateEquipment();
            Hooks();
        }

        protected override void CreateConfig(ConfigFile config)
        {
            Duration = config.Bind<float>("Equipment: " + EquipmentName, "Duration", 5, "How long does the equipment effect last?");
            PercentEchoedDamage = config.Bind<float>("Equipment: " + EquipmentName, "Percentage damage echoed", 0.15f, "What percentage of damage dealt to all marked targets is dealt again to all marked targets at the end of the duration?");
        }

        public override ItemDisplayRuleDict CreateItemDisplayRules()
        {
            var mpp = EquipmentModel.AddComponent<ModelPanelParameters>();
            GameObject focusPoint = new GameObject("FocusPoint");
            focusPoint.transform.SetParent(EquipmentModel.transform);
            focusPoint.transform.localPosition = Vector3.zero; // Center of model
            focusPoint.transform.localRotation = Quaternion.identity;

            // Create camera position transform (defines viewing angle)
            GameObject cameraPosition = new GameObject("CameraPosition");
            cameraPosition.transform.SetParent(EquipmentModel.transform);
            cameraPosition.transform.localPosition = new Vector3(1f, 0f, 0f); // Offset from focus point
            cameraPosition.transform.localRotation = Quaternion.identity;
            mpp.focusPointTransform = focusPoint.transform; //EquipmentModel.transform.Find("Target");
            mpp.cameraPositionTransform = cameraPosition.transform; //EquipmentModel.transform.Find("Source");
            mpp.minDistance = 100f;
            mpp.maxDistance = 200f;
            mpp.modelRotation = Quaternion.Euler(new Vector3(0, 90, 0));
            mpp.modelPositionOffset = new Vector3(0, 50, 0);
            return new ItemDisplayRuleDict();
        }

        public override void Hooks()
        {
            On.RoR2.HealthComponent.TakeDamage += HandleMark;
            On.RoR2.CharacterBody.OnBuffFinalStackLost += EchoDamage;
        }

        private void EchoDamage(On.RoR2.CharacterBody.orig_OnBuffFinalStackLost orig, CharacterBody self, BuffDef buffDef)
        {
            orig(self, buffDef);
            if(buffDef == omenBuff)
            {
                float damageToDeal = storedDamage * PercentEchoedDamage.Value;
                ProcChainMask procChainMask = default(ProcChainMask);
                procChainMask.AddProc(ProcType.SharedSuffering);
                DamageInfo damageInfo = new DamageInfo
                {
                    attacker = null,
                    damage = damageToDeal,
                    procChainMask = procChainMask,
                    damageColorIndex = DamageColorIndex.Electrocution,
                    damageType = DamageType.BypassArmor,
                    procCoefficient = 0f
                };
                for (int i = 0; i < MarkedEnemies.Count; i++)
                {
                    CharacterBody enemyToBeHit = MarkedEnemies[i];
                    DamageInfo damageInfo2 = damageInfo;
                    damageInfo2.position = enemyToBeHit.corePosition;
                    enemyToBeHit.healthComponent.TakeDamage(damageInfo2);
                }
            }
        }

        private void HandleMark(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo damageInfo)
        {
            orig(self, damageInfo);
            if (damageInfo.attacker && damageInfo.attacker.GetComponent<CharacterBody>())
            {
                CharacterBody attackerBody = damageInfo.attacker.GetComponent<CharacterBody>();
                if (attackerBody.HasBuff(omenBuff))
                {
                    if (!MarkedEnemies.Contains(self.body))
                    {
                        MarkedEnemies.Add(self.body);
                    }
                }
                if (MarkedEnemies.Contains(self.body))
                {
                    storedDamage += damageInfo.damage;
                }
            }
        }

        public void CreateBuff()
        {
            omenBuff = ScriptableObject.CreateInstance<BuffDef>();
            omenBuff.canStack = false;
            omenBuff.isDebuff = false;
            omenBuff.name = "omenBuff";
            omenBuff.isCooldown = false;
            omenBuff.iconSprite = MainAssets.LoadAsset<Sprite>("Omen Drum Icon.png");
            ContentAddition.AddBuffDef(omenBuff);
        }

        protected override bool ActivateEquipment(EquipmentSlot slot)
        {
            slot.characterBody.AddTimedBuff(omenBuff, Duration.Value);
            return true;
        }


    }
}
