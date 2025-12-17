using BepInEx.Configuration;
using EntityStates.TeleporterHealNovaController;
using R2API;
using RoR2;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.UIElements;
using static Smite_Items.Main;
using static UnityEngine.UI.Image;

namespace Smite_Items.Items
{
    public class SoulGem : ItemBase<SoulGem>
    {
        public ConfigEntry<float> BonusDamage;
        public ConfigEntry<float> BonusDamagePerStack;
        public ConfigEntry<int> StacksNeeded;
        public ConfigEntry<float> HealValue;
        public ConfigEntry<float> HealValuePerStack;
        public ConfigEntry<float> HealRadius;
        public ConfigEntry<float> HealRadiusPerStack;
        public override string ItemName => "Soul Gem";

        public override string ItemLangTokenName => "SOUL_GEM_ITEM";

        public override string ItemPickupDesc => "Charge by using skills to get bonus damage and area healing.";

        public override string ItemFullDescription => $"Activating <style=cIsUtility>skills</style> stores a charge, up to <style=cIsUtility>3 charges</style>. " +
            $"Requires <style=cIsUtility>3 charges</style> for your next hit to deal <style=cIsDamage>{BonusDamage.Value*100}%</style> <style=cStack>(+{BonusDamagePerStack.Value * 100}% per stack)</style> base damage and <style=cIsHealing>heal</style> yourself and allies for <style=cIsHealing>{HealValue.Value}</style> <style=cStack>(+{HealValuePerStack.Value} per stack)</style> within <style=cIsHealing>{HealRadius.Value}m</style> <style=cStack>(+{HealRadiusPerStack.Value}m per stack)</style>.";

        public override string ItemLore => "Item taken from Smite 2";

        public override ItemTier Tier => ItemTier.Tier1;

        public override ItemTag[] ItemTags => new ItemTag[]
        {
            ItemTag.Damage,
            ItemTag.Healing
        };
        public override GameObject ItemModel => MainAssets.LoadAsset<GameObject>("SoulGemModel.prefab");

        public override Sprite ItemIcon => MainAssets.LoadAsset<Sprite>("Soul Gem Icon.png");

        public static BuffDef soulGemStack;

        private static GameObject cachedPulseEffect;
        //GameObject healingPulseEffect = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/ImpactEffects/TPHealExplosion");
        public override void Init(ConfigFile config)
        {
            CreateConfig(config);
            CreateLang();
            CreateItem();
            CreateBuff();
            cachedPulseEffect = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/TPHealingNova/TeleporterHealNovaPulse.prefab").WaitForCompletion();
            //cachedPulseEffect = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/ImpactEffects/TPHealExplosion");
            //cachedPulseEffect = LegacyResourcesAPI.Load<GameObject>("Prefabs/NetworkedObjects/TeleporterHealNovaController");
            var effect = cachedPulseEffect.AddComponent<EffectComponent>();
            ContentAddition.AddEffect(cachedPulseEffect);
            Hooks();
        }

        private void CreateBuff()
        {
            soulGemStack = ScriptableObject.CreateInstance<BuffDef>();
            soulGemStack.canStack = true;
            soulGemStack.isDebuff = false;
            soulGemStack.name = "soulGemStack";
            soulGemStack.isCooldown = false;
            soulGemStack.iconSprite = MainAssets.LoadAsset<Sprite>("Soul Gem Icon.png");
            ContentAddition.AddBuffDef(soulGemStack);
        }

        public override void CreateConfig(ConfigFile config)
        {
            BonusDamage = config.Bind<float>("Item " + ItemName, "Bonus Damage", 0.4f, "How much bonus damage (as a percent of base damage) does a proc of Soul Gem deal?");
            BonusDamagePerStack = config.Bind<float>("Item " + ItemName, "Bonus Damage Per Stack", 0.4f, "How much additional bonus damage (as a percent of base damage) does a proc of Soul Gem deal per additional stack of the item?");
            StacksNeeded = config.Bind<int>("Item " + ItemName, "Stacks Needed", 3, "How many Soul Gem buff stacks until Soul Gem activates?");
            HealValue = config.Bind<float>("Item " + ItemName, "Heal Value", 30, "How much health does an activation of Soul Gem heal?");
            HealValuePerStack = config.Bind<float>("Item " + ItemName, "Heal Value Per Stack", 30, "How much extra health does an activation of Soul Gem heal per additional stack of the item?");
            HealRadius = config.Bind<float>("Item " + ItemName, "Heal Radius", 5, "In what radius around the character does the Soul Gem activation heal in meters?");
            HealRadiusPerStack = config.Bind<float>("Item " + ItemName, "Heal Radius Per Stack", 2.5f, "How much does each stack of the item increase the heal radius by in meters?");
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
            On.RoR2.CharacterBody.OnSkillActivated += GiveSoulGemStack;
            On.RoR2.HealthComponent.TakeDamage += ActivateSoulGem;
        }

