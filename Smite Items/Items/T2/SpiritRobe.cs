using BepInEx.Configuration;
using R2API;
using RoR2;
using Smite_Items.Utils;
using System;
using UnityEngine;
using UnityEngine.Networking;
using static Smite_Items.Items.DraconicScale;
using static Smite_Items.Main;

namespace Smite_Items.Items
{
    public class SpiritRobe : ItemBase<SpiritRobe>
    {
        public ConfigEntry<int> BuffArmor;
        public ConfigEntry<int> ExtraArmorPerStack;
        public ConfigEntry<float> PercentMaxHpPerSecond;
        public ConfigEntry<float> BonusPercentMaxHpPerStack;
        public ConfigEntry<int> BuffDuration;
        public override string ItemName => "Spirit Robe";

        public override string ItemLangTokenName => "SPIRIT_ROBE_ITEM";

        public override string ItemPickupDesc => "Gain armor and healing when debuffed";

        public override string ItemFullDescription => $"Upon being inflicted with a <style=cIsDamage>debuff</style>, <style=cIsHealing>increase armor</style> by <style=cIsHealing>{BuffArmor.Value}</style> <style=cStack>(+{ExtraArmorPerStack.Value} per stack)</style> for <style=cIsUtility>{BuffDuration.Value}s</style> and <style=cIsHealing>heal</style> for <style=cIsHealing>{PercentMaxHpPerSecond.Value*100}%</style> <style=cStack>(+{BonusPercentMaxHpPerStack.Value*100}% per stack)</style> of your <style=cIsHealing>health</style> every second <style=cIsUtility>while the buff is active</style>.";

        public override string ItemLore => "Item taken from Smite 2.";

        public override ItemTier Tier => ItemTier.Tier2;

        public override GameObject ItemModel => MainAssets.LoadAsset<GameObject>("SpiritRobeModel.prefab");

        public override Sprite ItemIcon => MainAssets.LoadAsset<Sprite>("Spirit Robe Icon.png");
        public override ItemTag[] ItemTags => new ItemTag[]
        {
            ItemTag.Utility,
            ItemTag.Healing
        };

        public static BuffDef spiritRobeBuff;

        public override void Init(ConfigFile config)
        {
            CreateConfig(config);
            CreateLang();
            CreateBuff();
            CreateItem();
            Hooks();
        }

        private void CreateBuff()
        {
            spiritRobeBuff = ScriptableObject.CreateInstance<BuffDef>();
            spiritRobeBuff.canStack = false;
            spiritRobeBuff.isDebuff = false;
            spiritRobeBuff.name = "spiritRobeBuff";
            spiritRobeBuff.isCooldown = false;
            spiritRobeBuff.iconSprite = MainAssets.LoadAsset<Sprite>("Spirit Robe Icon.png");
            ContentAddition.AddBuffDef(spiritRobeBuff);
        }

        public override void CreateConfig(ConfigFile config)
        {
            BuffArmor = config.Bind<int>("Item " + ItemName, "Armor from buff", 40, "How much armor does the Spirit Robe buff provide?");
            ExtraArmorPerStack = config.Bind<int>("Item " + ItemName, "Additional armor from buff per stack", 40, "How much armor does the Spirit Robe buff provide per additional stack of the item?");
            PercentMaxHpPerSecond = config.Bind<float>("Item " + ItemName, "Percent max hp healed per second from buff", 0.01f, "What percentage of maximum health is restored per second while the Spirit Robe buff is active?");
            BonusPercentMaxHpPerStack = config.Bind<float>("Item " + ItemName, "Percent max hp healed per second from buff per additional stack", 0.01f, "What additional percentage of maximum health is restored per second while the Spirit Robe buff is active per item stack?");
            BuffDuration = config.Bind<int>("Item " + ItemName, "Spirit Robe buff duration", 6, "How many seconds does the Spirit Robe buff last?");
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
            On.RoR2.CharacterBody.AddTimedBuff_BuffDef_float += CheckDebuff;
            RecalculateStatsAPI.GetStatCoefficients += CalculateSpiritRobeBuff;
            On.RoR2.CharacterBody.OnInventoryChanged += CharacterBody_OnInventoryChanged;
        }

        private void CharacterBody_OnInventoryChanged(On.RoR2.CharacterBody.orig_OnInventoryChanged orig, CharacterBody self)
        {
            orig(self);

            self.AddItemBehavior<SpiritRobeHealingBehavior>(GetCount(self));
        }

        private void CheckDebuff(On.RoR2.CharacterBody.orig_AddTimedBuff_BuffDef_float orig, CharacterBody self, BuffDef buffDef, float duration)
        {
            orig(self, buffDef, duration);
            if (buffDef.isDebuff) // If buff is a debuff
            {
                var stackCount = GetCount(self);
                if (stackCount > 0)
                {
                    if (self.HasBuff(spiritRobeBuff))
                    {
                        ItemHelpers.RefreshTimedBuffs(self, spiritRobeBuff, BuffDuration.Value);
                    }
                    else
                    {
                        self.AddTimedBuff(spiritRobeBuff, BuffDuration.Value);
                    }
                }
            }
        }

        private void CalculateSpiritRobeBuff(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (sender && sender.inventory)
            {
                var stackCount = GetCount(sender);
                if (stackCount > 0)
                {
                    bool hasBuff = sender.HasBuff(spiritRobeBuff);
                    if (hasBuff)
                    {
                        args.armorAdd += BuffArmor.Value + ((stackCount - 1) * ExtraArmorPerStack.Value);
                    }
                }
            }
        }

        /*private void CheckDebuff(On.RoR2.CharacterBody.orig_AddBuff_BuffDef orig, CharacterBody self, BuffDef buffDef)
        {
            orig(self, buffDef);
            if (buffDef.isDebuff) // If buff is a debuff
            {
                var stackCount = GetCount(self);
                if (stackCount > 0)
                {
                    if (self.HasBuff(spiritRobeBuff))
                    {
                        ItemHelpers.RefreshTimedBuffs(self, spiritRobeBuff, BuffDuration.Value);
                    }
                    else
                    {
                        self.AddTimedBuff(spiritRobeBuff, BuffDuration.Value);
                    }
                }
            }
        }*/
    }
    public class SpiritRobeHealingBehavior : CharacterBody.ItemBehavior
    {
        private const float healPeriodSeconds = 0.5f;
        private float healTimer;
        //private HealthComponent healthComponent;

        private void OnEnable()
        {
            //Debug.Log("Gets in enable");
            /*if (body)
            {
                Debug.Log("Gets in body enable");
                healthComponent = body.GetComponent<HealthComponent>();
            }*/
            healTimer = 0f;
        }

        /*private void OnDisable()
        {
            healthComponent = null;
        }*/
        private void FixedUpdate()
        {
            if (!body)
            {
                return;
            }
            if(!NetworkServer.active)
            {
                return;
            }
            if(body.HasBuff(SpiritRobe.spiritRobeBuff))
            {
                healTimer += Time.deltaTime;
                while (healTimer > healPeriodSeconds)
                {
                    body.GetComponent<HealthComponent>().HealFraction((SpiritRobe.instance.PercentMaxHpPerSecond.Value + (stack-1)*SpiritRobe.instance.BonusPercentMaxHpPerStack.Value) * healPeriodSeconds, default(ProcChainMask));
                    healTimer -= healPeriodSeconds;
                }
            }
        }
    }
}
