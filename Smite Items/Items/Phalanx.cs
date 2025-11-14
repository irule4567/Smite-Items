using BepInEx.Configuration;
using HG;
using R2API;
using RoR2;
using Smite_Items.Utils;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using static Smite_Items.Main;

namespace Smite_Items.Items
{
    public class Phalanx : ItemBase<Phalanx>
    {
        public ConfigEntry<float> AttackSpeedBonus;
        public ConfigEntry<float> AttackSpeedBonusPerStack;
        public ConfigEntry<int> MaxBuffStacks;
        public ConfigEntry<int> MaxBuffStacksPerStack;
        public ConfigEntry<float> BuffRadius;
        public ConfigEntry<int> BuffDuration;

        public override string ItemName => "Phalanx";

        public override string ItemLangTokenName => "PHALANX_ITEM";

        public override string ItemPickupDesc => "Gain attack speed when damaged. Share it with nearby allies.";

        public override string ItemFullDescription => $"Getting hit creates an aura in a <style=cIsDamage>{BuffRadius.Value}m</style> radius around you that increases <style=cIsDamage>attack speed</style> by <style=cIsDamage>{AttackSpeedBonus.Value*100}%</style> <style=cStack>(+{AttackSpeedBonusPerStack.Value*100}% per stack)</style> for all allies that lasts <style=cIsDamage>{BuffDuration.Value}s</style>." +
            $" Maximum cap of <style=cIsDamage>{MaxBuffStacks.Value*AttackSpeedBonus.Value*100}%</style> <style=cStack>(+{MaxBuffStacksPerStack.Value * (AttackSpeedBonus.Value + AttackSpeedBonusPerStack.Value) * 100}% per stack) <style=cIsDamage>attack speed</style>.";

        public override string ItemLore => "Item taken from Smite 1.";

        public override ItemTier Tier => ItemTier.Tier1;

        public override GameObject ItemModel => MainAssets.LoadAsset<GameObject>("PhalanxModel.prefab");

        public override Sprite ItemIcon => MainAssets.LoadAsset<Sprite>("Phalanx Icon.png");

        public static BuffDef attackSpeedOnDamageBuff;

        public static GameObject radiusIndicator;

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
            AttackSpeedBonus = config.Bind<float>("Item " + ItemName, "Attack Speed Buff", 0.1f, "How much does each buff stack increase attack speed by?");
            AttackSpeedBonusPerStack = config.Bind<float>("Item " + ItemName, "Attack Speed Buff increase per stack", 0.05f, "How much does each additional stack of the item increase the attack speed buff by?");
            MaxBuffStacks = config.Bind<int>("Item " + ItemName, "Max stacks of buff", 3, "What is the maximum number of stacks of the attack speed buff a character can have at once?");
            MaxBuffStacksPerStack = config.Bind<int>("Item " + ItemName, "Additional max stacks of buff per stack", 1, "How many additional stacks of the buff are allowed per additional stack of the item?");
            BuffRadius = config.Bind<float>("Item " + ItemName, "Buff Radius", 40, "What is the radius, in meters, in which the attack speed buff is shared?");
            BuffDuration = config.Bind<int>("Item " + ItemName, "Buff Duration", 10, "How long, in seconds, does the item buff last?");
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
        }

        private void AddIndicator(On.RoR2.CharacterBody.orig_OnInventoryChanged orig, CharacterBody self)
        {
            orig(self);
            var stackCount = GetCount(self);
            if (stackCount > 0 && radiusIndicator == null)
            {
                GameObject original = LegacyResourcesAPI.Load<GameObject>("Prefabs/NetworkedObjects/NearbyDamageBonusIndicator");
                radiusIndicator = UnityEngine.Object.Instantiate(original, self.corePosition, Quaternion.identity);
                radiusIndicator.transform.localScale *= (BuffRadius.Value/13f)*2;
                var renderer = radiusIndicator.GetComponentInChildren<Renderer>();
                if(renderer != null)
                {
                    Material mat = new Material(renderer.material);
                    Color newColor = new Color(0.5f, 0.2f, 1f, 0.3f);
                    mat.SetColor("_TintColor", newColor);
                    renderer.material = mat;
                }
                radiusIndicator.GetComponent<NetworkedBodyAttachment>().AttachToGameObjectAndSpawn(self.gameObject);
            }
            else if(stackCount == 0 && radiusIndicator != null)
            {
                UnityEngine.Object.Destroy(radiusIndicator);
                radiusIndicator=null;
            }
        }

        private void ApplyBuffStats(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            int buffCount = sender.GetBuffCount(attackSpeedOnDamageBuff);
            var stackCount = GetCount(sender);
            if (buffCount > 0) // Set movement speed based on stacks of buffs
            {
                args.attackSpeedMultAdd += buffCount * (AttackSpeedBonus.Value + ((stackCount-1) * AttackSpeedBonusPerStack.Value));
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
                    var buffCount = self.body.GetBuffCount(attackSpeedOnDamageBuff);
                    var maxBuffStacks = MaxBuffStacks.Value + ((stackCount - 1) * MaxBuffStacksPerStack.Value);
                    if(buffCount < maxBuffStacks)
                    {
                        self.body.AddTimedBuff(attackSpeedOnDamageBuff, BuffDuration.Value);
                    }
                    ItemHelpers.RefreshTimedBuffs(self.body, attackSpeedOnDamageBuff, BuffDuration.Value);
                }
            }
        }
    }
}
