using System;
using System.Collections.Generic;
using ItemChanger;
using NightmareAccess.Integrations.AccessRandomizer;
using NightmareAccess.Integrations.AlphabetRando;
using NightmareAccess.Integrations.BenchRando;
using NightmareAccess.Integrations.BreakableWallRandomizer;
using NightmareAccess.Integrations.CombatRandomizer;
using NightmareAccess.Integrations.CorpseDreamRando;
using NightmareAccess.Integrations.CurseRandomizer;
using NightmareAccess.Integrations.DarknessRandomizer;
using NightmareAccess.Integrations.ExtraRando;
using NightmareAccess.Integrations.Fishing;
using NightmareAccess.Integrations.FlowerRandomizer;
using NightmareAccess.Integrations.GodhomeRandomizer;
using NightmareAccess.Integrations.LoreRandomizer;
using NightmareAccess.Integrations.MoreLocations;
using NightmareAccess.Integrations.MoreStags;
using NightmareAccess.Integrations.RandoPlus;
using NightmareAccess.Integrations.RgbRando;
using NightmareAccess.Integrations.RopeRando;
using NightmareAccess.Integrations.TheGloryOfBeingAFoolRandomizer;
using NightmareAccess.Integrations.YARC;
using NightmareAccess.Logic;
using RandomizerCore.Logic;
using RandomizerMod.RC;
using RandomizerMod.Settings;

namespace NightmareAccess.Integrations
{
    internal static class IntegrationRegistry
    {
        private static readonly List<IIntegration> Integrations = new();
        private static bool _builtInRegistered;
        private static bool _initialized;

        public static IReadOnlyList<IIntegration> All
        {
            get
            {
                EnsureBuiltInRegistered();
                return Integrations;
            }
        }

        public static void EnsureBuiltInRegistered()
        {
            if (_builtInRegistered) return;
            _builtInRegistered = true;

            Register(YarcIntegration.Instance);
            Register(AccessRandomizerIntegration.Instance);
            Register(AlphabetRandoIntegration.Instance);
            Register(RgbRandoIntegration.Instance);
            Register(CombatRandomizerIntegration.Instance);
            Register(TheGloryOfBeingAFoolIntegration.Instance);
            Register(FishingIntegration.Instance);
            Register(DarknessRandomizerIntegration.Instance);
            Register(MoreStagsIntegration.Instance);
            Register(BenchRandoIntegration.Instance);
            Register(MoreLocationsIntegration.Instance);
            Register(CurseRandomizerIntegration.Instance);
            Register(RandoPlusIntegration.Instance);
            Register(BreakableWallRandomizerIntegration.Instance);
            Register(ExtraRandoIntegration.Instance);
            Register(FlowerRandomizerIntegration.Instance);
            Register(GodhomeRandomizerIntegration.Instance);
            Register(LoreRandomizerIntegration.Instance);
            Register(RopeRandoIntegration.Instance);
            Register(CorpseDreamRandoIntegration.Instance);
        }

        public static void Register(IIntegration integration)
        {
            if (integration == null) return;
            EnsureBuiltInRegistered();

            for (int i = 0; i < Integrations.Count; i++)
            {
                if (string.Equals(Integrations[i].Id, integration.Id, StringComparison.Ordinal))
                {
                    Integrations[i] = integration;
                    return;
                }
            }

            Integrations.Add(integration);
        }

        public static void Initialize()
        {
            EnsureBuiltInRegistered();
            if (_initialized) return;
            _initialized = true;

            foreach (IIntegration integration in Integrations)
            {
                try
                {
                    integration.Initialize();
                }
                catch (Exception ex)
                {
                    ModLog.Error($"Failed to initialize integration '{integration.Id}'", ex);
                }
            }
        }

        public static bool ProvidesEssenceSource(GenerationSettings gs)
        {
            EnsureBuiltInRegistered();
            foreach (IIntegration integration in Integrations)
            {
                if (integration.ProvidesEssenceSource(gs)) return true;
            }
            return false;
        }

