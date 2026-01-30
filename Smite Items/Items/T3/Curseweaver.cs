using BepInEx.Configuration;
using R2API;
using RoR2;
using System;
using UnityEngine;
using static Smite_Items.Main;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace Smite_Items.Items
{
    public class Curseweaver : ItemBase<Curseweaver>
    {
        public ConfigEntry<int> DebuffDuration;
        public ConfigEntry<int> BonusDebuffDurationPerStack;
        public ConfigEntry<float> PercentMaxHPDamage;
        public ConfigEntry<float> PercentDamageHealed;
        public override string ItemName => "Curseweaver";

        public override string ItemLangTokenName => "CURSEWEAVER_ITEM";

        public override string ItemPickupDesc => "Apply debuffs to enemies that cause damage on attacking.";

        public override string ItemFullDescription => $"Curse enemies on hit for <style=cIsDamage>{DebuffDuration.Value}</style> <style=cStack>(+{BonusDebuffDurationPerStack.Value} per stack)</style> seconds. When a cursed enemy activates a <style=cIsUtility>skill</style>, they take <style=cIsDamage>{PercentMaxHPDamage.Value*100}% of their max health as damage</style> and you are <style=cIsHealing>healed</style> for <style=cIsHealing>{PercentDamageHealed.Value*100}%</style> of the damage dealt.";

        public override string ItemLore => "Item taken from Smite 1.";

        public override ItemTier Tier => ItemTier.Tier3;

        public override ItemTag[] ItemTags => new ItemTag[]
        {
            ItemTag.Damage,
            ItemTag.Healing,
            ItemTag.AIBlacklist
        };
        public override GameObject ItemModel => MainAssets.LoadAsset<GameObject>("CurseweaverModel.prefab");

        public override Sprite ItemIcon => MainAssets.LoadAsset<Sprite>("Curseweaver Icon.png");

        public static BuffDef curse;

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
            curse = ScriptableObject.CreateInstance<BuffDef>();
            curse.canStack = false;
            curse.isDebuff = true;
            curse.name = "curse";
            curse.isCooldown = false;
            curse.iconSprite = MainAssets.LoadAsset<Sprite>("Curseweaver Icon.png");
            ContentAddition.AddBuffDef(curse);
        }

        public override void CreateConfig(ConfigFile config)
        {
            DebuffDuration = config.Bind<int>("Item " + ItemName, "Duration of debuff in seconds", 4, "How long does the Curseweaver debuff last?");
            BonusDebuffDurationPerStack = config.Bind<int>("Item " + ItemName, "Added duration of debuff per item stack", 4, "How much longer does the Curseweaver debuff last per additional item stack?");
            PercentMaxHPDamage = config.Bind<float>("Item " + ItemName, "Percent max hp damage", 0.05f, "What percentage of max health is dealt to the target when a skill is used with the debuff active?");
            PercentDamageHealed = config.Bind<float>("Item " + ItemName, "Percent damage healed", 0.01f, "What percentage of damage dealt by the Curseweaver debuff is healed to the debuff applier?");
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
            On.RoR2.HealthComponent.TakeDamage += ApplyCurse;
            On.RoR2.CharacterBody.OnSkillActivated += ApplyCurseDamage;
        }


        private void ApplyCurseDamage(On.RoR2.CharacterBody.orig_OnSkillActivated orig, CharacterBody self, GenericSkill skill)
        {
            orig(self, skill);
            if (self.HasBuff(curse))
            {
                var tracker = self.GetComponent<CurseTracker>();
                
                var CurseDamage = new RoR2.DamageInfo { };
                CurseDamage.damage = PercentMaxHPDamage.Value * self.maxHealth;
                CurseDamage.damageColorIndex = DamageColorIndex.Item;
                CurseDamage.procCoefficient = 0f;
                CurseDamage.damageType = DamageType.Generic;
                CurseDamage.crit = false;
                if (tracker != null)
                {
                    tracker.DealTrackedDamage(CurseDamage);
                }
                //CurseDamage.inflictor = damageInfo.inflictor;
                //CurseDamage.attacker = damageInfo.attacker;
                //CurseDamage.position = damageInfo.position;
                //self.healthComponent.TakeDamage(CurseDamage);
            }
        }

        private void ApplyCurse(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo damageInfo)
        {
            orig(self, damageInfo);
            if (damageInfo.attacker && damageInfo.attacker.GetComponent<CharacterBody>())
            {
                CharacterBody attackerBody = damageInfo.attacker.GetComponent<CharacterBody>();
                if (attackerBody.inventory)
                {
                    var stackCount = GetCount(attackerBody);
                    if (stackCount > 0)
                    {
                        if (!self.body.HasBuff(curse))
                        {
                            self.body.AddTimedBuff(curse, DebuffDuration.Value + (stackCount-1)*BonusDebuffDurationPerStack.Value);
                            var tracker = self.body.GetComponent<CurseTracker>();
                            if(tracker == null)
                            {
                                tracker = self.body.gameObject.AddComponent<CurseTracker>();
                            }
                            tracker.inflictorBody = damageInfo.inflictor.GetComponent<CharacterBody>();
                            tracker.inflictorGameObject = damageInfo.inflictor;
                            tracker.duration = DebuffDuration.Value + (stackCount - 1) * BonusDebuffDurationPerStack.Value;
                        }
                    }
                }
            }
        }

        public class CurseTracker : MonoBehaviour
        {
            public CharacterBody inflictorBody;
            public GameObject inflictorGameObject;
            public float duration;
            private float timer;
            private CharacterBody victimBody;
            private HealthComponent victimHealth;
            private void Start()
            {
                victimBody = GetComponent<CharacterBody>();
                victimHealth = GetComponent<HealthComponent>();

            }

            private void FixedUpdate()
            {
                timer += Time.fixedDeltaTime;

                if (!victimBody.HasBuff(curse))
                {
                    Destroy(this);
                    return;
                }
            }

            public void DealTrackedDamage(DamageInfo info)
            {
                if (inflictorBody == null || victimBody == null) return;

                info.inflictor = inflictorGameObject;
                info.position = victimBody.corePosition;

                victimBody.healthComponent.TakeDamage(info);

                // Trigger your custom event here
                OnDebuffDamageDealt(info);
            }

            private void OnDebuffDamageDealt(DamageInfo damageInfo)
            {
                // Heal inflictor based on damage dealt
                if (inflictorBody != null && inflictorBody.healthComponent != null)
                {
                    inflictorBody.healthComponent.Heal(damageInfo.damage * Curseweaver.instance.PercentDamageHealed.Value, default);
                }
            }
        }
    }
}
