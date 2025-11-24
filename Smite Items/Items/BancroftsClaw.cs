using BepInEx.Configuration;
using R2API;
using RoR2;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using static Smite_Items.Main;

namespace Smite_Items.Items
{
    public class BancroftsClaw : ItemBase<BancroftsClaw>
    {
        public ConfigEntry<float> BaseDamage;
        public ConfigEntry<float> BonusBaseDamagePerStack;
        public ConfigEntry<int> Recharge;
        public ConfigEntry<int> MaxHungerStacks;
        public ConfigEntry<int> BonusMaxHungerStacksPerStack;
        public ConfigEntry<float> MaxHPBarrier;
        public ConfigEntry<float> Radius;
        public override string ItemName => "Bancrofts Claw";

        public override string ItemLangTokenName => "BANCROFTS_CLAW_ITEM";

        public override string ItemPickupDesc => "Activating non-Primary skills also blasts nearby enemies and grants temporary barrier. Recharges over time.";

        public override string ItemFullDescription => $"Activating a <style=cIsUtility>Non-Primary skill</style> also unleashes a <style=cIsDamage>consuming blast</style> around you, dealing <style=cIsDamage>{BaseDamage.Value}%</style> <style=cStack>(+{BonusBaseDamagePerStack.Value}% per stack)</style> base damage. Each target hit also grants a <style=cIsHealing>temporary barrier</style> for <style=cIsHealing>{MaxHPBarrier.Value}%</style> of <style=cIsHealing>maximum health</style>. " +
            $"Can hold up to <style=cIsUtility>{MaxHungerStacks.Value}</style> <style=cStack>(+{BonusMaxHungerStacksPerStack.Value} per stack)</style> <style=cIsDamage>charges</style> which all reload over <style=cIsUtility>{Recharge.Value}</style> seconds.";

        public override string ItemLore => "Item taken from Smite 1.";

        public override ItemTier Tier => ItemTier.Tier2;

        //public float currentClawRecharge;

        //public bool itemActive;
        public override GameObject ItemModel => MainAssets.LoadAsset<GameObject>("BancroftsClawModel.prefab");

        public override Sprite ItemIcon => MainAssets.LoadAsset<Sprite>("Bancrofts Claw Icon.png");

        public static BuffDef clawCharge;

        public static Dictionary<CharacterBody, GameObject> clawRadiusIndicators = new Dictionary<CharacterBody, GameObject>();

        public static Dictionary <CharacterBody, float> clawRechargeTimers = new Dictionary<CharacterBody, float>();
        //public static Dictionary <CharacterBody, bool> clawActiveStates = new Dictionary<CharacterBody, bool>();
        public static GameObject cachedDamageEffect;

        public override ItemTag[] ItemTags => new ItemTag[]
        {
            ItemTag.Damage,
            ItemTag.Healing,
            ItemTag.AIBlacklist
        };

        // Recharge formula: 20/(itemAmount+2)
        public override void Init(ConfigFile config)
        {
            CreateConfig(config);
            CreateLang();
            CreateBuff();
            CreateItem();
            //ToDo: Find better effect
            cachedDamageEffect = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Common/VFX/ExplosionVFX.prefab").WaitForCompletion();
            Hooks();
        }

        public override void CreateConfig(ConfigFile config)
        {
            BaseDamage = config.Bind<float>("Item " + ItemName, "Base Damage", 500f, "What percentage of base damage does the blast deal?");
            BonusBaseDamagePerStack = config.Bind<float>("Item " + ItemName, "Bonus Base Damage Per Stack", 100f, "How much does additional stack of the item increase the percentage base damage by?");
            Recharge = config.Bind<int>("Item " + ItemName, "Recharge", 20, "How long, in seconds, does it take for all stacks of hunger to recharge?");
            MaxHungerStacks = config.Bind<int>("Item " + ItemName, "Max Hunger Stacks", 3, "What is the maximum number of stacks of hunger that can be stored at a time?");
            BonusMaxHungerStacksPerStack = config.Bind<int>("Item " + ItemName, "Bonus Max Hunger Stacks Per Stack", 1, "How many additional stacks of hunger can be stored per additional item stack?");
            MaxHPBarrier = config.Bind<float>("Item " + ItemName, "Max HP Barrier", 10f, "What percentage of maximum HP is given as barrier?");
            Radius = config.Bind<float>("Item " + ItemName, "Radius", 10f, "What is the radius of the claw effect?");
        }

