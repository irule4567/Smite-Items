using BepInEx.Configuration;
using R2API;
using RoR2;
using System;
using System.Collections.Generic;
using UnityEngine;
using static Smite_Items.Main;

namespace Smite_Items.Items
{
    public class Hydras : ItemBase<Hydras>
    {
        public ConfigEntry<float> BonusDamage;
        public override string ItemName => "Hydras Lament";

        public override string ItemLangTokenName => "HYDRAS_ITEM";

        public override string ItemPickupDesc => "Using a skill gives your next primary skill bonus damage.";

        public override string ItemFullDescription => $"Using a non-primary skill makes your next primary skill deals an extra hit equal to <style=cIsDamage>{BonusDamage.Value * 100}%</style> <style=cStack>(+{BonusDamage.Value * 100}% per stack)</style> base damage.";

        public override string ItemLore => "Item taken from Smite 2.";

        public override ItemTier Tier => ItemTier.Tier1;
        public override ItemTag[] ItemTags => new ItemTag[]
        {
            ItemTag.Damage
        };
        public override GameObject ItemModel => MainAssets.LoadAsset<GameObject>("HydrasLamentModel.prefab");

        public override Sprite ItemIcon => MainAssets.LoadAsset<Sprite>("Hydras Lament Icon.png");

        public static BuffDef hydrasBonusDamage;

        private Dictionary<CharacterBody, float> lastPrimaryUseTime = new Dictionary<CharacterBody, float>();
        private const float PRIMARY_SKILL_WINDOW = 0.5f;

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
            hydrasBonusDamage = ScriptableObject.CreateInstance<BuffDef>();
            hydrasBonusDamage.canStack = false;
            hydrasBonusDamage.isDebuff = false;
            hydrasBonusDamage.name = "hydrasBonusDamage";
            hydrasBonusDamage.isCooldown = false;
            hydrasBonusDamage.iconSprite = MainAssets.LoadAsset<Sprite>("Hydras Lament Icon.png");
            ContentAddition.AddBuffDef(hydrasBonusDamage);
        }

        public override void CreateConfig(ConfigFile config)
        {
            //string name = ItemName == "Hydra's Lament" ? "Hydras Lament" : ItemName;
            //Debug.Log(name);
            BonusDamage = config.Bind<float>("Item " + ItemName, "Bonus primary damage", 0.30f, "What percentage of base damage is added to primary skill?");
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
            LanguageAPI.Add("ITEM_" + ItemLangTokenName + "_NAME", "Hydra's Lament");
            LanguageAPI.Add("ITEM_" + ItemLangTokenName + "_PICKUP", ItemPickupDesc);
            LanguageAPI.Add("ITEM_" + ItemLangTokenName + "_DESCRIPTION", ItemFullDescription);
            LanguageAPI.Add("ITEM_" + ItemLangTokenName + "_LORE", ItemLore);
        }

        public override void Hooks()
        {
            On.RoR2.CharacterBody.OnSkillActivated += ApplyHydrasBuff;
            On.RoR2.HealthComponent.TakeDamage += HydrasDamageBonus;
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

        private void HydrasDamageBonus(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo damageInfo)
        {
            if (damageInfo.attacker)
            {
                CharacterBody attackerBody = damageInfo.attacker.GetComponent<CharacterBody>();
                if (attackerBody.inventory)
                {
                    var stackCount = GetCount(attackerBody);

                    if (stackCount > 0)
                    {
                        if (damageInfo.procCoefficient != 0f)
                        {
                            if (damageInfo.attacker && damageInfo.attacker.GetComponent<CharacterBody>())
                            {

                                bool isPrimaryAttack = false;
                                if (lastPrimaryUseTime.ContainsKey(attackerBody))
                                {
                                    isPrimaryAttack = (Time.time - lastPrimaryUseTime[attackerBody]) < PRIMARY_SKILL_WINDOW;
                                }
                                if (attackerBody.HasBuff(hydrasBonusDamage) && isPrimaryAttack)
                                {
                                    var HydrasDamage = new DamageInfo { };
                                    HydrasDamage.damage = attackerBody.baseDamage * (BonusDamage.Value * stackCount); // Add bonus damage from buff
                                    HydrasDamage.damageColorIndex = DamageColorIndex.Item;
                                    HydrasDamage.procCoefficient = 0f;
                                    HydrasDamage.damageType = DamageType.Generic;
                                    HydrasDamage.crit = false;
                                    HydrasDamage.position = damageInfo.position;
                                    //damageInfo.damageType = DamageType.Generic;
                                    HydrasDamage.inflictor = damageInfo.inflictor;
                                    HydrasDamage.attacker = damageInfo.attacker;
                                    attackerBody.RemoveBuff(hydrasBonusDamage);
                                    self.TakeDamage(HydrasDamage);
                                }

                            }
                        }
                    }
                }
            }

            orig(self, damageInfo);
        }

        private void ApplyHydrasBuff(On.RoR2.CharacterBody.orig_OnSkillActivated orig, CharacterBody self, GenericSkill skill)
        {

            orig(self, skill);
            // If skill use wasn't primary fire, apply hydras buff if not already present
            var inventoryCount = GetCount(self);
            if (inventoryCount > 0)
            {
                bool isPrimary = (self.skillLocator.primary.skillDef == skill.skillDef);
                if(!isPrimary && self.GetBuffCount(hydrasBonusDamage) == 0)
                {
                    //Debug.Log("Buff is added");
                    self.AddBuff(hydrasBonusDamage);
                }
            }
        }
    }
}
