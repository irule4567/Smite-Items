using BepInEx.Configuration;
using Newtonsoft.Json.Utilities;
using R2API;
using RoR2;
using Smite_Items.Utils;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using static Smite_Items.Main;

namespace Smite_Items.Equipment
{
    public class BlinkingAbyss : EquipmentBase
    {
        public ConfigEntry<float> MaxBlinkDistance;
        public ConfigEntry<float> DamageRadius;
        public ConfigEntry<float> AOEDelay;
        public ConfigEntry<float> CooldownRefund;
        public ConfigEntry<float> Damage;
        public override string EquipmentName => "Blinking Abyss";

        public override string EquipmentLangTokenName => "BLINKING_ABYSS_EQUIP";

        public override string EquipmentPickupDesc => "Teleport forward and damage enemies after a short delay.";

        public override string EquipmentFullDescription => $"<style=cIsUtility>Blink</style> up to <style=cIsUtility>{MaxBlinkDistance.Value}m</style> forward. After {AOEDelay.Value} second, <style=cIsDamage>damage</style> enemies within a <style=cIsDamage>{DamageRadius.Value}m radius</style> for <style=cIsDamage>{Damage.Value*100}% base damage</style>. Each enemy damaged this way reduces your next equipment cooldown by <style=cIsDamage>{CooldownRefund.Value*100}%</style> (multiplicatively).";

        public override string EquipmentLore => "Equipment taken from Smite 2.";

        public override GameObject EquipmentModel => MainAssets.LoadAsset<GameObject>("BlinkingAbyssModel.prefab");

        public override Sprite EquipmentIcon => MainAssets.LoadAsset<Sprite>("Blinking Abyss Icon.png");

        public override float Cooldown => 140;

        private static GameObject cachedExplosionEffect;

        //public static BuffDef blinkDamageIndicator;

        NetworkSoundEventDef blinkSoundEvent;

        public override void Init(ConfigFile config)
        {
            CreateConfig(config);
            CreateLang();
            //CreateBuff();
            CreateEffects();
            CreateSound();
            CreateEquipment();
            Hooks();
        }

        private void CreateSound()
        {
            blinkSoundEvent = ScriptableObject.CreateInstance<NetworkSoundEventDef>();
            blinkSoundEvent.eventName = "Blink_sfx";
            ContentAddition.AddNetworkSoundEventDef(blinkSoundEvent);
        }

        private void CreateEffects()
        {
            var originalPrefab = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/moon2/MoonBatteryDesignPulse.prefab").WaitForCompletion();
            cachedExplosionEffect = PrefabAPI.InstantiateClone(originalPrefab, "BlinkingAbyssExplosion");
            if (!cachedExplosionEffect.GetComponent<EffectComponent>())
            {
                cachedExplosionEffect.AddComponent<EffectComponent>();
            }
            ContentAddition.AddEffect(cachedExplosionEffect);
        }

        protected override void CreateConfig(ConfigFile config)
        {
            MaxBlinkDistance = config.Bind<float>("Equipment: " + EquipmentName, "Maximum distance of blink", 50f, "What is the maximum distance of the blink effect?");
            DamageRadius = config.Bind<float>("Equipment: " + EquipmentName, "Damage radius", 25f, "What is the radius of the post-blink damage effect?");
            AOEDelay = config.Bind<float>("Equipment: " + EquipmentName, "Damage delay", 1f, "How long, in seconds, does it take for the post-blink damage effect to activate after blinking?");
            CooldownRefund = config.Bind<float>("Equipment: " + EquipmentName, "Cooldown refund per enemy", 0.5f, "What percentage of the equipment cooldown is removed per enemy hit by the post-blink damage effect?");
            Damage = config.Bind<float>("Equipment: " + EquipmentName, "Damage", 3f, "What percentage of base damage is dealt by the post-blink damage effect?");
        }

