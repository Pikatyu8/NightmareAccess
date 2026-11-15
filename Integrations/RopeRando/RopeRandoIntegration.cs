using System.Collections.Generic;
using ItemChanger;
using NightmareAccess.Interop;
using NightmareAccess.Logic;
using RandomizerCore.Logic;
using RandomizerMod.Settings;

namespace NightmareAccess.Integrations.RopeRando
{
    internal sealed class RopeRandoIntegration : ModIntegrationBase
    {
        public static RopeRandoIntegration Instance { get; } = new();

        public const string ModId = "RopeRando";
        public const string ItemName = "Chandelier-Watcher_Knights";
        public const string LocationName = "Chandelier-Watcher_Knights";

        private RopeRandoIntegration() { }

        public override string Id => ModId;

        public override void Initialize()
        {
            RopeRandoInGameGating.Hook();
        }

        public override IEnumerable<string> GetAdditionalLogicLocations(LogicManagerBuilder lmb, GenerationSettings gs)
        {
            if (lmb?.LogicLookup == null || !RopeRandoInterop.IsEnabled())
            {
                yield break;
            }

            if (lmb.LogicLookup.ContainsKey(LocationName))
            {
                yield return LocationName;
            }
        }

        public override string TryGetItemTypeGroup(string itemName)
        {
            return itemName == ItemName ? ItemName : null;
        }

        public override bool IsNonResourceItem(string itemName)
        {
            return itemName == ItemName;
        }

        public override bool? CanPlacementAcceptCost(string locationName, AbstractPlacement placement)
        {
            return locationName == LocationName ? true : null;
        }

        public override bool TryApplyCost(
            string locationName,
            AbstractPlacement placement,
            NightmareAccessCost cost,
            IDictionary<string, AbstractPlacement> placements)
        {
            if (locationName != LocationName) return false;

            if (placement != null && cost != null)
            {
                CostTag tag = placement.GetOrAddTag<CostTag>();
                tag.Cost = CostApplier.CombineCost(tag.Cost, cost);
            }

            return true;
        }
    }
}