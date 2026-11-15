using System;
using System.Collections.Generic;
using ItemChanger;
using NightmareAccess.Interop;
using NightmareAccess.Logic;
using RandomizerCore.Logic;
using RandomizerMod.Settings;

namespace NightmareAccess.Integrations.AccessRandomizer
{
    internal sealed class AccessRandomizerIntegration : ModIntegrationBase
    {
        public static AccessRandomizerIntegration Instance { get; } = new();

        public const string ModId = "AccessRandomizer";
        public const string MantisRespectName = "Mantis_Respect";
        public const string ChainPrefix = "Hollow_Knight_Chain-";
        public const string ChainItemName = "Hollow_Knight_Chain";
        public const string TrapBenchName = "Trap_Bench";
        public const string ShadeGatePrefix = "Shade_Gate-";
        public const string SplitTramPassLocation = "Split_Tram_Pass";
        public const string SplitElevatorPassLocation = "Split_Elevator_Pass";

        private static readonly string[] UniqueKeyNames =
        {
            "Graveyard_Key",
            "Waterways_Key",
            "Pleasure_Key",
            "Coffin_Key"
        };

        private static readonly string[] ShadeGateNames =
        {
            "Shade_Gate-Birthplace_Dead_End",
            "Shade_Gate-Fog_Canyon",
            "Shade_Gate-Markoth",
            "Shade_Gate-Overgrown_Mound",
            "Shade_Gate-Sharp_Shadow",
            "Shade_Gate-Traitor_Lord",
            "Shade_Gate-Void_Heart",
            "Shade_Gate-Void_Tendrils"
        };

        private AccessRandomizerIntegration() { }

        public override string Id => ModId;

        public override void Initialize()
        {
            AccessRandomizerInGameGating.Hook();
        }

        public override IEnumerable<string> GetAdditionalLogicLocations(LogicManagerBuilder lmb, GenerationSettings gs)
        {
            if (lmb?.LogicLookup == null) yield break;

            IEnumerable<string> YieldIfPresent(string name)
            {
                if (lmb.LogicLookup.ContainsKey(name)) yield return name;
            }

            if (AccessRandomizerInterop.IsMantisRespectRandomized())
            {
                foreach (string s in YieldIfPresent(MantisRespectName)) yield return s;
            }

            if (AccessRandomizerInterop.IsHollowKnightChainsRandomized())
            {
                for (int i = 1; i <= 4; i++)
                {
                    foreach (string s in YieldIfPresent($"{ChainPrefix}{i}")) yield return s;
                }
            }

            if (AccessRandomizerInterop.IsMapperKeyRandomized())
            {
                foreach (string s in YieldIfPresent("Mapper_Key")) yield return s;
            }
            if (AccessRandomizerInterop.IsSlyKeyRandomized())
            {
                foreach (string s in YieldIfPresent("Sly_Key")) yield return s;
            }
            if (AccessRandomizerInterop.IsBrettaKeyRandomized())
            {
                foreach (string s in YieldIfPresent("Bretta_Key")) yield return s;
            }
            if (AccessRandomizerInterop.IsZoteKeyRandomized())
            {
                foreach (string s in YieldIfPresent("Zote_Key")) yield return s;
            }
            if (AccessRandomizerInterop.IsRelicKeyRandomized())
            {
                foreach (string s in YieldIfPresent("Relic_Key")) yield return s;
            }
            if (AccessRandomizerInterop.IsBirthplaceKeyRandomized())
            {
                foreach (string s in YieldIfPresent("Birthplace_Key")) yield return s;
            }

            if (AccessRandomizerInterop.IsSplitTramRandomized() && gs?.PoolSettings?.Keys == true)
            {
                foreach (string s in YieldIfPresent(SplitTramPassLocation)) yield return s;
            }

            if (AccessRandomizerInterop.IsSplitElevatorRandomized() && gs?.NoveltySettings?.RandomizeElevatorPass == true)
            {
                foreach (string s in YieldIfPresent(SplitElevatorPassLocation)) yield return s;
            }

            if (AccessRandomizerInterop.IsTrapBenchRandomized())
            {
                foreach (string s in YieldIfPresent(TrapBenchName)) yield return s;
            }

            if (AccessRandomizerInterop.IsShadeGatesRandomized())
            {
                foreach (string gate in ShadeGateNames)
                {
                    foreach (string s in YieldIfPresent(gate)) yield return s;
                }
            }
        }

