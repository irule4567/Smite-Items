using BepInEx.Configuration;
using R2API;
using RoR2;
using UnityEngine;
using static Smite_Items.Main;

namespace Smite_Items.Equipment
{
    public class AegisAmulet : EquipmentBase
    {
        public ConfigEntry<float> InvulnDuration;
        public override string EquipmentName => "Aegis Amulet";

        public override string EquipmentLangTokenName => "AEGIS_AMULET_EQUIPMENT";

        public override string EquipmentPickupDesc => "Grants temporary invulernability";

        public override string EquipmentFullDescription => $"Become invulnerable for <style=cIsUtility>{InvulnDuration.Value}</style> seconds.";

        public override string EquipmentLore => "Item taken from Smite 2";

        public override GameObject EquipmentModel => MainAssets.LoadAsset<GameObject>("AegisAmuletModel.prefab");

        public override Sprite EquipmentIcon => MainAssets.LoadAsset<Sprite>("Aegis Amulet Icon.png");

        public override float Cooldown => 40;

        public override void Init(ConfigFile config)
        {
            CreateConfig(config);
            CreateLang();
            CreateEquipment();
            Hooks();
        }

        protected override void CreateConfig(ConfigFile config)
        {
            InvulnDuration = config.Bind<float>("Equipment " + EquipmentName, "Number of seconds of invulnerability from the equipment", 1.5f, "How many seconds does the equipment effect last?");
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
            slot.characterBody.AddTimedBuff(RoR2Content.Buffs.Immune, InvulnDuration.Value);
            return true;
        }


    }
}