        public override ItemDisplayRuleDict CreateItemDisplayRules()
        {
            var mpp = ItemModel.AddComponent<ModelPanelParameters>();
            mpp.focusPointTransform = ItemModel.transform.Find("Target");
            mpp.cameraPositionTransform = ItemModel.transform.Find("Source");
            mpp.minDistance = 4f;
            mpp.maxDistance = 8f;
            mpp.modelRotation = Quaternion.Euler(new Vector3(0, 90, 0));
            return new ItemDisplayRuleDict();
        }

        protected override void CreateLang()
        {
            LanguageAPI.Add("ITEM_" + ItemLangTokenName + "_NAME", "Bancroft's Claw");
            LanguageAPI.Add("ITEM_" + ItemLangTokenName + "_PICKUP", ItemPickupDesc);
            LanguageAPI.Add("ITEM_" + ItemLangTokenName + "_DESCRIPTION", ItemFullDescription);
            LanguageAPI.Add("ITEM_" + ItemLangTokenName + "_LORE", ItemLore);
        }

        public void CreateBuff()
        {
            clawCharge = ScriptableObject.CreateInstance<BuffDef>();
            clawCharge.canStack = true;
            clawCharge.isDebuff = false;
            clawCharge.name = "clawCharge";
            clawCharge.isCooldown = true;
            clawCharge.iconSprite = MainAssets.LoadAsset<Sprite>("Bancrofts Claw Icon.png");
            ContentAddition.AddBuffDef(clawCharge);
        }

        public override void Hooks()
        {
            On.RoR2.CharacterBody.OnInventoryChanged += StartCooldowns;
            On.RoR2.CharacterBody.FixedUpdate += HandleClawCharge;
            On.RoR2.CharacterBody.OnSkillActivated += CheckAndApplyHunger;
            On.RoR2.CharacterBody.OnDeathStart += CleanupOnDeath;
        }

        private void CleanupOnDeath(On.RoR2.CharacterBody.orig_OnDeathStart orig, CharacterBody self)
        {
            orig(self);
            clawRechargeTimers.Remove(self);
            if (clawRadiusIndicators.TryGetValue(self, out GameObject existingIndicator))
            {
                if (existingIndicator != null)
                {
                    UnityEngine.Object.Destroy(existingIndicator);
                }
                clawRadiusIndicators.Remove(self);
            }
        }

        private void StartCooldowns(On.RoR2.CharacterBody.orig_OnInventoryChanged orig, CharacterBody self)
        {
            orig(self);
            if (!NetworkServer.active) return;
            var stackCount = GetCount(self);
            if(stackCount > 0)
            {
                //itemActive = true;
                if (!clawRechargeTimers.ContainsKey(self))
                {
                    clawRechargeTimers[self] = Recharge.Value / (stackCount + 2);
                }
            }
            else // Cleanup
            {
                //itemActive = false;
                clawRechargeTimers.Remove(self);
                while (self.HasBuff(clawCharge))
                {
                    self.RemoveBuff(clawCharge);
                }
                if (clawRadiusIndicators.TryGetValue(self, out GameObject existingIndicator))
                {
                    if (existingIndicator != null)
                    {
                        UnityEngine.Object.Destroy(existingIndicator);
                    }
                    clawRadiusIndicators.Remove(self);
                }
            }
        }

