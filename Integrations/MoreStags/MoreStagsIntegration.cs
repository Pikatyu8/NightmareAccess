using System;
using System.Collections.Generic;
using System.Linq;
using NightmareAccess.Interop;
using RandomizerCore.Logic;
using RandomizerMod.Settings;

namespace NightmareAccess.Integrations.MoreStags
{
    internal sealed class MoreStagsIntegration : ModIntegrationBase
    {
        public static MoreStagsIntegration Instance { get; } = new();

        public const string ModId = "MoreStags";

        private MoreStagsIntegration() { }

        public override string Id => ModId;

        public override IEnumerable<string> GetAdditionalLogicLocations(LogicManagerBuilder lmb, GenerationSettings gs)
        {
            if (lmb?.LogicLookup == null || gs?.PoolSettings?.Stags != true)
            {
                yield break;
            }

            foreach (string locName in MoreStagsInterop.GetActiveNonVanillaStagLocations()
                         .OrderBy(k => k, StringComparer.Ordinal))
            {
                if (lmb.LogicLookup.ContainsKey(locName))
                {
                    yield return locName;
                }
            }
        }
    }
}
