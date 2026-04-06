using BepInEx.Configuration;
using HG;
using R2API;
using RoR2;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using static Smite_Items.Main;

namespace Smite_Items.Items
{
    public class Pridwen : ItemBase<Pridwen>
    {
        public ConfigEntry<float> ShieldPercent;
        public ConfigEntry<float> ExplosionRadius;
        public ConfigEntry<float> ExplosionRadiusStack;
        public ConfigEntry<float> ShieldDamageMultiplier;
        public ConfigEntry<float> ShieldDamageMultiplierPerStack;
        public override string ItemName => "Glorious Pridwen";

        public override string ItemLangTokenName => "GLORIOUS_PRIDWEN_ITEM";

        public override string ItemPickupDesc => "Gain a shield that explodes when destroyed.";

        public override string ItemFullDescription => $"Whenever your <style=cIsHealing>shield</style> breaks, <style=cIsDamage>explode</style> in a <style=cIsDamage>{ExplosionRadius.Value}m</style> <style=cStack>(+{ExplosionRadiusStack.Value}m per stack)</style> radius for <style=cIsDamage>{ShieldDamageMultiplier.Value}</style> <style=cStack>(+{ShieldDamageMultiplierPerStack.Value} per stack)</style> <style=cIsDamage>times</style> your <style=cIsHealing>maximum shield</style> in <style=cIsDamage>damage</style>." + 
            $"Gain a <style=cIsHealing>shield</style> equal to <style=cIsHealing>{ShieldPercent.Value*100}%</style> of your maximum health. ";

        public override string ItemLore => "Item taken from Smite 2.";

        public override ItemTier Tier => ItemTier.Tier2;

        public override ItemTag[] ItemTags => new ItemTag[]
        {
            ItemTag.Damage,
            ItemTag.Utility
        };

        public override GameObject ItemModel => MainAssets.LoadAsset<GameObject>("GloriousPridwenModel.prefab");

        public override Sprite ItemIcon => MainAssets.LoadAsset<Sprite>("Glorious Pridwen Icon.png");

        private static GameObject cachedExplosionEffect;

        public override void Init(ConfigFile config)
        {
            CreateConfig(config);
            CreateLang();
            CreateItem();
            cachedExplosionEffect = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/moon2/MoonBatteryDesignPulse.prefab").WaitForCompletion();
            var effect = cachedExplosionEffect.AddComponent<EffectComponent>();
            ContentAddition.AddEffect(cachedExplosionEffect);
            Hooks();
        }

        public override void CreateConfig(ConfigFile config)
        {
            ShieldPercent = config.Bind<float>("Item: " + ItemName, "Percent Max Health Shield", 0.1f, "What percentage of maximum health should the first stack of the item provide in shield?");
            ExplosionRadius = config.Bind<float>("Item: " + ItemName, "Explosion Radius", 20f, "What is the base radius of the explosion when losing all shield?");
            ExplosionRadiusStack = config.Bind<float>("Item: " + ItemName, "Explosion Radius Per Stack", 4f, "How much does each stack of the item increase the explosion radius by?");
            ShieldDamageMultiplier = config.Bind<float>("Item: " + ItemName, "Shield Damage Multiplier", 10f, "How much is the characters max shield multiplied by to get the explosion damage?");
            ShieldDamageMultiplierPerStack = config.Bind<float>("Item: " + ItemName, "Shield Damage Multiplier Per Stack", 6f, "How much does each additional stack of the item add to the shield damage multiplier?");
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
            RecalculateStatsAPI.GetStatCoefficients += RecalculateStats;
            On.RoR2.HealthComponent.TakeDamage += CheckShield;
        }

        private void CheckShield(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo damageInfo)
        {
            float shieldBefore = self.shield;

            orig(self, damageInfo);

            float shieldAfter = self.shield;
            if (self.body && self.body.inventory) {
                int itemCount = GetCount(self.body);
                if (itemCount > 0) {
                    if (shieldBefore > 0f && shieldAfter <= 0f)
                    {
                        if (NetworkServer.active)
                        {
                            float explosionDamage = self.body.maxShield * (ShieldDamageMultiplier.Value + (itemCount - 1) * ShieldDamageMultiplierPerStack.Value);
                            float explosionRadius = ExplosionRadius.Value + ExplosionRadiusStack.Value * (itemCount - 1);
                            //GameObject spawnedEffect = GameObject.Instantiate(cachedExplosionEffect, self.body.transform.position, self.body.transform.rotation);
                            EffectManager.SpawnEffect(cachedExplosionEffect, new EffectData
                            {
                                origin = self.body.corePosition,
                                scale = explosionRadius,
                                rotation = self.body.transform.rotation
                            }, transmit: true);
                            /*Component[] allComponents = spawnedEffect.GetComponentsInChildren<Component>();
                            foreach (Component component in allComponents)
                            {
                                Debug.Log($"Component: {component.GetType().Name} on {component.gameObject.name}");
                            }

                            foreach (Component component in allComponents)
                            {
                                if (component.GetType().Name == "AkGameObj" || component.GetType().Name == "AkEvent")
                                {
                                    GameObject.Destroy(component);
                                }
                            }*/
                            //NetworkServer.Spawn(spawnedEffect);
                            
                            BlastAttack blastAttack = new BlastAttack
                            {
                                attacker = self.body.gameObject,
                                baseDamage = explosionDamage,
                                baseForce = 0f,
                                bonusForce = Vector3.zero,
                                crit = self.body.RollCrit(),
                                damageColorIndex = DamageColorIndex.Item,
                                falloffModel = BlastAttack.FalloffModel.None,
                                position = self.body.corePosition,
                                procChainMask = default,
                                procCoefficient = 1f,
                                radius = explosionRadius,
                                teamIndex = TeamComponent.GetObjectTeam(self.body.gameObject),
                                inflictor = self.body.gameObject
                            };
                            blastAttack.Fire();
                        }
                    } 
                }
            }
        }

        private void RecalculateStats(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            var itemCount = GetCount(sender);
            if (itemCount > 0 && sender.healthComponent != null)
            {
                float maxHealth = sender.maxHealth;
                if (maxHealth > 0)
                {
                    args.baseShieldAdd += maxHealth * ShieldPercent.Value;
                }
            }
        }
    }
}
