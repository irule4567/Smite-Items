using BepInEx.Configuration;
using R2API;
using RoR2;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using static Smite_Items.Main;

namespace Smite_Items.Items
{
    public class MysticalMail : ItemBase<MysticalMail>
    {
        public ConfigEntry<float> Frequency;
        public ConfigEntry<float> Damage;
        public ConfigEntry<float> DamagePerStack;
        public ConfigEntry<float> Radius;
        public override string ItemName => "Mystical Mail";

        public override string ItemLangTokenName => "MYSTICALMAIL_ITEM";

        public override string ItemPickupDesc => "Damage nearby enemies every second";

        public override string ItemFullDescription => $"Every <style=cIsUtility>{Frequency.Value}</style> second, deal <style=cIsDamage>{Damage.Value}</style> <style=cStack>(+{DamagePerStack.Value} per stack)</style> damage to all enemies within <style=cIsDamage>{Radius.Value}</style> meters.";

        public override string ItemLore => "Item taken from Smite 2";

        public override ItemTier Tier => ItemTier.Tier1;

        public override ItemTag[] ItemTags => new ItemTag[]
        {
            ItemTag.Damage,
            ItemTag.AIBlacklist
        };

        public override GameObject ItemModel => MainAssets.LoadAsset<GameObject>("MysticalMailModel.prefab");

        public override Sprite ItemIcon => MainAssets.LoadAsset<Sprite>("Mystical Mail Icon.png");

        public static GameObject AOEDamageField;

        //public GameObject mailPulsePrefab;
        //public static GameObject originalPulseEffect;

        public static GameObject cachedPulseEffect;

        public override void Init(ConfigFile config)
        {
            CreateConfig(config);
            CreateLang();
            //CreateAOE();
            CreateItem();
            cachedPulseEffect = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/moon2/MoonBatteryDesignPulse.prefab").WaitForCompletion();
            //cachedPulseEffect = GameObject.Instantiate(originalPulseEffect);
            //var effect = cachedPulseEffect.AddComponent<EffectComponent>();
            //ContentAddition.AddEffect(cachedPulseEffect);
            // Funky stuff to try and find a good shockwave effect, probably want to replace
            /*GameObject originalEffect = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Icicle/DisplayFrostRelic.prefab").WaitForCompletion();

            mailPulsePrefab = UnityEngine.Object.Instantiate(originalEffect);

            /*Transform foamSplashTransform = mailPulsePrefab.transform.Find("FoamSplash");
            if (foamSplashTransform) foamSplashTransform.gameObject.SetActive(false);

            ContentAddition.AddEffect(mailPulsePrefab);*/
            Hooks();
        }

        public override void CreateConfig(ConfigFile config)
        {
            Frequency = config.Bind<float>("Item " + ItemName, "Item Frequency", 1, "How often does the item effect activate?");
            Damage = config.Bind<float>("Item " + ItemName, "Damage", 15, "How much damage does each item activation do?");
            DamagePerStack = config.Bind<float>("Item " + ItemName, "Damage per stack", 15, "How much damage does each stack of the item add to the effect?");
            Radius = config.Bind<float>("Item " + ItemName, "Radius", 12, "In what radius around the player does the effect occur?");
        }

        /*private void CreateAOE()
        {
            AOEDamageField = PrefabAPI.InstantiateClone(Resources.Load<GameObject>("RoR2/Base/ExplodeOnDeath/WilloWispExplosion.prefab"), "MailAOE", true);
        }*/

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
            On.RoR2.CharacterBody.OnInventoryChanged += CharacterBody_OnInventoryChanged;
        }

        private void CharacterBody_OnInventoryChanged(On.RoR2.CharacterBody.orig_OnInventoryChanged orig, RoR2.CharacterBody self)
        {
            orig(self);

            self.AddItemBehavior<MysticalMailBehavior>(GetCount(self));
        }
    }
    public class MysticalMailBehavior : CharacterBody.ItemBehavior
    {

        private float aoeDamageTimer;
        private void FixedUpdate()
        {
            if (!body || !body.skillLocator)
                return;

            if (!NetworkServer.active)
            {
                return;
            }
            int itemCount = stack;
            if (itemCount <= 0)
                return;
            aoeDamageTimer += Time.fixedDeltaTime;
            if (aoeDamageTimer >= MysticalMail.instance.Frequency.Value)
            {
                aoeDamageTimer = 0;
                float radius = MysticalMail.instance.Radius.Value;

                //Make aoe projectile here
                /*EffectData effectData = new EffectData
                {
                    origin = body.corePosition,
                    scale = radius
                };*/
                //GameObject aoeEffect = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Common/VFX/OmniImpactVFX.prefab").WaitForCompletion();
                // Make visual effect using the pillar of design pulse, make a clone that is silent, and spawn that at the players location
                //GameObject mailPulse = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/moon2/MoonBatteryDesignPulse.prefab").WaitForCompletion();
                //GameObject silentPulse = PrefabAPI.InstantiateClone(mailPulse, "SilentPulse", false);
                /*foreach (AudioSource source in silentPulse.GetComponentsInChildren<AudioSource>())
                {
                    source.volume = 0f;
                }*/



                //GameObject spawnedEffect = GameObject.Instantiate(mailPulse, body.transform.position, body.transform.rotation);
                EffectManager.SpawnEffect(MysticalMail.cachedPulseEffect, new EffectData
                {
                    origin = body.corePosition,
                    scale = radius,
                    rotation = body.transform.rotation
                }, transmit: true);
                /*AkGameObj[] akGameObjs = spawnedEffect.GetComponentsInChildren<AkGameObj>();
                foreach (AkGameObj akObj in akGameObjs)
                {
                    akObj.enabled = false;
                }*/
                /*AudioSource[] audioSources = spawnedEffect.GetComponentsInChildren<AudioSource>();
                foreach (AudioSource audio in audioSources)
                {
                    audio.volume = 0f;
                }*/
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
                }
                NetworkServer.Spawn(spawnedEffect);*/
                //EffectManager.SpawnEffect(mailPulse, effectData, true);
                //GameObject obj = UnityEngine.Object.Instantiate(MysticalMail.instance.mailPulsePrefab, base.transform.position, base.transform.rotation);
                //NetworkServer.Spawn(obj);
                BlastAttack blastAttack = new BlastAttack
                {
                    attacker = body.gameObject,
                    baseDamage = MysticalMail.instance.Damage.Value + (stack-1)*MysticalMail.instance.DamagePerStack.Value,
                    baseForce = 0f,
                    bonusForce = Vector3.zero,
                    crit = body.RollCrit(),
                    //damageType = DamageType.AOE,
                    damageColorIndex = DamageColorIndex.Item,
                    falloffModel = BlastAttack.FalloffModel.None,
                    position = body.corePosition,
                    procChainMask = default,
                    procCoefficient = 0f,
                    radius = radius,
                    teamIndex = body.teamComponent.teamIndex,
                    inflictor = body.gameObject
                };
                //blastAttack.damageType = DamageType.AOE;
                blastAttack.Fire();
            }
        }
    }
}
