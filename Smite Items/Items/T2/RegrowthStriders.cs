using BepInEx.Configuration;
using R2API;
using RoR2;
using Smite_Items.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using static Rewired.UI.ControlMapper.ControlMapper;
using static Smite_Items.Main;

namespace Smite_Items.Items
{
    public class RegrowthStriders : ItemBase<RegrowthStriders>
    {
        public ConfigEntry<float> MoveSpeedPerStack;
        public ConfigEntry<float> BaseBuffDuration;
        public ConfigEntry<float> AddedBuffDurationPerStack;
        public ConfigEntry<int> MaxBuffStacks;
        public ConfigEntry<float> PercentMaxHpForBuff;
        public override string ItemName => "Regrowth Striders";

        public override string ItemLangTokenName => "REGROWTH_STRIDERS_ITEM";

        public override string ItemPickupDesc => "Gain bursts of movement speed by healing.";

        public override string ItemFullDescription => $"Every <style=cIsHealing>{PercentMaxHpForBuff.Value * 100}%</style> of your <style=cIsHealing>maximum health</style> that you <style=cIsHealing>heal</style> increases <style=cIsUtility>movement speed</style> by <style=cIsUtility>{MoveSpeedPerStack.Value * 100}%</style>, up to a <style=cIsUtility>{MaxBuffStacks.Value * MoveSpeedPerStack.Value * 100}%</style> increase, fading at a rate of <style=cIsUtility>{MoveSpeedPerStack.Value * 100}% movement speed</style> every <style=cIsUtility>{BaseBuffDuration.Value}</style> <style=cStack>(+{AddedBuffDurationPerStack.Value} per stack)</style> seconds.";

        public override string ItemLore => "Item based on the item of the same name from Smite 2.";

        public override ItemTier Tier => ItemTier.Tier2;

        public override ItemTag[] ItemTags => new ItemTag[]
        {
            ItemTag.Utility
        };

        public override GameObject ItemModel => MainAssets.LoadAsset<GameObject>("RegrowthStridersModel.prefab");

        public override Sprite ItemIcon => MainAssets.LoadAsset<Sprite>("Regrowth Striders Icon.png");

        public static BuffDef regrowthMoveSpeed;

        //public static GameObject ItemBodyModelPrefab;

        public static Dictionary<CharacterBody, float> storedHealingValues = new Dictionary<CharacterBody, float>(); // Store healing values that get used for movement speed buff per character
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
            regrowthMoveSpeed = ScriptableObject.CreateInstance<BuffDef>();
            regrowthMoveSpeed.canStack = true;
            regrowthMoveSpeed.isDebuff = false;
            regrowthMoveSpeed.name = "regrowthMoveSpeed";
            regrowthMoveSpeed.isCooldown = false;
            regrowthMoveSpeed.iconSprite = MainAssets.LoadAsset<Sprite>("Regrowth Striders Icon.png");
            ContentAddition.AddBuffDef(regrowthMoveSpeed);
        }

        public override void CreateConfig(ConfigFile config)
        {
            MoveSpeedPerStack = config.Bind<float>("Item " + ItemName, "Bonus Movement Speed per Buff Stack", 0.01f, "By what percentage is movement speed increased per buff stack?");
            BaseBuffDuration = config.Bind<float>("Item " + ItemName, "Base Duration of Buff", 0.5f, "How long, in seconds, does each buff stack last when only having one stack of the item?");
            AddedBuffDurationPerStack = config.Bind<float>("Item " + ItemName, "Added Duration of Buff Per Stack", 0.1f, "How much longer, in seconds, does each buff stack last per additional stack of the item?");
            MaxBuffStacks = config.Bind<int>("Item " + ItemName, "Maximum Buff Stacks", 50, "What is the maximum number of buff stacks a character can have?");
            PercentMaxHpForBuff = config.Bind<float>("Item " + ItemName, "Percent Max Health Healed for Buff", 0.01f, "What percentage of maximum health needs to be healed to get one buff stack?");
        }

        public override ItemDisplayRuleDict CreateItemDisplayRules()
        {
            //ItemBodyModelPrefab = MainAssets.LoadAsset<GameObject>("RegrowthStridersLogbookModel.prefab");
            //var itemDisplay = ItemBodyModelPrefab.AddComponent<ItemDisplay>();
            //itemDisplay.rendererInfos = ItemHelpers.ItemDisplaySetup(ItemBodyModelPrefab);
            //GameObject modelChild = new GameObject("Model");
            //modelChild.transform.SetParent(ItemModel.transform);
            //ItemModel.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

            /*if (!ItemBodyModelPrefab.TryGetComponent<ModelPanelParameters>(out var mdlParams))
                mdlParams = ItemBodyModelPrefab.AddComponent<ModelPanelParameters>();

            if (!mdlParams.focusPointTransform)
            {
                mdlParams.focusPointTransform = new GameObject("FocusPoint").transform;
                mdlParams.focusPointTransform.SetParent(ItemBodyModelPrefab.transform);
            }
            if (!mdlParams.cameraPositionTransform)
            {
                mdlParams.cameraPositionTransform = new GameObject("CameraPosition").transform;
                mdlParams.cameraPositionTransform.SetParent(ItemBodyModelPrefab.transform);
            }*/
            // Setting focus point and camera position manually in code.
            // Works because each model is near identical.
            // If needed, can simply add Target and Source game objects to unity project
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
            /*ItemModel.AddComponent<LogbookLightingFix>();
            // Disables ambient lighting to fix issue with mid-run logbook
            Renderer[] renderers = ItemModel.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }*/
            //mdlParams.minDistance = 4f;
            //mdlParams.maxDistance = 8f;
            //mdlParams.modelRotation = Quaternion.Euler(new Vector3(0, 90, 0));
            return new ItemDisplayRuleDict();
        }