        private void ActivateSoulGem(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo damageInfo)
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
                            if (damageInfo.attacker && damageInfo.attacker.GetComponent<CharacterBody>() && NetworkServer.active)
                            {

                                if (attackerBody.GetBuffCount(soulGemStack) >= StacksNeeded.Value)
                                {
                                    var SoulGemDamage = new DamageInfo { };
                                    // Calculate and deal damage
                                    SoulGemDamage.damage = attackerBody.baseDamage * (BonusDamage.Value + BonusDamagePerStack.Value * (stackCount - 1)); // Add bonus damage from buff
                                    SoulGemDamage.damageColorIndex = DamageColorIndex.Item;
                                    SoulGemDamage.procCoefficient = 0f;
                                    SoulGemDamage.crit = false;
                                    SoulGemDamage.position = damageInfo.position;
                                    SoulGemDamage.damageType = DamageType.Generic;
                                    //damageInfo.damageType = DamageType.Generic;
                                    SoulGemDamage.inflictor = damageInfo.inflictor;
                                    SoulGemDamage.attacker = damageInfo.attacker;
                                    attackerBody.SetBuffCount(soulGemStack.buffIndex, 0);
                                    self.TakeDamage(SoulGemDamage);
                                    // Calcuate and apply healing

                                    float healRadius = HealRadius.Value + (HealRadiusPerStack.Value * (stackCount - 1));
                                    float healValue = HealValue.Value + (HealValuePerStack.Value * (stackCount - 1));
                                    /*GameObject nova = UnityEngine.Object.Instantiate(cachedPulseEffect, attackerBody.corePosition, Quaternion.identity);
                                    TeleporterHealNovaPulse pulse = nova.GetComponent<TeleporterHealNovaPulse>();
                                    if (pulse != null)
                                    {
                                        pulse.radius = healRadius;

                                    }
                                    NetworkServer.Spawn(nova);*/
                                    EffectManager.SpawnEffect(cachedPulseEffect, new EffectData
                                    {
                                        origin = attackerBody.corePosition,
                                        scale = healRadius,
                                        rotation = attackerBody.transform.rotation
                                    }, transmit: true);
                                    TeamIndex teamIndex = attackerBody.teamComponent.teamIndex;
                                    SphereSearch sphereSearch = new SphereSearch
                                    {
                                        mask = LayerIndex.entityPrecise.mask,
                                        origin = attackerBody.corePosition,
                                        queryTriggerInteraction = QueryTriggerInteraction.Collide,
                                        radius = healRadius
                                    };
                                    TeamMask teamMask = default(TeamMask);
                                    teamMask.AddTeam(teamIndex);
                                    List<HurtBox> hurtBoxesList = new List<HurtBox>();
                                    sphereSearch.RefreshCandidates().FilterCandidatesByHurtBoxTeam(teamMask).FilterCandidatesByDistinctHurtBoxEntities().GetHurtBoxes(hurtBoxesList);
                                    int i = 0;
                                    foreach (HurtBox hurtBox in hurtBoxesList)
                                    {
                                        HealthComponent healthComponent = hurtBox.healthComponent;
                                        if (healthComponent)
                                        {
                                            healthComponent.Heal(healValue, default(ProcChainMask), true);
                                        }
                                    }
                                    hurtBoxesList.Clear();
                                    //Debug.Log("gets to removing buff");
                                }

                            }
                        }
                    }
                }
            }
            orig(self, damageInfo);
        }

        private void GiveSoulGemStack(On.RoR2.CharacterBody.orig_OnSkillActivated orig, CharacterBody self, GenericSkill skill)
        {
            orig(self, skill);
            // If skill use wasn't primary fire, apply soul gem buff
            var inventoryCount = GetCount(self);
            if (inventoryCount > 0)
            {
                bool isPrimary = (self.skillLocator.primary.skillDef == skill.skillDef);
                if (!isPrimary && self.GetBuffCount(soulGemStack) < StacksNeeded.Value)
                {
                    //Debug.Log("Buff is added");
                    self.AddBuff(soulGemStack);
                }
            }
        }
    }
}
