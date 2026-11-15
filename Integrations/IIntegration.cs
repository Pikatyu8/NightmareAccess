using System.Collections.Generic;
using ItemChanger;
using NightmareAccess.Logic;
using RandomizerCore.Logic;
using RandomizerMod.RC;
using RandomizerMod.Settings;

namespace NightmareAccess.Integrations
{
    internal sealed class VerificationSimulationContext
    {
        public int ExtraEssence { get; set; }
        public int NotchFragmentsObtained { get; set; }
        public bool NeedsFullRecheck { get; set; }
    }

    internal interface IIntegration
    {
        string Id { get; }

        void Initialize();

        // Logic & Settings
        bool ProvidesEssenceSource(GenerationSettings gs);
        bool ReplacesWhisperingRoots(GenerationSettings gs);
        void ModifyLogicBeforeRolling(GenerationSettings gs, LogicManagerBuilder lmb);
        void RecordSaveState(GenerationSettings gs, IDictionary<string, int> saveInts);
        IEnumerable<string> GetAdditionalLogicLocations(LogicManagerBuilder lmb, GenerationSettings gs);
        bool IsExcludedLocation(string locationName);
        bool IsConflictingCondition(AccessConditionType conditionType, string locationName);
        bool IsConditionTypeDisallowed(AccessConditionType conditionType, GenerationSettings gs);
        bool IsCloakSplit(GenerationSettings gs);
        bool TryOverrideLogicExpression(AccessCondition condition, GenerationSettings gs, out string expr);
        string TryGetItemTypeGroup(string itemName);
        AccessConditionType? TryGetItemResourceType(string itemName);
        bool IsNonResourceItem(string itemName);
        bool IsItemSelfConflicting(string itemName, AccessConditionType conditionType, bool isGroupMode);

        // Placement & Cost Application
        bool? CanPlacementAcceptCost(string locationName, AbstractPlacement placement);
        bool SupportsPerItemCostTags(string locationName, AbstractPlacement placement);
        bool TryApplyCost(
            string locationName,
            AbstractPlacement placement,
            NightmareAccessCost cost,
            IDictionary<string, AbstractPlacement> placements);

        // Verification Simulation
        void OnSimulationItemAdded(string itemName, VerificationSimulationContext sim);
        bool HasAdditionalProgressionEffect(RandoModItem item);
        int GetAdditionalNumericAmount(AccessConditionType type, ProgressionManager pm, VerificationSimulationContext sim);
        bool TryGetNumericAmountOverride(AccessConditionType type, PlayerData pd, out int amount);
        bool TryOverrideLogicNumericAmount(
            AccessConditionType type, ProgressionManager pm, VerificationSimulationContext sim, out int amount);
        IEnumerable<Term> GetAdditionalNumericTerms(AccessConditionType type, LogicManager lm);

        // In-Game Gating
        bool HasExternalEssenceOrbSupport();
        string TryResolveDreamOrbPlacementName(DreamPlantOrb orb);
        bool ShouldBlockAutoGive(AbstractPlacement placement, GiveInfo info);
    }

    internal abstract class ModIntegrationBase : IIntegration
    {
        public abstract string Id { get; }

        public virtual void Initialize() { }

        public virtual bool ProvidesEssenceSource(GenerationSettings gs) => false;
        public virtual bool ReplacesWhisperingRoots(GenerationSettings gs) => false;
        public virtual void ModifyLogicBeforeRolling(GenerationSettings gs, LogicManagerBuilder lmb) { }
        public virtual void RecordSaveState(GenerationSettings gs, IDictionary<string, int> saveInts) { }
        public virtual IEnumerable<string> GetAdditionalLogicLocations(LogicManagerBuilder lmb, GenerationSettings gs)
        {
            yield break;
        }
        public virtual bool IsExcludedLocation(string locationName) => false;
        public virtual bool IsConflictingCondition(AccessConditionType conditionType, string locationName) => false;
        public virtual bool IsConditionTypeDisallowed(AccessConditionType conditionType, GenerationSettings gs) => false;
        public virtual bool IsCloakSplit(GenerationSettings gs) => false;
        public virtual bool TryOverrideLogicExpression(AccessCondition condition, GenerationSettings gs, out string expr)
        {
            expr = null;
            return false;
        }
        public virtual string TryGetItemTypeGroup(string itemName) => null;
        public virtual AccessConditionType? TryGetItemResourceType(string itemName) => null;
        public virtual bool IsNonResourceItem(string itemName) => false;
        public virtual bool IsItemSelfConflicting(string itemName, AccessConditionType conditionType, bool isGroupMode) => false;

        public virtual bool? CanPlacementAcceptCost(string locationName, AbstractPlacement placement) => null;
        public virtual bool SupportsPerItemCostTags(string locationName, AbstractPlacement placement) => false;
        public virtual bool TryApplyCost(
            string locationName,
            AbstractPlacement placement,
            NightmareAccessCost cost,
            IDictionary<string, AbstractPlacement> placements) => false;

        public virtual void OnSimulationItemAdded(string itemName, VerificationSimulationContext sim) { }
        public virtual bool HasAdditionalProgressionEffect(RandoModItem item) => false;
        public virtual int GetAdditionalNumericAmount(
            AccessConditionType type, ProgressionManager pm, VerificationSimulationContext sim) => 0;

        public virtual bool TryGetNumericAmountOverride(AccessConditionType type, PlayerData pd, out int amount)
        {
            amount = 0;
            return false;
        }

        public virtual bool TryOverrideLogicNumericAmount(
            AccessConditionType type, ProgressionManager pm, VerificationSimulationContext sim, out int amount)
        {
            amount = 0;
            return false;
        }

        public virtual IEnumerable<Term> GetAdditionalNumericTerms(AccessConditionType type, LogicManager lm)
        {
            yield break;
        }

        public virtual bool HasExternalEssenceOrbSupport() => false;
        public virtual string TryResolveDreamOrbPlacementName(DreamPlantOrb orb) => null;
        public virtual bool ShouldBlockAutoGive(AbstractPlacement placement, GiveInfo info) => false;
    }
}