        public override void Hooks()
        {
            On.RoR2.CharacterBody.OnInventoryChanged += InitializeDict;
            On.RoR2.HealthComponent.Heal += ApplyRegrowthBuff;
            RecalculateStatsAPI.GetStatCoefficients += CalculateRegrowthSpeed;
            On.RoR2.CharacterBody.OnDeathStart += CleanupOnDeath;
        }

        private void CalculateRegrowthSpeed(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (sender && sender.inventory)
            {
                int buffStacks = sender.GetBuffCount(regrowthMoveSpeed);
                if (buffStacks > 0)
                {
                    args.moveSpeedMultAdd += MoveSpeedPerStack.Value * buffStacks;
                }
            }
        }

        private void CleanupOnDeath(On.RoR2.CharacterBody.orig_OnDeathStart orig, CharacterBody self)
        {
            orig(self);
            storedHealingValues.Remove(self);
        }
        private void InitializeDict(On.RoR2.CharacterBody.orig_OnInventoryChanged orig, CharacterBody self)
        {
            orig(self);
            if (!NetworkServer.active) return;
            var stackCount = GetCount(self);
            if (stackCount > 0)
            {
                if (!storedHealingValues.ContainsKey(self))
                {
                    storedHealingValues[self] = 0;
                }
            }
            else // Cleanup
            {
                storedHealingValues.Remove(self);
            }
        }

        private float ApplyRegrowthBuff(On.RoR2.HealthComponent.orig_Heal orig, HealthComponent self, float amount, ProcChainMask procChainMask, bool nonRegen)
        {
            var charBody = self.body;
            var stackCount = GetCount(charBody);
            if (NetworkServer.active && stackCount > 0 && nonRegen)
            {
                int buffCount = charBody.GetBuffCount(regrowthMoveSpeed);
                if (!storedHealingValues.ContainsKey(charBody)) // Just in case code somehow gets here without having recharge
                {
                    storedHealingValues[charBody] = 0f;
                }
                if(buffCount < MaxBuffStacks.Value)
                {
                    var tracker = charBody.GetComponent<IndependentStackTracker>();
                    if(tracker == null)
                    {
                        tracker = charBody.gameObject.AddComponent<IndependentStackTracker>();
                        tracker.trackedBuff = regrowthMoveSpeed;
                    }
                    float healingForBuff = charBody.maxHealth * PercentMaxHpForBuff.Value;
                    var duration = BaseBuffDuration.Value + (AddedBuffDurationPerStack.Value * (stackCount - 1));
                    storedHealingValues[charBody] += amount;
                    while (storedHealingValues[charBody] >= healingForBuff)
                    {
                        tracker.AddStack(duration);
                        storedHealingValues[charBody] -= healingForBuff;
                    }
                }
            }
            return orig(self, amount, procChainMask, nonRegen);
        }
        public class IndependentStackTracker : MonoBehaviour
        {
            private CharacterBody body;
            private readonly List<float> stackTimers = new List<float>();

            public BuffDef trackedBuff;
            public int maxStacks = RegrowthStriders.instance.MaxBuffStacks.Value;

            private void Awake()
            {
                body = GetComponent<CharacterBody>();
            }

            private void FixedUpdate()
            {
                if (!body)
                    return;

                if (!NetworkServer.active)
                    return;

                if (body.HasBuff(RegrowthStriders.regrowthMoveSpeed))
                {
                    // Look at and remove only one buff stack at a time
                    stackTimers[stackTimers.Count - 1] -= Time.fixedDeltaTime;
                    if (stackTimers[stackTimers.Count - 1] <= 0f)
                    {
                        stackTimers.RemoveAt(stackTimers.Count - 1);
                        if (body.HasBuff(trackedBuff))
                        {
                            body.RemoveBuff(trackedBuff);
                        }
                    }
                }
            }

            public void AddStack(float duration)
            {
                if (maxStacks > 0 && stackTimers.Count >= maxStacks)
                    return;  // Or replace oldest: stackTimers.RemoveAt(0); body.RemoveBuff(trackedBuff);

                stackTimers.Add(duration);
                body.AddBuff(trackedBuff);
            }

            public void ClearAllStacks()
            {
                while (body.HasBuff(trackedBuff))
                {
                    body.RemoveBuff(trackedBuff);
                }
                stackTimers.Clear();
            }

            public int CurrentStacks => stackTimers.Count;
        }

        /*public class LogbookLightingFix : MonoBehaviour
        {
            private Renderer[] renderers;

            private void Awake()
            {
                renderers = GetComponentsInChildren<Renderer>();
                ApplyLightingFix();
            }

            private void OnEnable()
            {
                ApplyLightingFix();
            }

            private void ApplyLightingFix()
            {
                foreach (Renderer renderer in renderers)
                {
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

                    // Force material property block to override any cached lighting
                    MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(mpb);

                    // Set ambient override if using Standard shader
                    mpb.SetColor("_AmbientColor", Color.white);

                    renderer.SetPropertyBlock(mpb);
                }
            }
        }*/
    }
}
