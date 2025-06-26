using BepInEx.Configuration;
using R2API;
using RoR2;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AddressableAssets;
using static Smite_Items.Main;

namespace Smite_Items.Items
{
    public class ChronosPendant : ItemBase<ChronosPendant>
    {
        public ConfigEntry<int> ItemCooldown;
        public ConfigEntry<float> CooldownsRemovedPerActivation;
        public override string ItemName => "Chronos Pendant";

        public override string ItemLangTokenName => "CHRONOS_PENDANT";

        public override string ItemPickupDesc => "Periodically lower ability cooldowns.";

        public override string ItemFullDescription => $"Every <style=cIsUtility>{ItemCooldown.Value}</style> seconds, lower all ability cooldowns by <style=cIsUtility>{CooldownsRemovedPerActivation.Value}</style> <style=cStack>(+{CooldownsRemovedPerActivation.Value} per stack)</style> seconds.";

        public override string ItemLore => "Item taken from Smite 2.";

        public override ItemTier Tier => ItemTier.Tier2;

        public override GameObject ItemModel => MainAssets.LoadAsset<GameObject>("ChronosPendantModel.prefab");

        public override Sprite ItemIcon => MainAssets.LoadAsset<Sprite>("Chronos Pendant Icon.png");

        public static BuffDef chronosPendantCooldown;

        //public static float secondsRemovedPerActivation = 1f;

        //public static float chronosPendantCooldownDuration = 10f;

        //public static float secondsRemovedPerActivation => instance.CooldownsRemovedPerActivation.Value;

       // public static float chronosPendantCooldownDuration => instance.ItemCooldown.Value;

        public static GameObject ItemBodyModelPrefab;


        public override void Init(ConfigFile config)
        {
            CreateConfig(config);
            CreateLang();
            CreateItem();
            CreateBuff();
            Hooks();
        }

        public override void CreateConfig(ConfigFile config)
        {
            ItemCooldown = config.Bind<int>("Item " + ItemName, "Item Cooldown", 10, "How many seconds between each item proc?");
            CooldownsRemovedPerActivation = config.Bind<float>("Item " + ItemName, "Ability cooldowns removed per activation", 1, "How many seconds removed from each ability cooldown per item proc?");
        }

        public override void Hooks()
        {
            On.RoR2.CharacterBody.OnInventoryChanged += CharacterBody_OnInventoryChanged;
            //On.RoR2.CharacterBody.FixedUpdate += ChronosCooldown;
        }

        private void CharacterBody_OnInventoryChanged(On.RoR2.CharacterBody.orig_OnInventoryChanged orig, CharacterBody self)
        {
            orig(self);
            //On.RoR2.CharacterBody.FixedUpdate += ChronosCooldown;

            self.AddItemBehavior<ChronosPendantBehavior>(GetCount(self));
        }

        /*private void ChronosCooldown(On.RoR2.CharacterBody.orig_FixedUpdate orig, CharacterBody self)
        {
            orig(self);
            var inventoryCount = GetCount(self);
            if (inventoryCount > 0 && self && !self.HasBuff(chronosPendantCooldown))
            {
                Debug.Log("Gets into buff check");
                if (self.skillLocator)
                {
                    Debug.Log("Adds buff");
                    self.skillLocator.DeductCooldownFromAllSkillsServer(secondsRemovedPerActivation);
                    for (int k = 1; (float)k <= chronosPendantCooldownDuration; k++)
                    {
                        Debug.Log("Adds buff of " + k + " second duration");
                        self.AddTimedBuff(chronosPendantCooldown, chronosPendantCooldownDuration);
                    }
                }
            }
        }*/

        public void CreateBuff()
        {
            chronosPendantCooldown = ScriptableObject.CreateInstance<BuffDef>();
            chronosPendantCooldown.canStack = true;
            chronosPendantCooldown.isDebuff = false;
            chronosPendantCooldown.name = "ChronosPendantCooldown";
            chronosPendantCooldown.isCooldown = true;
            chronosPendantCooldown.iconSprite = MainAssets.LoadAsset<Sprite>("Chronos Pendant Icon.png");
            ContentAddition.AddBuffDef(chronosPendantCooldown);
        }


        public override ItemDisplayRuleDict CreateItemDisplayRules()
        {
            return new ItemDisplayRuleDict();
        }
    }

    public class ChronosPendantBehavior : CharacterBody.ItemBehavior
    {
        private float cooldownTimer;
        

        private void Awake()
        {
            
        }

        private void OnEnable()
        {
            cooldownTimer = 0f;
        }

        private void OnDisable()
        {
            if (body && body.HasBuff(ChronosPendant.chronosPendantCooldown))
            {
                body.ClearTimedBuffs(ChronosPendant.chronosPendantCooldown);
            }
        }

        private void FixedUpdate()
        {
            if (!body || !body.skillLocator)
                return;

            /*if(stack == 0)
            {
                if (body.HasBuff(ChronosPendant.chronosPendantCooldown))
                {
                    body.RemoveBuff(ChronosPendant.chronosPendantCooldown);
                }
            }*/

            int currentStacks = body.GetBuffCount(ChronosPendant.chronosPendantCooldown);

            if (currentStacks > 0)
            {
                return;
            }

            //float reduction = ChronosPendant.secondsRemovedPerActivation * stack;
            float reduction = ChronosPendant.instance.CooldownsRemovedPerActivation.Value * stack;
            // Reduction is currently 1 second per stack, may change to be hyperbolic
            // Hyperbolic idea: Reduction = secondsRemovedPerActivation + (1 - 1/(1 + (stack-1) * coefficient))
            body.skillLocator.DeductCooldownFromAllSkillsServer(reduction);

            //body.SetBuffCount(ChronosPendant.chronosPendantCooldown.buffIndex, (int)ChronosPendant.chronosPendantCooldownDuration);

            //for (int k = 1; (int)k <= ChronosPendant.chronosPendantCooldownDuration; k++)
            for (int k = 1; (int)k <= ChronosPendant.instance.ItemCooldown.Value; k++)
            {
                body.AddTimedBuff(ChronosPendant.chronosPendantCooldown, k);
            }
            /*if (!body.HasBuff(ChronosPendant.chronosPendantCooldown))
            {
                cooldownTimer -= Time.deltaTime;

                if (cooldownTimer <= 0f)
                {
                    cooldownTimer = ChronosPendant.chronosPendantCooldownDuration;

                    float reduction = ChronosPendant.secondsRemovedPerActivation;
                    body.skillLocator.DeductCooldownFromAllSkillsServer(reduction);
                    for (int k = 1; (int)k <= ChronosPendant.chronosPendantCooldownDuration; k++)
                    {
                        Debug.Log("Adds buff of " + k + " second duration");
                        body.AddTimedBuff(ChronosPendant.chronosPendantCooldown, k);
                    }
                }
            }
            else
            {
                cooldownTimer = 0f;
            }*/

        }
    }
}