        private void HandleClawCharge(On.RoR2.CharacterBody.orig_FixedUpdate orig, CharacterBody self)
        {
            orig(self);
            var stackCount = GetCount(self);
            if (NetworkServer.active && stackCount > 0)
            {
                if (!clawRechargeTimers.ContainsKey(self)) // Just in case code somehow gets here without having recharge
                {
                    clawRechargeTimers[self] = Recharge.Value / (stackCount + 2);
                }
                var buffCount = self.GetBuffCount(clawCharge);
                var maxBuffStacks = MaxHungerStacks.Value + ((stackCount - 1) * BonusMaxHungerStacksPerStack.Value);
                
                if(buffCount < maxBuffStacks)
                {
                    clawRechargeTimers[self] -= Time.fixedDeltaTime;
                }
                if(clawRechargeTimers[self] <= 0 && buffCount < maxBuffStacks)
                {
                    clawRechargeTimers[self] += (Recharge.Value / (stackCount + 2));
                    self.AddBuff(clawCharge);
                }
                if(buffCount > 0)
                {
                    if (!clawRadiusIndicators.ContainsKey(self) || clawRadiusIndicators[self] == null) // Check for and apply radius indicator if have at least one charge
                    {
                        GameObject original = LegacyResourcesAPI.Load<GameObject>("Prefabs/NetworkedObjects/NearbyDamageBonusIndicator");
                        GameObject radiusIndicator = UnityEngine.Object.Instantiate(original, self.corePosition, Quaternion.identity);
                        radiusIndicator.transform.localScale *= (Radius.Value / 13f) * 2;
                        var renderer = radiusIndicator.GetComponentInChildren<Renderer>();
                        if (renderer != null)
                        {
                            //Material mat = new Material(renderer.material);
                            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
                            renderer.GetPropertyBlock(propBlock);
                            Color newColor = new Color(0.31f, 0.588f, 0.8588f, 0.6f);
                            propBlock.SetColor("_TintColor", newColor);
                            renderer.SetPropertyBlock(propBlock);
                            //renderer.material = mat;
                        }
                        radiusIndicator.GetComponent<NetworkedBodyAttachment>().AttachToGameObjectAndSpawn(self.gameObject);
                        clawRadiusIndicators[self] = radiusIndicator;
                    }
                }
                else // Remove radius indicator if no charges are present
                {
                    if (clawRadiusIndicators.TryGetValue(self, out GameObject existingIndicator))
                    {
                        if (existingIndicator != null)
                        {
                            UnityEngine.Object.Destroy(existingIndicator);
                        }
                        clawRadiusIndicators.Remove(self);
                    }
                }
            }
        }

        private void CheckAndApplyHunger(On.RoR2.CharacterBody.orig_OnSkillActivated orig, CharacterBody self, GenericSkill skill)
        {
            orig(self, skill);
            var inventoryCount = GetCount(self);
            if (inventoryCount > 0)
            {
                bool isPrimary = (self.skillLocator.primary.skillDef == skill.skillDef);
                if(!isPrimary && self.HasBuff(clawCharge))
                {
                    SphereSearch enemySearch = new SphereSearch
                    {
                        origin = self.corePosition,
                        radius = Radius.Value,
                        mask = LayerIndex.entityPrecise.mask
                    };
                    enemySearch.RefreshCandidates();
                    enemySearch.FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(self.teamComponent.teamIndex));
                    enemySearch.FilterCandidatesByDistinctHurtBoxEntities();
                    List<HurtBox> hurtBoxes = new List<HurtBox>();
                    enemySearch.GetHurtBoxes(hurtBoxes);
                    enemySearch.ClearCandidates();
                    if(hurtBoxes.Count > 0) // Only activate if at least one enemy is in range
                    {
                        if ((object)cachedDamageEffect != null)
                        {
                            EffectManager.SpawnEffect(cachedDamageEffect, new EffectData
                            {
                                origin = self.corePosition,
                                scale = Radius.Value
                            }, transmit: true);
                        }
                        BlastAttack blastAttack = new BlastAttack
                        {
                            attacker = self.gameObject,
                            baseDamage = self.baseDamage * ((BaseDamage.Value/100f) + ((inventoryCount-1)*BonusBaseDamagePerStack.Value/100)),
                            baseForce = 0f,
                            bonusForce = Vector3.zero,
                            crit = self.RollCrit(),
                            //damageType = DamageType.AOE,
                            damageColorIndex = DamageColorIndex.Item,
                            falloffModel = BlastAttack.FalloffModel.None,
                            position = self.corePosition,
                            procChainMask = default,
                            procCoefficient = 0f,
                            radius = Radius.Value,
                            teamIndex = self.teamComponent.teamIndex,
                            inflictor = self.gameObject
                        };
                        //blastAttack.damageType = DamageType.AOE;
                        blastAttack.Fire();
                        self.healthComponent.AddBarrier(self.maxHealth * hurtBoxes.Count * (MaxHPBarrier.Value / 100));
                        self.RemoveBuff(clawCharge);
                    }
                    hurtBoxes.Clear();
                }
            }
        }
    }
}
