using BepInEx.Configuration;
using R2API;
using RoR2;
using UnityEngine;
using static Smite_Items.Main;

namespace Smite_Items.Items
{
    public class DraconicScale : ItemBase<DraconicScale>
    {
        public override string ItemName => "Draconic Scale";

        public override string ItemLangTokenName => "SCALE_ITEM";

        public override string ItemPickupDesc => "Gain temporary armor after taking damage";

        public override string ItemFullDescription => "Each time you take damage, gain a stack (value) armor for (time) seconds up to a maximum of (max). Stacks decay by 1 instead of being fully removed.";

        public override string ItemLore => "";

        public override ItemTier Tier => ItemTier.Tier1;

        public override GameObject ItemModel => MainAssets.LoadAsset<GameObject>("ExampleItemPrefab.prefab");

        public override Sprite ItemIcon => MainAssets.LoadAsset<Sprite>("ExampleItemIcon.png");

        public override void Init(ConfigFile config)
        {
            CreateConfig(config);
            CreateLang();
            CreateItem();
            Hooks();
        }

        public override void CreateConfig(ConfigFile config)
        {

        }

        public override ItemDisplayRuleDict CreateItemDisplayRules()
        {
            return new ItemDisplayRuleDict();
        }

        public override void Hooks()
        {

        }

    }
}
