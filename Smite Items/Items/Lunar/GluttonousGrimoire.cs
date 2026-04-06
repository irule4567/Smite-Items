using BepInEx.Configuration;
using R2API;
using RoR2;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using static Smite_Items.Main;

namespace Smite_Items.Items
{
    public class GluttonousGrimoire : ItemBase<GluttonousGrimoire>
    {
        public ConfigEntry<float> PercentHealingConverted;
        public ConfigEntry<float> PercentHealingConvertedAtMax;
        public override string ItemName => "Gluttonous Grimoire";

        public override string ItemLangTokenName => "GLUTTONOUS_GRIMOIRE_ITEM";

        public override string ItemPickupDesc => "Convert healing to bonus damage.";

        public override string ItemFullDescription => $"<style=cIsHealing>Convert {PercentHealingConverted.Value*100}%</style> <style=cStack>(+{PercentHealingConverted.Value*100}% per stack hyperbolically)</style> of healing or <style=cIsHealing>{PercentHealingConvertedAtMax.Value*100}%</style> <style=cStack>(+{PercentHealingConvertedAtMax.Value*100}% per stack hyperbolically)</style> of healing at <style=cIsHealing>full health</style> into a stored <style=cIsDamage>damage</style> bonus on the next hit of your <style=cIsUtility>primary skill</style>.";

        public override string ItemLore => "Item taken from Smite 2.";

        public override ItemTier Tier => ItemTier.Lunar;
        public override ItemTag[] ItemTags => new ItemTag[]
        {
            ItemTag.Healing,
            ItemTag.Damage
        };
        public override GameObject ItemModel => MainAssets.LoadAsset<GameObject>("GluttonousGrimoireModel.prefab");

        public override Sprite ItemIcon => MainAssets.LoadAsset<Sprite>("Gluttonous Grimoire Icon.png");

        public static BuffDef storedBonusDamage;

        //public float currentPercentageHealingConverted;

        //public float currentPercentageHealingConvertedAtMaxHealth;

        private Dictionary<CharacterBody, float> cachedHealingConverted = new Dictionary<CharacterBody, float>();
        private Dictionary<CharacterBody, float> cachedHealingConvertedAtMax = new Dictionary<CharacterBody, float>();


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

        public void CreateBuff()
        {
            storedBonusDamage = ScriptableObject.CreateInstance<BuffDef>();
            storedBonusDamage.canStack = true;
            storedBonusDamage.isDebuff = false;
            storedBonusDamage.name = "storedBonusDamage";
            storedBonusDamage.isCooldown = false;
            storedBonusDamage.iconSprite = MainAssets.LoadAsset<Sprite>("Gluttonous Grimoire Icon.png");
            ContentAddition.AddBuffDef(storedBonusDamage);
        }

        public override void CreateConfig(ConfigFile config)
        {
            PercentHealingConverted = config.Bind<float>("Item: " + ItemName, "Percent healing converted", 0.25f, "What percentage of healing is converted to bonus damage on next primary skill hit?");
            PercentHealingConvertedAtMax = config.Bind<float>("Item: " + ItemName, "Percent healing converted while at max health", 0.4f, "What percentage of healing is converted to bonus damage on next primary skill hit while at maximum health?");
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
            On.RoR2.CharacterBody.OnInventoryChanged += AdjustHealingConversion;
            On.RoR2.HealthComponent.Heal += HandleHealingConversion;
            On.RoR2.CharacterBody.OnSkillActivated += TrackPrimarySkillUse;
            On.RoR2.HealthComponent.TakeDamage += GrimoireDamageBonus;
            GlobalEventManager.onCharacterDeathGlobal += CleanupDictionaries;
        }

        private void CleanupDictionaries(DamageReport report)
        {
            if (report.victimBody)
            {
                var body = report.victimBody;
                lastPrimaryUseTime.Remove(body);
                cachedHealingConverted.Remove(body);
                cachedHealingConvertedAtMax.Remove(body);
            }
        }