        public static bool ReplacesWhisperingRoots(GenerationSettings gs)
        {
            EnsureBuiltInRegistered();
            foreach (IIntegration integration in Integrations)
            {
                if (integration.ReplacesWhisperingRoots(gs)) return true;
            }
            return false;
        }

        public static void ModifyLogicBeforeRolling(GenerationSettings gs, LogicManagerBuilder lmb)
        {
            EnsureBuiltInRegistered();
            foreach (IIntegration integration in Integrations)
            {
                integration.ModifyLogicBeforeRolling(gs, lmb);
            }
        }

        public static void RecordSaveState(GenerationSettings gs, IDictionary<string, int> saveInts)
        {
            if (saveInts == null) return;
            EnsureBuiltInRegistered();
            foreach (IIntegration integration in Integrations)
            {
                integration.RecordSaveState(gs, saveInts);
            }
        }

        public static IEnumerable<string> GetAdditionalLogicLocations(LogicManagerBuilder lmb, GenerationSettings gs)
        {
            EnsureBuiltInRegistered();
            foreach (IIntegration integration in Integrations)
            {
                foreach (string locName in integration.GetAdditionalLogicLocations(lmb, gs))
                {
                    yield return locName;
                }
            }
        }

        public static bool IsExcludedLocation(string locationName)
        {
            EnsureBuiltInRegistered();
            foreach (IIntegration integration in Integrations)
            {
                if (integration.IsExcludedLocation(locationName)) return true;
            }
            return false;
        }

        public static bool IsConflictingCondition(AccessConditionType conditionType, string locationName)
        {
            EnsureBuiltInRegistered();
            foreach (IIntegration integration in Integrations)
            {
                if (integration.IsConflictingCondition(conditionType, locationName)) return true;
            }
            return false;
        }

        public static bool IsConditionTypeDisallowed(AccessConditionType conditionType, GenerationSettings gs)
        {
            EnsureBuiltInRegistered();
            foreach (IIntegration integration in Integrations)
            {
                if (integration.IsConditionTypeDisallowed(conditionType, gs)) return true;
            }
            return false;
        }

        public static bool IsCloakSplit(GenerationSettings gs)
        {
            EnsureBuiltInRegistered();
            foreach (IIntegration integration in Integrations)
            {
                if (integration.IsCloakSplit(gs)) return true;
            }
            return false;
        }

        public static bool TryOverrideLogicExpression(AccessCondition condition, GenerationSettings gs, out string expr)
        {
            EnsureBuiltInRegistered();
            foreach (IIntegration integration in Integrations)
            {
                if (integration.TryOverrideLogicExpression(condition, gs, out expr)) return true;
            }
            expr = null;
            return false;
        }

        public static string TryGetItemTypeGroup(string itemName)
        {
            EnsureBuiltInRegistered();
            foreach (IIntegration integration in Integrations)
            {
                string group = integration.TryGetItemTypeGroup(itemName);
                if (!string.IsNullOrEmpty(group)) return group;
            }
            return null;
        }

        public static AccessConditionType? TryGetItemResourceType(string itemName)
        {
            EnsureBuiltInRegistered();
            foreach (IIntegration integration in Integrations)
            {
                AccessConditionType? resource = integration.TryGetItemResourceType(itemName);
                if (resource.HasValue) return resource;
            }
            return null;
        }

        public static bool IsNonResourceItem(string itemName)
        {
            EnsureBuiltInRegistered();
            foreach (IIntegration integration in Integrations)
            {
                if (integration.IsNonResourceItem(itemName)) return true;
            }
            return false;
        }

        public static bool IsItemSelfConflicting(string itemName, AccessConditionType conditionType, bool isGroupMode)
        {
            EnsureBuiltInRegistered();
            foreach (IIntegration integration in Integrations)
            {
                if (integration.IsItemSelfConflicting(itemName, conditionType, isGroupMode)) return true;
            }
            return false;
        }

