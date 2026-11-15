using System;
using System.Collections.Generic;
using System.Linq;
using ItemChanger;
using NightmareAccess.Interop;
using NightmareAccess.Logic;
using RandomizerCore.Logic;
using RandomizerMod.Settings;

namespace NightmareAccess.Integrations.Fishing
{
    internal sealed class FishingIntegration : ModIntegrationBase
    {
        public static FishingIntegration Instance { get; } = new();

        public const string ModId = "Fishing";
        public const string LocationPrefix = "Fishing_Spot-";

        private FishingIntegration() { }

        public override string Id => ModId;

        public override IEnumerable<string> GetAdditionalLogicLocations(LogicManagerBuilder lmb, GenerationSettings gs)
        {
            if (lmb?.LogicLookup == null || !FishingInterop.IsEnabled())
            {
                yield break;
            }

            foreach (string key in lmb.LogicLookup.Keys
                         .Where(k => k.StartsWith(LocationPrefix, StringComparison.Ordinal))
                         .OrderBy(k => k, StringComparer.Ordinal))
            {
                yield return key;
            }
        }

        public override bool? CanPlacementAcceptCost(string locationName, AbstractPlacement placement)
        {
            if (!string.IsNullOrEmpty(locationName) && locationName.StartsWith(LocationPrefix, StringComparison.Ordinal))
            {
                return true;
            }
            return null;
        }

        public override bool TryApplyCost(
            string locationName,
            AbstractPlacement placement,
            NightmareAccessCost cost,
            IDictionary<string, AbstractPlacement> placements)
        {
            if (string.IsNullOrEmpty(locationName) || !locationName.StartsWith(LocationPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            // FishingMinigame spawns shinies via ShinyUtility.MakeNewShiny; ShinyFsmPatcher copies this CostTag onto them.
            if (placement != null && cost != null)
            {
                CostTag tag = placement.GetOrAddTag<CostTag>();
                tag.Cost = CostApplier.CombineCost(tag.Cost, cost);
            }

            return true;
        }
    }
}
