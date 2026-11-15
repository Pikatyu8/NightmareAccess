using System;
using System.Collections.Generic;
using System.Linq;
using ItemChanger;
using NightmareAccess.Interop;
using NightmareAccess.Logic;
using RandomizerCore.Logic;
using RandomizerMod.Settings;

namespace NightmareAccess.Integrations.BreakableWallRandomizer
{
    internal sealed class BreakableWallRandomizerIntegration : ModIntegrationBase
    {
        public static BreakableWallRandomizerIntegration Instance { get; } = new();

        public const string ModId = "BreakableWallRandomizer";
        public const string ShopLocationName = "Myla_Shop";

        private BreakableWallRandomizerIntegration() { }

        public override string Id => ModId;

        public override void Initialize()
        {
            BreakableWallInGameGating.Hook();
        }

        public override IEnumerable<string> GetAdditionalLogicLocations(LogicManagerBuilder lmb, GenerationSettings gs)
        {
            if (lmb?.LogicLookup == null || !BreakableWallRandomizerInterop.IsEnabled())
            {
                yield break;
            }

            bool rockWalls = BreakableWallRandomizerInterop.IsRockWallsEnabled();
            bool woodenPlanks = BreakableWallRandomizerInterop.IsWoodenPlanksEnabled();
            bool diveFloors = BreakableWallRandomizerInterop.IsDiveFloorsEnabled();
            bool collapsers = BreakableWallRandomizerInterop.IsCollapsersEnabled();
            bool groupWalls = BreakableWallRandomizerInterop.IsGroupWallsEnabled();

            foreach (string key in lmb.LogicLookup.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                if (rockWalls && (key.StartsWith("Wall-", StringComparison.Ordinal)
                                  || (groupWalls && key.StartsWith("Wall_Group-", StringComparison.Ordinal))))
                {
                    yield return key;
                }
                else if (woodenPlanks && (key.StartsWith("Plank-", StringComparison.Ordinal)
                                          || (groupWalls && key.StartsWith("Plank_Group-", StringComparison.Ordinal))))
                {
                    yield return key;
                }
                else if (diveFloors && (key.StartsWith("Dive_Floor-", StringComparison.Ordinal)
                                        || (groupWalls && key.StartsWith("Dive_Group-", StringComparison.Ordinal))))
                {
                    yield return key;
                }
                else if (collapsers && (key.StartsWith("Collapser-", StringComparison.Ordinal)
                                        || (groupWalls && key.StartsWith("Collapser_Group-", StringComparison.Ordinal))))
                {
                    yield return key;
                }
            }
        }

        public override bool IsExcludedLocation(string locationName)
        {
            return locationName == ShopLocationName;
        }

        public override string TryGetItemTypeGroup(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return null;

            if (itemName.StartsWith("Wall-", StringComparison.Ordinal)
                || itemName.StartsWith("Wall_Group-", StringComparison.Ordinal))
            {
                return "Walls";
            }

            if (itemName.StartsWith("Plank-", StringComparison.Ordinal)
                || itemName.StartsWith("Plank_Group-", StringComparison.Ordinal))
            {
                return "Planks";
            }

            if (itemName.StartsWith("Dive_Floor-", StringComparison.Ordinal)
                || itemName.StartsWith("Dive_Group-", StringComparison.Ordinal))
            {
                return "Dive_Floors";
            }

            if (itemName.StartsWith("Collapser-", StringComparison.Ordinal)
                || itemName.StartsWith("Collapser_Group-", StringComparison.Ordinal))
            {
                return "Collapsers";
            }

            return null;
        }

        public override bool IsNonResourceItem(string itemName)
        {
            // Prevents items like Wall-420_Geo_Rock or Dive_Floor-Basin_Grub from matching Geo/Grub substrings
            return TryGetItemTypeGroup(itemName) != null;
        }

        public override bool? CanPlacementAcceptCost(string locationName, AbstractPlacement placement)
        {
            return TryGetItemTypeGroup(locationName) != null ? true : null;
        }

        public override bool TryApplyCost(
            string locationName,
            AbstractPlacement placement,
            NightmareAccessCost cost,
            IDictionary<string, AbstractPlacement> placements)
        {
            if (TryGetItemTypeGroup(locationName) == null) return false;

            if (placement != null && cost != null)
            {
                CostTag tag = placement.GetOrAddTag<CostTag>();
                tag.Cost = CostApplier.CombineCost(tag.Cost, cost);
            }

            return true;
        }
    }
}