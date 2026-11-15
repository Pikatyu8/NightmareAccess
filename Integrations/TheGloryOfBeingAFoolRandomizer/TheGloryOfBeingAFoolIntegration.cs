using System.Collections.Generic;
using NightmareAccess.Interop;
using RandomizerCore.Logic;
using RandomizerMod.Settings;

namespace NightmareAccess.Integrations.TheGloryOfBeingAFoolRandomizer
{
    internal sealed class TheGloryOfBeingAFoolIntegration : ModIntegrationBase
    {
        public static TheGloryOfBeingAFoolIntegration Instance { get; } = new();

        public const string ModId = "TheGloryOfBeingAFoolRandomizer";
        public const string ItemName = "The_Glory_of_Being_a_Fool";
        public const string LocationName = "The_Glory_of_Being_a_Fool-Colosseum";

        private TheGloryOfBeingAFoolIntegration() { }

        public override string Id => ModId;

        public override IEnumerable<string> GetAdditionalLogicLocations(LogicManagerBuilder lmb, GenerationSettings gs)
        {
            if (lmb?.LogicLookup == null || !TheGloryOfBeingAFoolInterop.IsEnabled())
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
    }
}
