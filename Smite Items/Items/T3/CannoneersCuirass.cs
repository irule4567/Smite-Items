using BepInEx.Configuration;
using HG;
using R2API;
using RoR2;
using RoR2.Orbs;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using static Smite_Items.Main;

namespace Smite_Items.Items
{
    public class CannoneersCuirass : ItemBase<CannoneersCuirass>
    {
        public ConfigEntry<float> ItemCooldown;
        public ConfigEntry<float> ExplosionRadius;
        public ConfigEntry<float> BonusExplosionRadiusPerStack;
        public ConfigEntry<float> PercentHealthExplosionDamage;
        public ConfigEntry<float> BonusPercentHealthExplosionDamagePerStack;
        public ConfigEntry<int> BaseGoldOnProc;
        public ConfigEntry<int> BonusGoldPerStack;
        public override string ItemName => "Cannoneers Cuirass";

        public override string ItemLangTokenName => "CANNONEERS_CUIRASS_ITEM";

        public override string ItemPickupDesc => "Explode non-boss enemies for bonus gold. Recharges over time.";

        public override string ItemFullDescription => $"Your next attack will <style=cIsDamage>instantly kill</style> a <style=cIsDamage>non-Boss enemy</style> and create an <style=cIsDamage>explosion</style> in a <style=cIsDamage>{ExplosionRadius.Value}m</style> <style=cStack>(+{BonusExplosionRadiusPerStack.Value}m per stack)</style> radius for <style=cIsHealth>{PercentHealthExplosionDamage.Value * 100}%</style> <style=cStack>(+{BonusPercentHealthExplosionDamagePerStack.Value * 100}% per stack)</style> of that enemy's <style=cIsHealth>health</style>." +
            $" Additionally, you gain <style=cIsUtility>{BaseGoldOnProc.Value}</style> <style=cStack>(+{BonusGoldPerStack.Value} per stack)</style> <style=cIsUtility>gold</style> that <style=cIsUtility>scales over time</style>. Recharges every <style=cIsUtility>{ItemCooldown.Value}</style> seconds.";

        public override string ItemLore => "Item taken from Smite 1.";

        public override ItemTier Tier => ItemTier.Tier3;

        public override ItemTag[] ItemTags => new ItemTag[]
        {
            ItemTag.AIBlacklist,
            ItemTag.BrotherBlacklist,
            ItemTag.Damage,
            ItemTag.Utility,
            ItemTag.OnKillEffect
        };

        public override GameObject ItemModel => MainAssets.LoadAsset<GameObject>("CannoneersCuirassModel.prefab");

        public override Sprite ItemIcon => MainAssets.LoadAsset<Sprite>("Cannoneers Cuirass Icon.png");

        public static BuffDef cannoneerCooldown;

        public static BuffDef cannoneerReady;

        //public static GameObject originalExplosionEffect;
        public static GameObject cachedExplosionEffect;

        public override void Init(ConfigFile config)
        {
            CreateConfig(config);
            CreateLang();
            CreateItem();
            CreateBuff();
            Hooks();
            cachedExplosionEffect = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Common/VFX/ExplosionVFX.prefab").WaitForCompletion();
            //cachedExplosionEffect = GameObject.Instantiate(originalExplosionEffect);
            //var effect = cachedExplosionEffect.AddComponent<EffectComponent>();
            //ContentAddition.AddEffect(cachedExplosionEffect);
        }

        public override void CreateConfig(ConfigFile config)
        {
            ItemCooldown = config.Bind<float>("Item " + ItemName, "Time between each activation of the item", 7, "How many seconds does the item need before it can activate again?");
            ExplosionRadius = config.Bind<float>("Item " + ItemName, "Radius of the item explosion in meters", 10, "What is the radius of the explosion triggered by the item effect?");
            BonusExplosionRadiusPerStack = config.Bind<float>("Item " + ItemName, "Additional radius of the item explosion in meters per additional stack", 2, "How much does each additional stack of the item increase the explosion radius by?");
            PercentHealthExplosionDamage = config.Bind<float>("Item " + ItemName, "Percent maximum health explosion damage", 0.1f, "What percentage of the targets maximum health does the explosion deal?");
            BonusPercentHealthExplosionDamagePerStack = config.Bind<float>("Item " + ItemName, "Additional percent maximum health explosion damage per additional stack", 0.05f, "How much does each additional stack of the item increase the percentage of maximum health the explosion deals?");
            BaseGoldOnProc = config.Bind<int>("Item " + ItemName, "Base gold on item proc", 8, "What is the base gold an activation of the item grants?");
            BonusGoldPerStack = config.Bind<int>("Item " + ItemName, "Additional base gold on item proc per additional item stack", 8, "How much does each additional stack of the item increase the base gold of an item activation?");
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
            On.RoR2.CharacterBody.OnInventoryChanged += CharacterBody_OnInventoryChanged;
            On.RoR2.HealthComponent.TakeDamage += ProcExplosion;
            On.RoR2.CharacterBody.FixedUpdate += CheckCooldown;
        }

        private void CheckCooldown(On.RoR2.CharacterBody.orig_FixedUpdate orig, CharacterBody self)
        {
            orig(self);
            if (!self || !self.inventory) return;
            
            int itemCount = GetCount(self);
            if (itemCount <= 0) return;
                
            if(!self.HasBuff(cannoneerReady) && !self.HasBuff(cannoneerCooldown)) // Check if cooldown is finished
            {
                self.AddBuff(cannoneerReady);
            }
        }