        /*public void CreateBuff()
        {
            blinkDamageIndicator = ScriptableObject.CreateInstance<BuffDef>();
            blinkDamageIndicator.canStack = false;
            blinkDamageIndicator.isDebuff = false;
            blinkDamageIndicator.name = "blinkDamageIndicator";
            blinkDamageIndicator.isCooldown = false;
            blinkDamageIndicator.iconSprite = MainAssets.LoadAsset<Sprite>("Blinking Abyss Icon.png");
            ContentAddition.AddBuffDef(blinkDamageIndicator);
        }*/
        public override ItemDisplayRuleDict CreateItemDisplayRules()
        {
            var mpp = EquipmentModel.AddComponent<ModelPanelParameters>();
            GameObject focusPoint = new GameObject("FocusPoint");
            focusPoint.transform.SetParent(EquipmentModel.transform);
            focusPoint.transform.localPosition = Vector3.zero; // Center of model
            focusPoint.transform.localRotation = Quaternion.identity;

            // Create camera position transform (defines viewing angle)
            GameObject cameraPosition = new GameObject("CameraPosition");
            cameraPosition.transform.SetParent(EquipmentModel.transform);
            cameraPosition.transform.localPosition = new Vector3(1f, 0f, 0f); // Offset from focus point
            cameraPosition.transform.localRotation = Quaternion.identity;
            mpp.focusPointTransform = focusPoint.transform; //EquipmentModel.transform.Find("Target");
            mpp.cameraPositionTransform = cameraPosition.transform; //EquipmentModel.transform.Find("Source");
            mpp.minDistance = 100f;
            mpp.maxDistance = 200f;
            mpp.modelRotation = Quaternion.Euler(new Vector3(0, 90, 0));
            mpp.modelPositionOffset = new Vector3(0, 50, 0);
            return new ItemDisplayRuleDict();
        }

        protected override bool ActivateEquipment(EquipmentSlot slot)
        {
            Ray aimRay = slot.GetAimRay();
            if(Physics.Raycast(aimRay, out var hitInfo, MaxBlinkDistance.Value, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
            {
                TeleportHelper.TeleportBody(slot.characterBody, hitInfo.point + hitInfo.normal, false);
            } //hitInfo.normal added to prevent clipping into geometry
            else
            {
                Vector3 telePosition = aimRay.GetPoint(MaxBlinkDistance.Value);
                TeleportHelper.TeleportBody(slot.characterBody, telePosition, false);
            }
            //Util.PlaySound("Blink_sfx", slot.characterBody.gameObject);
            //AkSoundEngine.PostEvent(401743183, slot.characterBody.gameObject);
            
            if (NetworkServer.active)
            {
                EffectManager.SimpleSoundEffect(
                    blinkSoundEvent.index,
                    slot.characterBody.transform.position,
                    transmit: true
                    );
            }
            //Xoroshiro128Plus rng = new Xoroshiro128Plus(Run.instance.treasureRng.nextUlong);
            //MiscUtils.TeleportBody(slot.characterBody, hitInfo.point, cachedExplosionEffect, HullClassification.Human, rng, 0, 0, false);
            slot.StartCoroutine(DelayedExplosion(slot.characterBody));
            return true;
        }

        private IEnumerator DelayedExplosion(CharacterBody body)
        {
            yield return new WaitForSeconds(AOEDelay.Value);

            if (body)
            {
                if (NetworkServer.active)
                {
                    //GameObject spawnedEffect = GameObject.Instantiate(cachedExplosionEffect, self.body.transform.position, self.body.transform.rotation);
                    EffectManager.SpawnEffect(cachedExplosionEffect, new EffectData
                    {
                        origin = body.corePosition,
                        scale = DamageRadius.Value,
                        rotation = body.transform.rotation
                    }, transmit: true);
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
                    }*/
                    //NetworkServer.Spawn(spawnedEffect);

                    BlastAttack blastAttack = new BlastAttack
                    {
                        attacker = body.gameObject,
                        baseDamage = body.baseDamage * Damage.Value,
                        baseForce = 0f,
                        bonusForce = Vector3.zero,
                        crit = body.RollCrit(),
                        damageColorIndex = DamageColorIndex.Item,
                        falloffModel = BlastAttack.FalloffModel.None,
                        position = body.corePosition,
                        procChainMask = default,
                        procCoefficient = 1f,
                        radius = DamageRadius.Value,
                        teamIndex = TeamComponent.GetObjectTeam(body.gameObject),
                        inflictor = body.gameObject
                    };
                    BlastAttack.Result result = blastAttack.Fire();
                    int enemiesHit = result.hitCount;
                    float cooldownFraction = 1f;
                    for (int i = 0; i < enemiesHit; i++) // Note: Single enemy with multiple hitboxes, like magma worm, would proc on each hitbox. Let's call that intended
                    {
                        cooldownFraction = cooldownFraction * (1-CooldownRefund.Value);
                        body.inventory.DeductActiveEquipmentCooldown(Cooldown*cooldownFraction);
                    }
                }
            }
        }

    }
}
