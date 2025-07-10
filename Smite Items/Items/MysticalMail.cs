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

        public override string ItemLangTokenName => "MYSTICAL_MAIL";

        public override string ItemPickupDesc => "Damage nearby enemies every second";

        public override string ItemFullDescription => $"Every <style=cIsUtility>{Frequency.Value}</style> second, deal <style=cIsDamage>{Damage.Value}</style> <style=cStack>(+{DamagePerStack.Value} per stack)</style> damage to all enemies within <style=cIsDamage>{Radius.Value}</style> meters.";

        public override string ItemLore => "Item taken from Smite 2";

        public override ItemTier Tier => ItemTier.Tier1;

        public override GameObject ItemModel => MainAssets.LoadAsset<GameObject>("ExampleItemPrefab.prefab");

        public override Sprite ItemIcon => MainAssets.LoadAsset<Sprite>("ExampleItemIcon.png");

        public static GameObject AOEDamageField;

        public GameObject mailPulsePrefab;

        public override void Init(ConfigFile config)
        {
            CreateConfig(config);
            CreateLang();
            //CreateAOE();
            CreateItem();
            // Funky stuff to try and find a good shockwave effect, probably want to replace
            GameObject originalEffect = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Common/VFX/OmniImpactVFX.prefab").WaitForCompletion();

            mailPulsePrefab = UnityEngine.Object.Instantiate(originalEffect);

            Transform foamSplashTransform = mailPulsePrefab.transform.Find("FoamSplash");
            if (foamSplashTransform) foamSplashTransform.gameObject.SetActive(false);

            ContentAddition.AddEffect(mailPulsePrefab);
            Hooks();
        }

        public override void CreateConfig(ConfigFile config)
        {
            Frequency = config.Bind<float>("Item " + ItemName, "Item Frequency", 1, "How often does the item effect activate?");
            Damage = config.Bind<float>("Item " + ItemName, "Damage", 15, "How much damage does each item activation do?");
            DamagePerStack = config.Bind<float>("Item " + ItemName, "Damage per stack", 10, "How much damage does each stack of the item add to the effect?");
            Radius = config.Bind<float>("Item " + ItemName, "Radius", 12, "In what radius around the player does the effect occur?");
        }

        /*private void CreateAOE()
        {
            AOEDamageField = PrefabAPI.InstantiateClone(Resources.Load<GameObject>("RoR2/Base/ExplodeOnDeath/WilloWispExplosion.prefab"), "MailAOE", true);
        }*/

        public override ItemDisplayRuleDict CreateItemDisplayRules()
        {
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
                EffectData effectData = new EffectData
                {
                    origin = body.corePosition,
                    scale = radius
                };
                //GameObject aoeEffect = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Common/VFX/OmniImpactVFX.prefab").WaitForCompletion();
                EffectManager.SpawnEffect(MysticalMail.instance.mailPulsePrefab, effectData, true);
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
