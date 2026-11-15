using System;
using System.Collections.Generic;
using System.Linq;
using ItemChanger;
using NightmareAccess.InGame;
using NightmareAccess.Interop;
using NightmareAccess.Logic;
using RandomizerCore.Logic;
using RandomizerMod.Settings;

namespace NightmareAccess.Integrations.GodhomeRandomizer
{
    internal sealed class GodhomeRandomizerIntegration : ModIntegrationBase
    {
        public static GodhomeRandomizerIntegration Instance { get; } = new();

        public const string ModId = "GodhomeRandomizer";
        public const string ShopLocationName = "Godhome_Shop";
        public const string LifebloodName = "Godhome_Lifeblood";
        public const string OrdealName = "Eternal_Ordeal";
        public const string StatueMarkPrefix = "Statue_Mark-";

        private static readonly string[] PantheonPrefixes =
        {
            "Pantheon_Master-",
            "Pantheon_Artist-",
            "Pantheon_Sage-",
            "Pantheon_Knight-",
            "Pantheon_Hallownest-"
        };

        private static readonly string[] PantheonBindingSuffixes =
        {
            "Completion",
            "Nail_Binding",
            "Shell_Binding",
            "Charms_Binding",
            "Soul_Binding",
            "All_Bindings",
            "Hitless"
        };

        private GodhomeRandomizerIntegration() { }

        public override string Id => ModId;

        public override void Initialize()
        {
            GodhomeRandomizerInGameGating.Hook();
        }

        public override IEnumerable<string> GetAdditionalLogicLocations(LogicManagerBuilder lmb, GenerationSettings gs)
        {
            if (lmb?.LogicLookup == null) yield break;

            var cfg = GodhomeRandomizerInterop.GetConfig();
            if (cfg == null || !cfg.Enabled) yield break;

            bool includeUnlock = cfg.IncludeUnlockLocations;
            bool includeBronze = cfg.RandomizeTiers >= 1;
            bool includeSilver = cfg.RandomizeTiers >= 2;
            bool includeGold = cfg.RandomizeTiers >= 3;

            foreach (string key in lmb.LogicLookup.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                if ((includeUnlock && key.StartsWith("Empty_Mark-", StringComparison.Ordinal))
                    || (includeBronze && key.StartsWith("Bronze_Mark-", StringComparison.Ordinal))
                    || (includeSilver && key.StartsWith("Silver_Mark-", StringComparison.Ordinal))
                    || (includeGold && key.StartsWith("Gold_Mark-", StringComparison.Ordinal)))
                {
                    yield return key;
                }
            }

            if (cfg.RandomizeOrdeal && lmb.LogicLookup.ContainsKey(OrdealName))
            {
                yield return OrdealName;
            }

            if (cfg.Lifeblood && lmb.LogicLookup.ContainsKey(LifebloodName))
            {
                yield return LifebloodName;
            }

            if (cfg.PantheonsIncluded > 0)
            {
                bool[] enabledBindings =
                {
                    cfg.Completion,
                    cfg.Nail,
                    cfg.Shell,
                    cfg.Charms,
                    cfg.Soul,
                    cfg.AllAtOnce,
                    cfg.Hitless
                };

                int maxPantheon = Math.Min(cfg.PantheonsIncluded, PantheonPrefixes.Length);
                for (int p = 0; p < maxPantheon; p++)
                {
                    for (int b = 0; b < PantheonBindingSuffixes.Length; b++)
                    {
                        if (!enabledBindings[b]) continue;
                        string locName = PantheonPrefixes[p] + PantheonBindingSuffixes[b];
                        if (lmb.LogicLookup.ContainsKey(locName))
                        {
                            yield return locName;
                        }
                    }
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

            if (itemName.StartsWith(StatueMarkPrefix, StringComparison.Ordinal) || itemName == OrdealName)
            {
                return "Statue_Marks";
            }

            if (IsPantheonMarkLocationOrItem(itemName))
            {
                return "Pantheon_Marks";
            }

            if (itemName == LifebloodName)
            {
                return LifebloodName;
            }

            return null;
        }

        public override bool IsNonResourceItem(string itemName)
        {
            return TryGetItemTypeGroup(itemName) != null;
        }

        public override bool? CanPlacementAcceptCost(string locationName, AbstractPlacement placement)
        {
            return IsGodhomeAutoLocation(locationName) ? true : null;
        }

        public override bool TryApplyCost(
            string locationName,
            AbstractPlacement placement,
            NightmareAccessCost cost,
            IDictionary<string, AbstractPlacement> placements)
        {
            if (!IsGodhomeAutoLocation(locationName)) return false;

            if (placement != null && cost != null)
            {
                CostTag tag = placement.GetOrAddTag<CostTag>();
                tag.Cost = CostApplier.CombineCost(tag.Cost, cost);
            }

            return true;
        }

        public override bool ShouldBlockAutoGive(AbstractPlacement placement, GiveInfo info)
        {
            if (placement == null || string.IsNullOrEmpty(placement.Name)) return false;

            // StatueLocation & BindingLocation call Placement.GiveAll(..., RegainControl) with FlingType.DirectDeposit
            if (info?.FlingType == FlingType.DirectDeposit
                && (IsStatueMarkLocation(placement.Name) || IsPantheonMarkLocationOrItem(placement.Name)))
            {
                return !CornerPromptHelper.CheckOrShowLocked(
                    placement.Name, FsmGateHelper.FormatPlacementName(placement.Name));
            }

            return false;
        }

        private static bool IsGodhomeAutoLocation(string locationName)
        {
            if (string.IsNullOrEmpty(locationName)) return false;

            return locationName == LifebloodName
                   || locationName == OrdealName
                   || IsStatueMarkLocation(locationName)
                   || IsPantheonMarkLocationOrItem(locationName);
        }

        private static bool IsStatueMarkLocation(string name)
        {
            return name.StartsWith("Empty_Mark-", StringComparison.Ordinal)
                   || name.StartsWith("Bronze_Mark-", StringComparison.Ordinal)
                   || name.StartsWith("Silver_Mark-", StringComparison.Ordinal)
                   || name.StartsWith("Gold_Mark-", StringComparison.Ordinal);
        }

        private static bool IsPantheonMarkLocationOrItem(string name)
        {
            foreach (string prefix in PantheonPrefixes)
            {
                if (name.StartsWith(prefix, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}