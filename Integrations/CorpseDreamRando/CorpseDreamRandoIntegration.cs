using System;
using System.Collections.Generic;
using System.Linq;
using ItemChanger;
using NightmareAccess.Interop;
using NightmareAccess.Logic;
using RandomizerCore.Logic;
using RandomizerMod.Settings;

namespace NightmareAccess.Integrations.CorpseDreamRando
{
    internal sealed class CorpseDreamRandoIntegration : ModIntegrationBase
    {
        public static CorpseDreamRandoIntegration Instance { get; } = new();

        public const string ModId = "CorpseDreamRando";
        public const string PlacementPrefix = "Corpse_Dream-";

        private CorpseDreamRandoIntegration() { }

        public override string Id => ModId;

        public override void Initialize()
        {
            CorpseDreamRandoInGameGating.Hook();
        }

        public override IEnumerable<string> GetAdditionalLogicLocations(LogicManagerBuilder lmb, GenerationSettings gs)
        {
            if (lmb?.LogicLookup == null || !CorpseDreamRandoInterop.IsEnabled())
            {
                yield break;
            }

            foreach (string key in lmb.LogicLookup.Keys
                         .Where(k => k.StartsWith(PlacementPrefix, StringComparison.Ordinal))
                         .OrderBy(k => k, StringComparer.Ordinal))
            {
                yield return key;
            }
        }

        public override string TryGetItemTypeGroup(string itemName)
        {
            if (!string.IsNullOrEmpty(itemName) && itemName.StartsWith(PlacementPrefix, StringComparison.Ordinal))
            {
                return "Corpse_Dreams";
            }
            return null;
        }

        public override bool IsNonResourceItem(string itemName)
        {
            return !string.IsNullOrEmpty(itemName) && itemName.StartsWith(PlacementPrefix, StringComparison.Ordinal);
        }

        public override bool? CanPlacementAcceptCost(string locationName, AbstractPlacement placement)
        {
            if (!string.IsNullOrEmpty(locationName) && locationName.StartsWith(PlacementPrefix, StringComparison.Ordinal))
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
            if (string.IsNullOrEmpty(locationName) || !locationName.StartsWith(PlacementPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            if (placement != null && cost != null)
            {
                CostTag tag = placement.GetOrAddTag<CostTag>();
                tag.Cost = CostApplier.CombineCost(tag.Cost, cost);
            }

            return true;
        }
    }
}