        private void ProcExplosion(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo damageInfo)
        {
            if (damageInfo.attacker && damageInfo.attacker.GetComponent<CharacterBody>() && NetworkServer.active)
            {
                CharacterBody attackerBody = damageInfo.attacker.GetComponent<CharacterBody>();
                if (attackerBody.inventory)
                {
                    var stackCount = GetCount(attackerBody);
                    if (stackCount > 0 && attackerBody.HasBuff(cannoneerReady) && self.body && !self.body.isBoss) // Core checks: Attacker has item and buff and victim isn't a boss
                    {
                        attackerBody.RemoveBuff(cannoneerReady);
                        for (int k = 1; k <= ItemCooldown.Value; k++)
                        {
                            attackerBody.AddTimedBuff(cannoneerCooldown, k);
                        }
                        DamageReport damageReport = new DamageReport(damageInfo, self, damageInfo.damage, self.combinedHealth);
                        float executionHealthLost = Mathf.Max(self.combinedHealth, 0f);
                        if (self.health > 0f)
                        {
                            self.Networkhealth = 0f;
                        }
                        if (self.shield > 0f)
                        {
                            self.Networkshield = 0f;
                        }
                        if (self.barrier > 0f)
                        {
                            self.Networkbarrier = 0f;
                        }
                        GlobalEventManager.ServerCharacterExecuted(damageReport, executionHealthLost);
                        if ((object)cachedExplosionEffect != null)
                        {
                            EffectManager.SpawnEffect(cachedExplosionEffect, new EffectData
                            {
                                origin = self.body.corePosition,
                                scale = ExplosionRadius.Value + ((stackCount-1)*BonusExplosionRadiusPerStack.Value)
                            }, transmit: true);
                        }
                        BlastAttack blastAttack = new BlastAttack
                        {
                            attacker = attackerBody.gameObject,
                            baseDamage = self.body.maxHealth * (PercentHealthExplosionDamage.Value + ((stackCount-1)*BonusPercentHealthExplosionDamagePerStack.Value)),
                            baseForce = 0f,
                            bonusForce = Vector3.zero,
                            crit = attackerBody.RollCrit(),
                            //damageType = DamageType.AOE,
                            damageColorIndex = DamageColorIndex.Item,
                            falloffModel = BlastAttack.FalloffModel.None,
                            position = self.body.corePosition,
                            procChainMask = default,
                            procCoefficient = 0f,
                            radius = ExplosionRadius.Value + ((stackCount - 1) * BonusExplosionRadiusPerStack.Value),
                            teamIndex = attackerBody.teamComponent.teamIndex,
                            inflictor = attackerBody.gameObject
                        };
                        //blastAttack.damageType = DamageType.AOE;
                        blastAttack.Fire();
                        GoldOrb goldOrb2 = new GoldOrb();
                        goldOrb2.origin = damageInfo.position;
                        goldOrb2.target = attackerBody.mainHurtBox;
                        goldOrb2.goldAmount = (uint)((float)(BaseGoldOnProc.Value + ((stackCount-1)*BonusGoldPerStack.Value)) * Run.instance.difficultyCoefficient);
                        OrbManager.instance.AddOrb(goldOrb2);
                    }
                }
            }

            orig(self, damageInfo);

        }

        private void CharacterBody_OnInventoryChanged(On.RoR2.CharacterBody.orig_OnInventoryChanged orig, CharacterBody self)
        {
            orig(self);
            int stackCount = GetCount(self);
            if (stackCount >= 1 && !self.HasBuff(cannoneerReady) && !self.HasBuff(cannoneerCooldown)) // Item starts ready
            {
                self.AddBuff(cannoneerReady);
            }
            else if (stackCount < 1) { // No more stacks of the item
                self.RemoveBuff(cannoneerReady);
                while (self.HasBuff(cannoneerCooldown))
                {
                    self.RemoveBuff(cannoneerCooldown);
                }
            }
        }

        protected override void CreateLang()
        {
            LanguageAPI.Add("ITEM_" + ItemLangTokenName + "_NAME", "Cannoneer's Cuirass");
            LanguageAPI.Add("ITEM_" + ItemLangTokenName + "_PICKUP", ItemPickupDesc);
            LanguageAPI.Add("ITEM_" + ItemLangTokenName + "_DESCRIPTION", ItemFullDescription);
            LanguageAPI.Add("ITEM_" + ItemLangTokenName + "_LORE", ItemLore);
        }

        public void CreateBuff() // ToDo: Create unique cooldown icon
        {
            cannoneerCooldown = ScriptableObject.CreateInstance<BuffDef>();
            cannoneerCooldown.canStack = true;
            cannoneerCooldown.isDebuff = false;
            cannoneerCooldown.name = "cannoneerCooldown";
            cannoneerCooldown.isCooldown = true;
            cannoneerCooldown.iconSprite = MainAssets.LoadAsset<Sprite>("Cannoneers Cuirass Icon.png");
            ContentAddition.AddBuffDef(cannoneerCooldown);

            cannoneerReady = ScriptableObject.CreateInstance<BuffDef>();
            cannoneerReady.canStack = false;
            cannoneerReady.isDebuff = false;
            cannoneerReady.name = "cannoneerReady";
            cannoneerReady.isCooldown = false;
            cannoneerReady.iconSprite = MainAssets.LoadAsset<Sprite>("Cannoneers Cuirass Icon.png");
            ContentAddition.AddBuffDef(cannoneerReady);
        }

    }
}
