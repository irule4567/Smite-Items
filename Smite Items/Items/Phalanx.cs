using BepInEx.Configuration;
using HG;
using R2API;
using RoR2;
using Smite_Items.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using static Smite_Items.Main;

namespace Smite_Items.Items
{
    public class Phalanx : ItemBase<Phalanx>
    {
        public ConfigEntry<float> AttackSpeedBonus;
        //public ConfigEntry<float> AttackSpeedBonusPerStack;
        public ConfigEntry<int> MaxBuffStacks;
        public ConfigEntry<int> MaxBuffStacksPerStack;
        public ConfigEntry<float> BuffRadius;
        public ConfigEntry<int> BuffDuration;
        //public ConfigEntry<int> BuffDurationPerStack;

        public override string ItemName => "Phalanx";

        public override string ItemLangTokenName => "PHALANX_ITEM";

        public override string ItemPickupDesc => "You and nearby allies gain attack speed when you take damage.";

        public override string ItemFullDescription => $"Increase the <style=cIsDamage>attack speed</style> of you and allies in a <style=cIsDamage>{BuffRadius.Value}m</style> radius around you by <style=cIsDamage>{AttackSpeedBonus.Value * 100}%</style> <style=cStack>(+{AttackSpeedBonus.Value * 100}% per stack)</style> when you get hit up to <style=cIsDamage>{MaxBuffStacks.Value}</style> <style=cStack>(+{MaxBuffStacksPerStack.Value} per stack)</style> <style=cIsDamage>times</style> for <style=cIsDamage>{BuffDuration.Value}s</style>.";

        public override string ItemLore => "Item taken from Smite 1.";

        public override ItemTier Tier => ItemTier.Tier1;

        public override GameObject ItemModel => MainAssets.LoadAsset<GameObject>("PhalanxModel.prefab");

        public override Sprite ItemIcon => MainAssets.LoadAsset<Sprite>("Phalanx Icon.png");

        public static BuffDef attackSpeedOnDamageBuff;

        //public static GameObject radiusIndicator;

        public static Dictionary<CharacterBody,GameObject> radiusIndicators = new Dictionary<CharacterBody, GameObject>();

        public override void Init(ConfigFile config)
        {
            CreateConfig(config);
            CreateLang();
            CreateItem();
            CreateBuff();
            //CreateIndicator();
            Hooks();
        }

        /*private void CreateIndicator()
        {
            //radiusIndicator = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/NearbyDamageBonus/NearbyDamageBonusIndicator.prefab").WaitForCompletion();

            
        }*/

        public override void CreateConfig(ConfigFile config)
        {
            AttackSpeedBonus = config.Bind<float>("Item " + ItemName, "Attack Speed Buff", 0.075f, "How much does each buff stack increase attack speed by?");
            //AttackSpeedBonusPerStack = config.Bind<float>("Item " + ItemName, "Attack Speed Buff increase per stack", 0.05f, "How much does each additional stack of the item increase the attack speed buff by?");
            MaxBuffStacks = config.Bind<int>("Item " + ItemName, "Max stacks of buff", 3, "What is the maximum number of stacks of the attack speed buff a character can have at once?");
            MaxBuffStacksPerStack = config.Bind<int>("Item " + ItemName, "Additional max stacks of buff per stack", 3, "How many additional stacks of the buff are allowed per additional stack of the item?");
            BuffRadius = config.Bind<float>("Item " + ItemName, "Buff Radius", 20, "What is the radius, in meters, in which the attack speed buff is shared?");
            BuffDuration = config.Bind<int>("Item " + ItemName, "Buff Duration", 10, "How long, in seconds, does the item buff last?");
            //BuffDurationPerStack = config.Bind<int>("Item " + ItemName, "Added Buff Duration per stack", 5, "How long, in seconds, does each additional stack of the item increase the buff duration by?");
        }