        private void GrimoireDamageBonus(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo damageInfo)
        {
            orig(self, damageInfo);
            if (damageInfo.attacker && damageInfo.attacker.GetComponent<CharacterBody>())
            {
                CharacterBody attackerBody = damageInfo.attacker.GetComponent<CharacterBody>();
                if (attackerBody.inventory)
                {
                    var stackCount = GetCount(attackerBody);

                    if (stackCount > 0)
                    {
                        bool isPrimaryAttack = false;
                        if (lastPrimaryUseTime.ContainsKey(attackerBody))
                        {
                            isPrimaryAttack = (Time.time - lastPrimaryUseTime[attackerBody]) < PRIMARY_SKILL_WINDOW;
                        }
                        if (attackerBody.HasBuff(storedBonusDamage) && isPrimaryAttack)
                        {
                            int bonusDamage = attackerBody.GetBuffCount(storedBonusDamage);
                            damageInfo.damage = bonusDamage; // Add bonus damage from buff
                            damageInfo.damageColorIndex = DamageColorIndex.Item;
                            damageInfo.procCoefficient = 0f;
                            damageInfo.crit = false;
                            //damageInfo.damageType = DamageType.Generic;
                            damageInfo.inflictor = damageInfo.attacker;
                            attackerBody.SetBuffCount(storedBonusDamage.buffIndex, 0);
                            self.TakeDamage(damageInfo);
                        }

                    }
                }
            }
        }

        private void TrackPrimarySkillUse(On.RoR2.CharacterBody.orig_OnSkillActivated orig, CharacterBody self, GenericSkill skill)
        {
            orig(self, skill);
            if (self.skillLocator.primary && skill)
            {
                bool isPrimary = (self.skillLocator.primary.skillDef == skill.skillDef);
                if (isPrimary)
                {
                    lastPrimaryUseTime[self] = Time.time;
                }
            }
        }

        private void AdjustHealingConversion(On.RoR2.CharacterBody.orig_OnInventoryChanged orig, CharacterBody self)
        {
            orig(self);
            int stackCount = GetCount(self);
            if (stackCount > 0)
            {
                cachedHealingConverted[self] = 1 - (1 / (1 + (PercentHealingConverted.Value * stackCount)));
                cachedHealingConvertedAtMax[self] = 1 - (1 / (1 + (PercentHealingConvertedAtMax.Value * stackCount)));
            }
            else
            {
                cachedHealingConverted.Remove(self);
                cachedHealingConvertedAtMax.Remove(self);
            }
        }

        private float HandleHealingConversion(On.RoR2.HealthComponent.orig_Heal orig, RoR2.HealthComponent self, float amount, RoR2.ProcChainMask procChainMask, bool nonRegen)
        {
            if (!NetworkServer.active) return orig(self, amount, procChainMask, nonRegen); 
            if (self && self.alive && self.body && self.body.inventory) // Basic sanity checks
            {
                var stackCount = GetCount(self.body);
                if (stackCount > 0 && nonRegen)
                {
                    // Equation for amount of healing to convert is the same as Tougher Times but with 0.25 or 0.4
                    // Equation: 1 - 1/(1 + A*x), where A = 0.25 when not at full health and 0.4 when at full health
                    //float percentHealthToConvert;
                    float reducedHealing;
                    int storedBonusDamageBuff;
                    if (self.health >= self.fullHealth)
                    {
                        reducedHealing = amount * (1 - cachedHealingConvertedAtMax.GetValueOrDefault(self.body, 0f));
                        storedBonusDamageBuff = Mathf.RoundToInt(amount * cachedHealingConvertedAtMax.GetValueOrDefault(self.body, 0f));
                    }
                    else
                    {
                        reducedHealing = amount * (1 - cachedHealingConverted.GetValueOrDefault(self.body, 0f));
                        storedBonusDamageBuff = Mathf.RoundToInt(amount * cachedHealingConverted.GetValueOrDefault(self.body, 0f));
                    }
                    
                    int buffStacks = self.body.GetBuffCount(storedBonusDamage);
                    self.body.SetBuffCount(storedBonusDamage.buffIndex, storedBonusDamageBuff + buffStacks);
                    //orig(self, reducedHealing, procChainMask, nonRegen);
                    return orig(self, reducedHealing, procChainMask, nonRegen);
                }
            }
            //orig(self, amount, procChainMask, nonRegen);
            return orig(self, amount, procChainMask, nonRegen);
        }
    }
}