        public override bool IsConflictingCondition(AccessConditionType conditionType, string locationName)
        {
            if (IsUniqueKeyName(locationName) && conditionType == AccessConditionType.SimpleKeys)
            {
                return true;
            }

            return false;
        }

        public override string TryGetItemTypeGroup(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return null;

            if (IsUniqueKeyName(itemName)) return "SimpleKeys";
            if (itemName is "Upper_Tram_Pass" or "Lower_Tram_Pass") return "Tram_Pass";
            if (itemName is "Left_Elevator_Pass" or "Right_Elevator_Pass") return "Elevator_Pass";
            if (itemName.StartsWith(ShadeGatePrefix, StringComparison.Ordinal)) return "Shade_Gates";

            if (itemName is MantisRespectName or ChainItemName or TrapBenchName
                or "Mapper_Key" or "Sly_Key" or "Bretta_Key" or "Zote_Key"
                or "Relic_Key" or "Glade_Key" or "Birthplace_Key")
            {
                return itemName;
            }

            return null;
        }

        public override AccessConditionType? TryGetItemResourceType(string itemName)
        {
            if (IsUniqueKeyName(itemName)) return AccessConditionType.SimpleKeys;
            return null;
        }

        public override bool? CanPlacementAcceptCost(string locationName, AbstractPlacement placement)
        {
            if (IsInPlaceLocation(locationName)) return true;
            return null;
        }

        public override bool TryApplyCost(
            string locationName,
            AbstractPlacement placement,
            NightmareAccessCost cost,
            IDictionary<string, AbstractPlacement> placements)
        {
            if (!IsInPlaceLocation(locationName)) return false;

            // Keep AutoPlacement intact and attach CostTag so any spawned shiny (e.g. Trap_Bench / RespectLocation)
            // receives CostInfo automatically, while in-place hooks enforce gating on interaction/trigger.
            if (placement != null && cost != null)
            {
                CostTag tag = placement.GetOrAddTag<CostTag>();
                tag.Cost = CostApplier.CombineCost(tag.Cost, cost);
            }

            return true;
        }

        public override bool TryGetNumericAmountOverride(AccessConditionType type, PlayerData pd, out int amount)
        {
            if (type == AccessConditionType.SimpleKeys
                && AccessRandomizerInterop.TryGetObtainedUniqueKeysCount(out amount))
            {
                return true;
            }

            amount = 0;
            return false;
        }

        public override bool TryOverrideLogicNumericAmount(
            AccessConditionType type, ProgressionManager pm, VerificationSimulationContext sim, out int amount)
        {
            amount = 0;
            if (type != AccessConditionType.SimpleKeys || pm?.lm == null) return false;
            if (!HasUniqueKeyTerms(pm.lm)) return false;

            // Count distinct obtained unique keys so duplicate keys in logic cannot inflate SimpleKeys count
            int total = 0;
            foreach (string keyName in UniqueKeyNames)
            {
                Term term = pm.lm.GetTerm(keyName);
                if (term != null && pm.Has(term))
                {
                    total++;
                }
            }

            amount = total;
            return true;
        }

        public override IEnumerable<Term> GetAdditionalNumericTerms(AccessConditionType type, LogicManager lm)
        {
            if (type != AccessConditionType.SimpleKeys || lm == null) yield break;

            foreach (string keyName in UniqueKeyNames)
            {
                Term term = lm.GetTerm(keyName);
                if (term != null) yield return term;
            }
        }

        public static bool IsShadeGateLocation(string locationName) =>
            !string.IsNullOrEmpty(locationName)
            && locationName.StartsWith(ShadeGatePrefix, StringComparison.Ordinal);

        private static bool IsInPlaceLocation(string locationName)
        {
            if (string.IsNullOrEmpty(locationName)) return false;

            return locationName == MantisRespectName
                   || locationName.StartsWith(ChainPrefix, StringComparison.Ordinal)
                   || locationName == TrapBenchName
                   || locationName.StartsWith(ShadeGatePrefix, StringComparison.Ordinal);
        }

        private static bool IsUniqueKeyName(string name)
        {
            return name is "Graveyard_Key" or "Waterways_Key" or "Pleasure_Key" or "Coffin_Key";
        }

        private static bool HasUniqueKeyTerms(LogicManager lm)
        {
            foreach (string keyName in UniqueKeyNames)
            {
                if (lm.GetTerm(keyName) != null) return true;
            }
            return false;
        }
    }
}