using BepInEx.Configuration;
using R2API;
using RoR2;
using System;
using UnityEngine;
using UnityEngine.Networking;
using static Smite_Items.Main;

namespace Smite_Items.Equipment
{
    public class DaggerOfFrenzy : EquipmentBase
    {
        //public ConfigEntry<float> BonusDamage;
        public ConfigEntry<float> BonusAttackSpeed;
        public ConfigEntry<int> NumAttacks;
        public ConfigEntry<int> SecondsReducedPerKill;
        public override string EquipmentName => "Dagger of Frenzy";

        public override string EquipmentLangTokenName => "DAGGER_FRENZY_EQUIPMENT";

        public override string EquipmentPickupDesc => $"Your next {NumAttacks.Value} Primary skill uses are buffed. Kills reduce cooldown.";

        public override string EquipmentFullDescription => $"Your next <style=cIsDamage>{NumAttacks.Value}</style> uses of your <style=cIsUtility>Primary skill</style> have their <style=cIsDamage>attack speed</style> increased by <style=cIsDamage>{BonusAttackSpeed.Value*100}%</style>. <style=cIsDamage>Kills reduce</style> <style=cIsUtility>your equipment cooldown</style> by <style=cIsUtility>{SecondsReducedPerKill.Value}s</style>.";

        public override string EquipmentLore => "Item taken from Smite 2.";

        public override GameObject EquipmentModel => MainAssets.LoadAsset<GameObject>("DaggerOfFrenzyModel.prefab");

        public override Sprite EquipmentIcon => MainAssets.LoadAsset<Sprite>("Dagger of Frenzy Icon.png");

        public static BuffDef daggerBuff;

        public override float Cooldown => 30;

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
            //BonusDamage = config.Bind<float>("Equipment " + EquipmentName, "Percentage increase in damage from the buff", 0.5f, "How much bonus damage does the buff provide to primary skill fires?");
            BonusAttackSpeed = config.Bind<float>("Equipment: " + EquipmentName, "Percentage increase in primary skill attack speed", 0.5f, "How much bonus attack speed is the primary skill given from the buff?");
            NumAttacks = config.Bind<int>("Equipment: " + EquipmentName, "Number of buffed attacks", 6, "How many primary skill activations does the buff apply to?");
            SecondsReducedPerKill = config.Bind<int>("Equipment: " + EquipmentName, "Seconds reduced off cooldown per kill", 2, "How much is the equipment cooldown reduced by on kill?");
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
            //On.RoR2.CharacterBody.OnSkillActivated += ApplyDaggerAttackSpeedBuff;
            On.RoR2.GenericSkill.OnExecute += ApplyDaggerAttackSpeedBuff;
            On.RoR2.GlobalEventManager.OnCharacterDeath += ApplyDaggerCooldownReduction;
            //On.RoR2.GlobalEventManager.ProcessHitEnemy += ApplyDaggerDamageBuff;
        }

        private void ApplyDaggerCooldownReduction(On.RoR2.GlobalEventManager.orig_OnCharacterDeath orig, GlobalEventManager self, DamageReport damageReport)
        {
            orig(self, damageReport);
            if(!damageReport.attacker || !damageReport.attackerBody || !damageReport.victim || !damageReport.victimBody)
            {
                return;
            }
            CharacterBody attackerBody = damageReport.attackerBody;
            if((bool)attackerBody.equipmentSlot && attackerBody.equipmentSlot.equipmentIndex == this.EquipmentDef.equipmentIndex)
            {
                attackerBody.inventory.DeductActiveEquipmentCooldown(SecondsReducedPerKill.Value);
            }
        }

        private void ApplyDaggerAttackSpeedBuff(On.RoR2.GenericSkill.orig_OnExecute orig, GenericSkill self)
        {
            if (self != null && self.characterBody != null)
            {
                bool isPrimary = (self.characterBody.skillLocator.primary.skillDef == self.skillDef);
                if (isPrimary && self.characterBody.HasBuff(daggerBuff))
                {
                    self.characterBody.attackSpeed *= 1 + BonusAttackSpeed.Value;
                    if (NetworkServer.active) {
                        self.characterBody.RemoveBuff(daggerBuff);
                    }
                }
            }
            orig(self);
        }

        /*private void ApplyDaggerDamageBuff(On.RoR2.GlobalEventManager.orig_ProcessHitEnemy orig, GlobalEventManager self, DamageInfo damageInfo, GameObject victim)
        {
            throw new NotImplementedException();
        }*/

        /*private void ApplyDaggerAttackSpeedBuff(On.RoR2.CharacterBody.orig_OnSkillActivated orig, CharacterBody self, GenericSkill skill)
        {

            bool isPrimary = (self.skillLocator.primary.skillDef == skill.skillDef);
            if (isPrimary && self.HasBuff(daggerBuff))
            {
                skill.cooldownScale /= 1 + BonusAttackSpeed.Value;
                self.RemoveBuff(daggerBuff);
            }
            orig(self, skill);
        }*/

        public void CreateBuff()
        {
            daggerBuff = ScriptableObject.CreateInstance<BuffDef>();
            daggerBuff.canStack = true;
            daggerBuff.isDebuff = false;
            daggerBuff.name = "daggerBuff";
            daggerBuff.isCooldown = false;
            daggerBuff.iconSprite = MainAssets.LoadAsset<Sprite>("Dagger of Frenzy Icon.png");
            ContentAddition.AddBuffDef(daggerBuff);
        }
        protected override bool ActivateEquipment(EquipmentSlot slot)
        {
            slot.characterBody.SetBuffCount(daggerBuff.buffIndex, NumAttacks.Value);
            return true;
        }


    }
}
