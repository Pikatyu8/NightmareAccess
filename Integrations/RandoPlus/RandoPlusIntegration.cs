using System;
using System.Collections.Generic;
using System.Linq;
using ItemChanger;
using ItemChanger.Placements;
using NightmareAccess.Interop;
using NightmareAccess.Logic;
using RandomizerCore.Logic;
using RandomizerMod.Settings;

namespace NightmareAccess.Integrations.RandoPlus
{
    internal sealed class RandoPlusIntegration : ModIntegrationBase
    {
        public static RandoPlusIntegration Instance { get; } = new();

        public const string ModId = "RandoPlus";
        public const string MrMushroomPrefix = "Mr_Mushroom-";
        public const string MrMushroomItem = "Mr_Mushroom_Level_Up";
        public const string NailsmithPrefix = "Nailsmith_Upgrade_";
        public const string NailUpgradeItem = "Nail_Upgrade";
        public const string GhostPrefix = "Ghost_Essence-";
        public const string GhostEssenceItem = "Ghost_Essence";
        public const string NoLanternItem = "Not_Lumafly_Lantern";
        public const string NoTearItem = "Not_Ismas_Tear";
        public const string NoSwimItem = "Not_Swim";

        private RandoPlusIntegration() { }

        public override string Id => ModId;

        public override void Initialize()
        {
            RandoPlusInGameGating.Hook();
        }

        public override IEnumerable<string> GetAdditionalLogicLocations(LogicManagerBuilder lmb, GenerationSettings gs)
        {
            if (lmb?.LogicLookup == null) yield break;

            if (RandoPlusInterop.IsMrMushroomEnabled())
            {
                foreach (string key in lmb.LogicLookup.Keys
                             .Where(k => k.StartsWith(MrMushroomPrefix, StringComparison.Ordinal))
                             .OrderBy(k => k, StringComparer.Ordinal))
                {
                    yield return key;
                }
            }

            if (RandoPlusInterop.IsNailUpgradesEnabled())
            {
                for (int i = 1; i <= 4; i++)
                {
                    string key = $"{NailsmithPrefix}{i}";
                    if (lmb.LogicLookup.ContainsKey(key)) yield return key;
                }
            }

            if (RandoPlusInterop.IsGhostEssenceEnabled())
            {
                foreach (string key in lmb.LogicLookup.Keys
                             .Where(k => k.StartsWith(GhostPrefix, StringComparison.Ordinal))
                             .OrderBy(k => k, StringComparer.Ordinal))
                {
                    yield return key;
                }
            }
        }

        public override bool IsExcludedLocation(string locationName)
        {
            if (string.IsNullOrEmpty(locationName)) return false;

            if (RandoPlusInterop.IsExcludedByAreaBlitz(locationName)) return true;

            return false;
        }

        public override bool IsConditionTypeDisallowed(AccessConditionType conditionType, GenerationSettings gs)
        {
            if (conditionType == AccessConditionType.LumaflyLantern && RandoPlusInterop.IsNoLanternEnabled()) return true;
            if (conditionType == AccessConditionType.AcidArmour && RandoPlusInterop.IsNoTearEnabled()) return true;
            return false;
        }

        public override string TryGetItemTypeGroup(string itemName)
        {
            return itemName switch
            {
                NoLanternItem => "Lumafly_Lantern",
                NoTearItem => "Isma's_Tear",
                NoSwimItem => "Swim",
                MrMushroomItem => MrMushroomItem,
                NailUpgradeItem => NailUpgradeItem,
                GhostEssenceItem => "Essence",
                _ => null
            };
        }

        public override AccessConditionType? TryGetItemResourceType(string itemName)
        {
            return itemName == GhostEssenceItem ? AccessConditionType.Essence : null;
        }

        public override bool IsItemSelfConflicting(string itemName, AccessConditionType conditionType, bool isGroupMode)
        {
            if (itemName == NoLanternItem && conditionType == AccessConditionType.LumaflyLantern) return true;
            if (itemName == NoTearItem && conditionType == AccessConditionType.AcidArmour) return true;
            if (itemName == GhostEssenceItem && conditionType is AccessConditionType.Essence or AccessConditionType.Roots) return true;
            return false;
        }

        public override bool? CanPlacementAcceptCost(string locationName, AbstractPlacement placement)
        {
            if (string.IsNullOrEmpty(locationName)) return null;

            if (locationName.StartsWith(NailsmithPrefix, StringComparison.Ordinal)) return true;
            if (locationName.StartsWith(GhostPrefix, StringComparison.Ordinal)) return true;
            return null;
        }

        public override bool TryApplyCost(
            string locationName,
            AbstractPlacement placement,
            NightmareAccessCost cost,
            IDictionary<string, AbstractPlacement> placements)
        {
            if (string.IsNullOrEmpty(locationName)) return false;

            if (locationName.StartsWith(NailsmithPrefix, StringComparison.Ordinal))
            {
                // NailsmithLocation reads AutoPlacement.Cost directly via NailsmithLocation.GetCost()
                if (placement is AutoPlacement ap)
                {
                    ap.Cost = CostApplier.CombineCost(ap.Cost, cost);
                }
                return true;
            }

            if (locationName.StartsWith(GhostPrefix, StringComparison.Ordinal))
            {
                if (placement != null && cost != null)
                {
                    CostTag tag = placement.GetOrAddTag<CostTag>();
                    tag.Cost = CostApplier.CombineCost(tag.Cost, cost);
                }
                return true;
            }

            return false;
        }

        public override bool TryGetNumericAmountOverride(AccessConditionType type, PlayerData pd, out int amount)
        {
            // When RandoPlus replaces vanilla nail upgrades, Pale Ore is never spent at the Nailsmith,
            // while DelayedNailUpgradeItem still increments nailSmithUpgrades.
            if (type == AccessConditionType.PaleOre && pd != null && RandoPlusInterop.IsNailUpgradesActiveInSave())
            {
                amount = pd.ore;
                return true;
            }

            amount = 0;
            return false;
        }
    }
}