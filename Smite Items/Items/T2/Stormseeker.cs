using BepInEx.Configuration;
using R2API;
using RoR2;
using System;
using UnityEngine;
using UnityEngine.Networking;
using static Smite_Items.Main;

namespace Smite_Items.Items
{
    public class Stormseeker : ItemBase<Stormseeker>
    {
        public ConfigEntry<float> AttackSpeedPerStack;
        public ConfigEntry<int> MaxStacksPerStack;
        public override string ItemName => "Stormseeker";

        public override string ItemLangTokenName => "STORMSEEKER_ITEM";

        public override string ItemPickupDesc => "Gain permanent increased attack speed by hitting enemies";

        public override string ItemFullDescription => $"Dealing damage increases your <style=cIsDamage>attack speed permanently</style> by <style=cIsDamage>{AttackSpeedPerStack.Value*100}%</style> <style=cStack>(+{AttackSpeedPerStack.Value * 100}% per stack)</style>, up to a <style=cIsDamage>maximum</style> increase of <style=cIsDamage>{MaxStacksPerStack.Value* AttackSpeedPerStack.Value * 100}%</style> <style=cStack>(+{MaxStacksPerStack.Value * AttackSpeedPerStack.Value * 100}% per stack)</style>.";

        public override string ItemLore => "Item taken from Smite 1.";

        public override ItemTier Tier => ItemTier.Tier2;

        public override ItemTag[] ItemTags => new ItemTag[]
       {
            ItemTag.Damage
       };

        public override GameObject ItemModel => MainAssets.LoadAsset<GameObject>("StormseekerModel.prefab");

        public override Sprite ItemIcon => MainAssets.LoadAsset<Sprite>("Stormseeker Icon.png");

        public override void Init(ConfigFile config)
        {
            CreateConfig(config);
            CreateLang();
            CreateItem();
            Hooks();
        }

        public override void CreateConfig(ConfigFile config)
        {
            AttackSpeedPerStack = config.Bind<float>("Item: " + ItemName, "Attack speed per stack", 0.0005f, "How much permanent additional attack speed is given per stacks from hits?");
            MaxStacksPerStack = config.Bind<int>("Item: " + ItemName, "Max attack speed stacks", 1000, "How many stacks of bonus attack speed can each stack of the item provide?");
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
            On.RoR2.GlobalEventManager.OnHitEnemy += ApplyStormBuff;
            RecalculateStatsAPI.GetStatCoefficients += RecalculateStats;
        }

        private void ApplyStormBuff(On.RoR2.GlobalEventManager.orig_OnHitEnemy orig, GlobalEventManager self, DamageInfo damageInfo, GameObject victim)
        {
            orig(self, damageInfo, victim);
            if (!NetworkServer.active)
            {
                return;
            }
            if (damageInfo.attacker && damageInfo.procCoefficient > 0f)
            {
                CharacterBody inflictor = damageInfo.attacker.GetComponent<CharacterBody>();
                
                //Inventory inventory = master.inventory;
                if (inflictor)
                {
                    int stormItem = GetCount(inflictor);
                    if (stormItem > 0)
                    {
                        CharacterMaster master = inflictor.master;
                        if (master)
                        {
                            var tracker = master.GetComponent<StormseekerTracker>();
                            if (!tracker)
                            {
                                tracker = master.gameObject.AddComponent<StormseekerTracker>();
                            }
                            if (tracker.attackSpeedStacks < stormItem * MaxStacksPerStack.Value) // Check for maximum stacks
                            {
                                tracker.attackSpeedStacks = tracker.attackSpeedStacks + stormItem; // Add one stack per item stack
                                if (tracker.attackSpeedStacks > stormItem * MaxStacksPerStack.Value) // Set to max if it goes over
                                {
                                    tracker.attackSpeedStacks = stormItem * MaxStacksPerStack.Value;
                                }
                                inflictor.statsDirty = true; // Ensure stats immediately get updated
                            }
                        }
                    }
                }
            }
        }

        private void RecalculateStats(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (sender)
            {
                var itemCount = GetCount(sender);
                if (itemCount > 0 && sender.healthComponent != null)
                {
                    var tracker = sender.master?.GetComponent<StormseekerTracker>();
                    if (tracker)
                    {
                        int maxStacks = itemCount * MaxStacksPerStack.Value;
                        if(tracker.attackSpeedStacks > maxStacks)
                        {
                            tracker.attackSpeedStacks = maxStacks;
                        }
                        //Debug.Log("Item stacks: " + tracker.attackSpeedStacks);
                        args.baseAttackSpeedAdd += tracker.attackSpeedStacks * AttackSpeedPerStack.Value;
                    }
                }
            }
        }
    }
    public class StormseekerTracker : MonoBehaviour
    {
        public int attackSpeedStacks = 0;
    }
}
