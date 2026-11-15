using System;
using System.Collections.Generic;
using System.Linq;
using ItemChanger;
using NightmareAccess.Interop;
using NightmareAccess.Logic;
using RandomizerCore.Logic;
using RandomizerMod.Settings;

namespace NightmareAccess.Integrations.BenchRando
{
    internal sealed class BenchRandoIntegration : ModIntegrationBase
    {
        public static BenchRandoIntegration Instance { get; } = new();

        public const string ModId = "BenchRando";
        public const string BenchPrefix = "Bench-";

        private BenchRandoIntegration() { }

        public override string Id => ModId;

        public override void Initialize()
        {
            BenchRandoInGameGating.Hook();
        }

        public override IEnumerable<string> GetAdditionalLogicLocations(LogicManagerBuilder lmb, GenerationSettings gs)
        {
            if (lmb?.LogicLookup == null) yield break;

            foreach (string bench in BenchRandoInterop.GetActiveRandomizedBenches(gs)
                         .OrderBy(b => b, StringComparer.Ordinal))
            {
                if (lmb.LogicLookup.ContainsKey(bench))
                {
                    yield return bench;
                }
            }
        }

        public override string TryGetItemTypeGroup(string itemName)
        {
            if (!string.IsNullOrEmpty(itemName) && itemName.StartsWith(BenchPrefix, StringComparison.Ordinal))
            {
                return "Benches";
            }
            return null;
        }

        public override bool? CanPlacementAcceptCost(string locationName, AbstractPlacement placement)
        {
            if (!string.IsNullOrEmpty(locationName) && locationName.StartsWith(BenchPrefix, StringComparison.Ordinal))
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
            return !string.IsNullOrEmpty(locationName) && locationName.StartsWith(BenchPrefix, StringComparison.Ordinal);
        }
    }
}
