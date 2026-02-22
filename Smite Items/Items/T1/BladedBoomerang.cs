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
    public class BladedBoomerang : ItemBase<BladedBoomerang>
    {
        public ConfigEntry<float> CritChanceBuff;
        public ConfigEntry<float> BonusCritChanceBuffPerStack;
        public ConfigEntry<int> MaxBuffStacks;
        public ConfigEntry<float> BuffDuration;
        public ConfigEntry<float> BladeCooldown;
        public ConfigEntry<float> BladeDropLifetime;
        //public ConfigEntry<float> BladeBeginBlinking;
        public override string ItemName => "Bladed Boomerang";

        public override string ItemLangTokenName => "BLADED_BOOMERANG_ITEM";

        public override string ItemPickupDesc => "Hitting enemies spawns pickups that grant critical strike chance.";

        public override string ItemFullDescription => $"Once every <style=cIsUtility>{BladeCooldown.Value}</style> seconds, hitting an enemy spawns a <style=cIsDamage>blade</style> that when picked up grant <style=cIsDamage>+{CritChanceBuff.Value}%</style> <style=cStack>(+{BonusCritChanceBuffPerStack.Value}% per stack)</style> <style=cIsDamage>critical strike chance</style> up to <style=cIsUtility>{MaxBuffStacks.Value} times</style>. Lasts {BuffDuration.Value} seconds.";

        public override string ItemLore => "Item taken from Smite 1.";

        public override ItemTier Tier => ItemTier.Tier1;

        public override GameObject ItemModel => MainAssets.LoadAsset<GameObject>("BladedBoomerangModel.prefab");

        public override Sprite ItemIcon => MainAssets.LoadAsset<Sprite>("Bladed Boomerang Icon.png");

        public static Dictionary<CharacterBody, float> bladeRechargeTimers = new Dictionary<CharacterBody, float>();

        public static BuffDef bladedBoomerangCritChance;
        public static BuffDef bladedBoomerangReady;

        public static GameObject bladeDropPrefab;

        public override ItemTag[] ItemTags => new ItemTag[]
        {
            ItemTag.Damage
        };

        public override void Init(ConfigFile config)
        {
            CreateConfig(config);
            CreateLang();
            CreateBuff();
            //CreateAssets();
            CreateItem();
            Hooks();
        }

        /*private void CreateAssets()
        {
            GameObject pickupObject = new GameObject("BladeOrb");

            // Visual - simple sphere for now, replace with your mesh
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.transform.SetParent(pickupObject.transform);
            visual.transform.localScale = Vector3.one * 0.5f;

            // Remove the primitive's collider since we'll add our own
            UnityEngine.Object.Destroy(visual.GetComponent<Collider>());

            // Add a trigger collider for pickup detection
            SphereCollider col = pickupObject.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 1.5f; // pickup radius

            // Add Rigidbody so trigger events fire properly
            Rigidbody rb = pickupObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Add your pickup behavior script
            pickupObject.AddComponent<BladePickup>();

            // Optional: make it glow with a light
            Light light = pickupObject.AddComponent<Light>();
            light.color = Color.cyan;
            light.intensity = 2f;
            light.range = 5f;

            bladeDropPrefab = pickupObject;
        }*/

        public override void CreateConfig(ConfigFile config)
        {
            CritChanceBuff = config.Bind<float>("Item " + ItemName, "Bonus Crit Chance from Buff", 6, "How much crit chance does each stack of the bladed boomerang buff give?");
            BonusCritChanceBuffPerStack = config.Bind<float>("Item " + ItemName, "Bonus Crit Chance from Buff per stack", 6, "How much additional crit chance does each stack of the bladed boomerang buff give per additional stack of the item?");
            MaxBuffStacks = config.Bind<int>("Item " + ItemName, "Max buff stacks", 3, "What is the maximum number of bladed boomerang buff stacks a player can have?");
            BuffDuration = config.Bind<float>("Item " + ItemName, "Blade Buff Duration", 8, "How long, in seconds, does the bladed boomerang buff last?");
            BladeCooldown = config.Bind<float>("Item" + ItemName, "Blade cooldown", 2, "How much time, in seconds, must pass from a blade dropping for a new blade to be allowed to spawn?");
            BladeDropLifetime = config.Bind<float>("Item" + ItemName, "Blade drop lifetime", 10, "How much time, in seconds, does a bladed boomerang drop last after being spawned?");
            //BladeBeginBlinking = config.Bind<float>("Item" + ItemName, "Blade begin blinking time", 9, "How much time, in seconds, after being spawned does a bladed boomerang drop start blinking?");
        }

        private void CreateBuff()
        {
            bladedBoomerangCritChance = ScriptableObject.CreateInstance<BuffDef>();
            bladedBoomerangCritChance.canStack = true;
            bladedBoomerangCritChance.isDebuff = false;
            bladedBoomerangCritChance.name = "bladedBoomerangCritChance";
            bladedBoomerangCritChance.isCooldown = false;
            bladedBoomerangCritChance.iconSprite = MainAssets.LoadAsset<Sprite>("Bladed Boomerang Icon.png");
            ContentAddition.AddBuffDef(bladedBoomerangCritChance);

            bladedBoomerangReady = ScriptableObject.CreateInstance<BuffDef>();
            bladedBoomerangReady.canStack = false;
            bladedBoomerangReady.isDebuff = false;
            bladedBoomerangReady.name = "bladedBoomerangReady";
            bladedBoomerangReady.isCooldown = false;
            bladedBoomerangReady.iconSprite = MainAssets.LoadAsset<Sprite>("Bladed Boomerang Icon.png");
            ContentAddition.AddBuffDef(bladedBoomerangReady);
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
            On.RoR2.GlobalEventManager.OnHitEnemy += CheckAndSpawnBlade;
            On.RoR2.CharacterBody.OnInventoryChanged += StartCooldowns;
            On.RoR2.CharacterBody.FixedUpdate += HandleBladeRecharge;
            RecalculateStatsAPI.GetStatCoefficients += HandleBuff;
        }

        private void HandleBuff(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            int buffCount = sender.GetBuffCount(bladedBoomerangCritChance);
            int stackCount = GetCount(sender);
            if (buffCount > 0 && stackCount > 0)
            {
                //Debug.Log("Gets in adding crit chance");
                args.critAdd += buffCount * (CritChanceBuff.Value + (stackCount - 1) * BonusCritChanceBuffPerStack.Value);
            }
        }

        private void StartCooldowns(On.RoR2.CharacterBody.orig_OnInventoryChanged orig, CharacterBody self)
        {
            orig(self);
            if (!NetworkServer.active) return;
            var stackCount = GetCount(self);
            if (stackCount > 0)
            {
                //itemActive = true;
                if (!bladeRechargeTimers.ContainsKey(self))
                {
                    bladeRechargeTimers[self] = BladeCooldown.Value;
                }
            }
            else // Cleanup
            {
                //itemActive = false;
                bladeRechargeTimers.Remove(self);
                if(self.GetBuffCount(bladedBoomerangReady) > 0)
                {
                    self.RemoveBuff(bladedBoomerangReady);
                }
            }
        }

        private void HandleBladeRecharge(On.RoR2.CharacterBody.orig_FixedUpdate orig, CharacterBody self)
        {
            orig(self);
            var stackCount = GetCount(self);
            if (NetworkServer.active && stackCount > 0)
            {
                if (!bladeRechargeTimers.ContainsKey(self)) // Just in case code somehow gets here without having recharge
                {
                    bladeRechargeTimers[self] = BladeCooldown.Value;
                }

                if (bladeRechargeTimers[self] > 0 && self.GetBuffCount(bladedBoomerangReady) == 0) // Bladed boomerang is on cooldown and isn't used
                {
                    bladeRechargeTimers[self] -= Time.fixedDeltaTime;
                }
                if (bladeRechargeTimers[self] <= 0 && self.GetBuffCount(bladedBoomerangReady) == 0)
                {
                    self.AddBuff(bladedBoomerangReady);
                }
            }
        }

        private void CheckAndSpawnBlade(On.RoR2.GlobalEventManager.orig_OnHitEnemy orig, GlobalEventManager self, DamageInfo damageInfo, GameObject victim)
        {
            orig(self, damageInfo, victim);
            if (damageInfo.attacker && damageInfo.attacker.GetComponent<CharacterBody>())
            {
                CharacterBody attackerBody = damageInfo.attacker.GetComponent<CharacterBody>();
                var stackCount = GetCount(attackerBody);
                if (stackCount > 0)
                {
                    if(attackerBody.GetBuffCount(bladedBoomerangReady) >= 1)
                    {
                        //Debug.Log("Gets to right part of check");
                        //GameObject bladeDrop = UnityEngine.Object.Instantiate(bladeDropPrefab, damageInfo.position, UnityEngine.Random.rotation);
                        //TeamFilter teamFilter = attackerBody.GetComponent<TeamFilter>();
                        TeamIndex teamIndex = attackerBody.teamComponent ? attackerBody.teamComponent.teamIndex : TeamIndex.None;
                        //TeamIndex attackerTeamIndex = damageInfo.attacker.GetComponent<TeamIndex>();
                        Vector3 spawnPos = victim.GetComponent<CharacterBody>().corePosition + Vector3.up * 1f;
                        PickupSpawner.SpawnPickupAt(spawnPos, teamIndex);
                        //var pickup = bladeDrop.GetComponentInChildren<BladePickup>();
                        //pickup.team = teamFilter;
                        //pickup.baseObject = bladeDrop;
                        //NetworkServer.Spawn(bladeDrop);
                        attackerBody.RemoveBuff(bladedBoomerangReady);
                        bladeRechargeTimers[attackerBody] += BladeCooldown.Value;
                    }
                }
            }
        }

        public static void AddBladeBuff(CharacterBody originalPickupBody)
        {
            int currentBuffs = originalPickupBody.GetBuffCount(bladedBoomerangCritChance);
            if(currentBuffs < instance.MaxBuffStacks.Value)
            {
                originalPickupBody.AddTimedBuff(bladedBoomerangCritChance, instance.BuffDuration.Value);
                ItemHelpers.RefreshTimedBuffs(originalPickupBody, bladedBoomerangCritChance, instance.BuffDuration.Value);
            }
        }

        public static GameObject CreatePickupPrefab()
        {
            //Debug.Log("CreatePickupPrefab runs");
            GameObject pickupObject = new GameObject("BladeOrb");

            // Visual - simple sphere for now, replace with your mesh
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.transform.SetParent(pickupObject.transform);
            visual.transform.localScale = Vector3.one * 0.5f;

            // Remove the primitive's collider since we'll add our own
            UnityEngine.Object.Destroy(visual.GetComponent<Collider>());

            // Add a trigger collider for pickup detection
            SphereCollider col = pickupObject.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 1.5f; // pickup radius

            // Add Rigidbody so trigger events fire properly
            Rigidbody rb = pickupObject.AddComponent<Rigidbody>();
            // Settings copied from monster tooth heal pack
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.drag = 2;
            rb.angularDrag = 0.05f;
            rb.mass = 1;
            rb.detectCollisions = true;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

            pickupObject.AddComponent<TeamFilter>();

            DestroyOnTimer dt = pickupObject.AddComponent<DestroyOnTimer>();
            dt.duration = BladedBoomerang.instance.BladeDropLifetime.Value;
            dt.enabled = true;

            // Tried and failed to get blinking when about to disappear; would be easier with proper model
            /*BeginRapidlyActivatingAndDeactivating bd = pickupObject.AddComponent<BeginRapidlyActivatingAndDeactivating>();
            bd.blinkFrequency = 20f;
            bd.delayBeforeBeginningBlinking = dt.duration - 1f;
            bd.blinkingRootObject = pickupObject.gameObject;
            bd.enabled = true;
            bd.fixedAge = 0;
            bd.blinkAge = 0;*/

            // Add your pickup behavior script
            pickupObject.AddComponent<BladePickup>();

            // Optional: make it glow with a light
            Light light = pickupObject.AddComponent<Light>();
            light.color = Color.cyan;
            light.intensity = 2f;
            light.range = 5f;

            return pickupObject;
        }

        public class BladePickup : MonoBehaviour
        {
            //public TeamFilter team;
            public GameObject baseObject;
            private void OnTriggerEnter(Collider other)
            {
                //Debug.Log("Something enters trigger");
                //Debug.Log(this.team);
                if (!NetworkServer.active || !this.GetComponent<TeamFilter>() || !other)
                {
                    /*Debug.Log("Fails due to something missing");
                    Debug.Log(NetworkServer.active);
                    Debug.Log(this.GetComponent<TeamFilter>());
                    Debug.Log(other);*/
                    return;
                }
                if (TeamComponent.GetObjectTeam(other.gameObject) != this.GetComponent<TeamFilter>().teamIndex)
                {
                    //Debug.Log("Fails due to incorrect team");
                    return;
                }
                CharacterBody body = other.GetComponent<CharacterBody>();
                if (body != null)
                {
                    //Debug.Log("Gets to adding buff");
                    BladedBoomerang.AddBladeBuff(body);
                    UnityEngine.Object.Destroy(gameObject);
                }
            }
        }

        public static class PickupSpawner
        {
            //private static GameObject _pickupPrefab = CreatePickupPrefab();
            private static GameObject _pickupPrefab;
            public static void Init()
            {
                //_pickupPrefab = CreatePickupPrefab();
                // If using asset bundle instead:
                // _pickupPrefab = myBundle.LoadAsset<GameObject>("MyBuffOrb");
            }

            // Call this wherever your trigger event occurs
            public static void SpawnPickupAt(Vector3 position, TeamIndex team)
            {
                //Debug.Log(team);
                _pickupPrefab = CreatePickupPrefab();
                if (_pickupPrefab == null)
                {
                    //Debug.Log("null prefab");
                    return;
                }
                GameObject instance = UnityEngine.Object.Instantiate(
                    _pickupPrefab,
                    position,
                    Quaternion.identity
                );
                //Debug.Log("Creates orb");
                TeamFilter teamFilter = instance.GetComponent<TeamFilter>();
                //Debug.Log("Before setting team");
                if (teamFilter != null)
                {
                    //Debug.Log("Gets in setting team");
                    teamFilter.teamIndex = team;
                }
                //Debug.Log("After setting team");

                // Optional: auto-destroy after some time if not collected
                //UnityEngine.Object.Destroy(instance, BladedBoomerang.instance.BladeDropLifetime.Value);
            }
        }
    }
}