        public static bool? CanPlacementAcceptCost(string locationName, AbstractPlacement placement)
        {
            EnsureBuiltInRegistered();
            foreach (IIntegration integration in Integrations)
            {
                bool? result = integration.CanPlacementAcceptCost(locationName, placement);
                if (result.HasValue) return result.Value;
            }
            return null;
        }

        public static bool SupportsPerItemCostTags(string locationName, AbstractPlacement placement)
        {
            EnsureBuiltInRegistered();
            foreach (IIntegration integration in Integrations)
            {
                if (integration.SupportsPerItemCostTags(locationName, placement)) return true;
            }
            return false;
        }

        public static bool TryApplyCost(
            string locationName,
            AbstractPlacement placement,
            NightmareAccessCost cost,
            IDictionary<string, AbstractPlacement> placements)
        {
            EnsureBuiltInRegistered();
            foreach (IIntegration integration in Integrations)
            {
                if (integration.TryApplyCost(locationName, placement, cost, placements)) return true;
            }
            return false;
        }

        public static void OnSimulationItemAdded(string itemName, VerificationSimulationContext sim)
        {
            EnsureBuiltInRegistered();
            foreach (IIntegration integration in Integrations)
            {
                integration.OnSimulationItemAdded(itemName, sim);
            }
        }

        public static bool HasAdditionalProgressionEffect(RandoModItem item)
        {
            EnsureBuiltInRegistered();
            foreach (IIntegration integration in Integrations)
            {
                if (integration.HasAdditionalProgressionEffect(item)) return true;
            }
            return false;
        }

        public static int GetAdditionalNumericAmount(
            AccessConditionType type, ProgressionManager pm, VerificationSimulationContext sim)
        {
            EnsureBuiltInRegistered();
            int total = 0;
            foreach (IIntegration integration in Integrations)
            {
                total += integration.GetAdditionalNumericAmount(type, pm, sim);
            }
            return total;
        }

        public static bool TryGetNumericAmountOverride(AccessConditionType type, PlayerData pd, out int amount)
        {
            EnsureBuiltInRegistered();
            foreach (IIntegration integration in Integrations)
            {
                if (integration.TryGetNumericAmountOverride(type, pd, out amount)) return true;
            }
            amount = 0;
            return false;
        }

        public static bool TryOverrideLogicNumericAmount(
            AccessConditionType type, ProgressionManager pm, VerificationSimulationContext sim, out int amount)
        {
            EnsureBuiltInRegistered();
            foreach (IIntegration integration in Integrations)
            {
                if (integration.TryOverrideLogicNumericAmount(type, pm, sim, out amount)) return true;
            }
            amount = 0;
            return false;
        }

        public static IEnumerable<Term> GetAdditionalNumericTerms(AccessConditionType type, LogicManager lm)
        {
            EnsureBuiltInRegistered();
            foreach (IIntegration integration in Integrations)
            {
                foreach (Term term in integration.GetAdditionalNumericTerms(type, lm))
                {
                    yield return term;
                }
            }
        }

        public static bool HasExternalEssenceOrbSupport()
        {
            EnsureBuiltInRegistered();
            foreach (IIntegration integration in Integrations)
            {
                if (integration.HasExternalEssenceOrbSupport()) return true;
            }
            return false;
        }

        public static IEnumerable<string> GetDreamOrbPlacementCandidates(DreamPlantOrb orb)
        {
            EnsureBuiltInRegistered();
            foreach (IIntegration integration in Integrations)
            {
                string candidate = integration.TryResolveDreamOrbPlacementName(orb);
                if (!string.IsNullOrEmpty(candidate))
                {
                    yield return candidate;
                }
            }
        }

        public static bool ShouldBlockAutoGive(AbstractPlacement placement, GiveInfo info)
        {
            EnsureBuiltInRegistered();
            foreach (IIntegration integration in Integrations)
            {
                if (integration.ShouldBlockAutoGive(placement, info)) return true;
            }
            return false;
        }

        internal static void ResetForTests()
        {
            Integrations.Clear();
            _builtInRegistered = false;
            _initialized = false;
        }
    }
}