using System.Collections.Generic;
using NightmareAccess.Interop;
using NightmareAccess.Logic;
using RandomizerCore.Logic;
using RandomizerMod.Settings;

namespace NightmareAccess.Integrations.MoreLocations
{
    internal sealed class MoreLocationsIntegration : ModIntegrationBase
    {
        public static MoreLocationsIntegration Instance { get; } = new();

        public const string ModId = "MoreLocations";

        private MoreLocationsIntegration() { }

        public override string Id => ModId;

        public override IEnumerable<string> GetAdditionalLogicLocations(LogicManagerBuilder lmb, GenerationSettings gs)
        {
            if (lmb?.LogicLookup == null) yield break;

            foreach (string locName in MoreLocationsInterop.GetEnabledMiscLocations())
            {
                if (lmb.LogicLookup.ContainsKey(locName))
                {
                    yield return locName;
                }
            }
        }

        public override bool IsExcludedLocation(string locationName)
        {
            // Lemm & Junk_Shop are shops; Geo_Chest-Above_Baldur_Shell is a nonreplaceable chest without cost support.
            return locationName is "Lemm" or "Junk_Shop" or "Geo_Chest-Above_Baldur_Shell";
        }

        public override string TryGetItemTypeGroup(string itemName)
        {
            return IsRelicSaleItem(itemName) ? "Geo" : null;
        }

        public override AccessConditionType? TryGetItemResourceType(string itemName)
        {
            // Override NightmareLogicHandler's substring match that would otherwise classify *_Sale geo items as Relics.
            return IsRelicSaleItem(itemName) ? AccessConditionType.Geo : null;
        }

        private static bool IsRelicSaleItem(string itemName) => itemName is
            "Wanderer's_Journal_Sale"
            or "Hallownest_Seal_Sale"
            or "King's_Idol_Sale"
            or "Arcane_Egg_Sale";
    }
}
