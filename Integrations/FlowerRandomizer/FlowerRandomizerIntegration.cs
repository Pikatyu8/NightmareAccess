using System;
using System.Collections.Generic;
using System.Linq;
using ItemChanger;
using NightmareAccess.Interop;
using NightmareAccess.Logic;
using RandomizerCore.Logic;
using RandomizerMod.Settings;

namespace NightmareAccess.Integrations.FlowerRandomizer
{
    internal sealed class FlowerRandomizerIntegration : ModIntegrationBase
    {
        public static FlowerRandomizerIntegration Instance { get; } = new();

        public const string ModId = "FlowerRandomizer";
        public const string LocationPrefix = "Flower_Quest-";

        private FlowerRandomizerIntegration() { }

        public override string Id => ModId;

        public override void Initialize()
        {
            FlowerRandomizerInGameGating.Hook();
        }

        public override IEnumerable<string> GetAdditionalLogicLocations(LogicManagerBuilder lmb, GenerationSettings gs)
        {
            if (lmb?.LogicLookup == null) yield break;

            foreach (string locName in FlowerRandomizerInterop.GetEnabledQuestLocations()
                         .OrderBy(k => k, StringComparer.Ordinal))
            {
                if (lmb.LogicLookup.ContainsKey(locName))
                {
                    yield return locName;
                }
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

            if (placement != null && cost != null)
            {
                CostTag tag = placement.GetOrAddTag<CostTag>();
                tag.Cost = CostApplier.CombineCost(tag.Cost, cost);
            }

            return true;
        }
    }
}