        public void CreateBuff()
        {
            attackSpeedOnDamageBuff = ScriptableObject.CreateInstance<BuffDef>();
            attackSpeedOnDamageBuff.canStack = true;
            attackSpeedOnDamageBuff.isDebuff = false;
            attackSpeedOnDamageBuff.name = "attackSpeedOnDamageBuff";
            attackSpeedOnDamageBuff.isCooldown = false;
            attackSpeedOnDamageBuff.isHidden = false;
            attackSpeedOnDamageBuff.iconSprite = MainAssets.LoadAsset<Sprite>("Phalanx Icon.png");
            ContentAddition.AddBuffDef(attackSpeedOnDamageBuff);
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

        public override void Hooks()
        {
            On.RoR2.HealthComponent.TakeDamage += ApplyPhalanxBuff;
            RecalculateStatsAPI.GetStatCoefficients += ApplyBuffStats;
            On.RoR2.CharacterBody.OnInventoryChanged += AddIndicator;
            On.RoR2.CharacterBody.OnDestroy += CleanupIndicator;
            //Stage.onStageStartGlobal += CleanupInicatorsStage;
        }

        /*private void CleanupInicatorsStage(Stage stage)
        {
            
            radiusIndicators.Clear();
        }*/

        private void CleanupIndicator(On.RoR2.CharacterBody.orig_OnDestroy orig, CharacterBody self)
        {
            orig(self);
            if (radiusIndicators.TryGetValue(self, out GameObject existingIndicator))
            {
                if (existingIndicator != null)
                {
                    UnityEngine.Object.Destroy(existingIndicator);
                }
                radiusIndicators.Remove(self);
            }
        }

        private void AddIndicator(On.RoR2.CharacterBody.orig_OnInventoryChanged orig, CharacterBody self)
        {
            orig(self);
            if (!NetworkServer.active) return;
            var stackCount = GetCount(self);
            if (stackCount > 0)
            {
                if (!radiusIndicators.ContainsKey(self) || radiusIndicators[self] == null)
                {
                    GameObject original = LegacyResourcesAPI.Load<GameObject>("Prefabs/NetworkedObjects/NearbyDamageBonusIndicator");
                    GameObject radiusIndicator = UnityEngine.Object.Instantiate(original, self.corePosition, Quaternion.identity);
                    radiusIndicator.transform.localScale *= (BuffRadius.Value / 13f) * 2;
                    var renderer = radiusIndicator.GetComponentInChildren<Renderer>();
                    if (renderer != null)
                    {
                        //Material mat = new Material(renderer.material);
                        MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(propBlock);
                        Color newColor = new Color(0.5f, 0.2f, 1f, 0.3f);
                        propBlock.SetColor("_TintColor", newColor);
                        renderer.SetPropertyBlock(propBlock);
                        //renderer.material = mat;
                    }
                    radiusIndicator.GetComponent<NetworkedBodyAttachment>().AttachToGameObjectAndSpawn(self.gameObject);
                    radiusIndicators[self] = radiusIndicator;
                }
            }
            else if(radiusIndicators.TryGetValue(self, out GameObject existingIndicator))
            {
                if (existingIndicator != null)
                {
                    UnityEngine.Object.Destroy(existingIndicator);
                }
                radiusIndicators.Remove(self);
            }
        }

        private void ApplyBuffStats(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (sender == null) return;
            int buffCount = sender.GetBuffCount(attackSpeedOnDamageBuff);
            //var stackCount = GetCount(sender);
            if (buffCount > 0) // Set attack speed based on stacks of buffs
            {
                args.attackSpeedMultAdd += buffCount * AttackSpeedBonus.Value; /*+ ((stackCount-1) * AttackSpeedBonusPerStack.Value));*/
            }
        }

        private void ApplyPhalanxBuff(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo damageInfo)
        {
            orig(self, damageInfo);
            if (!NetworkServer.active) return;
            if (self && self.alive && self.body && self.body.inventory) // Basic sanity checks
            {
                var stackCount = GetCount(self.body);
                if (stackCount > 0)
                {
                    TeamIndex teamIndex = self.body.teamComponent.teamIndex;
                    SphereSearch sphereSearch = new SphereSearch
                    {
                        mask = LayerIndex.entityPrecise.mask,
                        origin = self.body.corePosition,
                        queryTriggerInteraction = QueryTriggerInteraction.Collide,
                        radius = BuffRadius.Value
                    };
                    TeamMask teamMask = default(TeamMask);
                    teamMask.AddTeam(teamIndex);
                    List<HurtBox> hurtBoxesList = new List<HurtBox>();
                    sphereSearch.RefreshCandidates().FilterCandidatesByHurtBoxTeam(teamMask).FilterCandidatesByDistinctHurtBoxEntities().GetHurtBoxes(hurtBoxesList);
                    foreach (HurtBox hurtBox in hurtBoxesList)
                    {
                        HealthComponent healthComponent = hurtBox.healthComponent;
                        if (healthComponent)
                        {
                            var buffCount = healthComponent.body.GetBuffCount(attackSpeedOnDamageBuff);
                            var maxBuffStacks = MaxBuffStacks.Value + ((stackCount - 1) * MaxBuffStacksPerStack.Value);
                            for (int j = 0; j < stackCount; j++) // Add a buff stack per stack of the item
                            {
                                if (buffCount < maxBuffStacks)
                                {
                                    healthComponent.body.AddTimedBuff(attackSpeedOnDamageBuff, BuffDuration.Value);
                                    buffCount = healthComponent.body.GetBuffCount(attackSpeedOnDamageBuff);
                                }
                            }
                            ItemHelpers.RefreshTimedBuffs(healthComponent.body, attackSpeedOnDamageBuff, BuffDuration.Value);
                        }
                    }
                    hurtBoxesList.Clear();
                    
                }
            }
        }
    }
}
