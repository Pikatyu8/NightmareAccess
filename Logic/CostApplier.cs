using System;
using System.Collections.Generic;
using System.Linq;
using ItemChanger;
using ItemChanger.Locations;
using ItemChanger.Placements;
using ItemChanger.Tags;
using NightmareAccess.Integrations;
using NightmareAccess.Settings;
using RandomizerMod.RC;
using RandomizerMod.Settings;
using Random = System.Random;

namespace NightmareAccess.Logic
{
    /// <summary>Applies and verifies rolled access conditions across all requirement scopes.</summary>
    internal sealed class CostApplier
    {
        private const string WhisperingRootPrefix = "Whispering_Root";

        private readonly NightmareAccessSettings _settings;
        private readonly GenerationSettings _gs;
        private readonly Random _rng;
        private readonly List<AccessConditionType> _allowed;

        private CostApplier(NightmareAccessSettings settings, GenerationSettings gs)
        {
            _settings = settings;
            _gs = gs;
            _rng = new Random((gs?.Seed ?? 1337) + 0x4F54);
            _allowed = NightmareLogicHandler.GetAllowedTypes(settings, gs);
        }

        public static void Apply(RandoController rc = null)
        {
            if (!NightmareAccess.Settings.Enabled) return;

            var placements = ItemChanger.Internal.Ref.Settings?.Placements;
            ApplyToPlacements(
                placements,
                rc?.ctx,
                NightmareAccess.Settings,
                rc?.gs ?? RandomizerMod.RandomizerMod.RS?.GenerationSettings);
        }

        internal static void ApplyToPlacements(
            IDictionary<string, AbstractPlacement> placements,
            RandoModContext ctx,
            NightmareAccessSettings settings,
            GenerationSettings gs)
        {
            if (settings == null || !settings.Enabled) return;

            IReadOnlyDictionary<string, AbstractPlacement> readOnlyPlacements =
                placements as IReadOnlyDictionary<string, AbstractPlacement>
                ?? (placements != null ? new Dictionary<string, AbstractPlacement>(placements) : null);

            Dictionary<string, List<AccessCondition>> multiItemConditions = PrepareAndVerify(
                ctx,
                settings,
                gs,
                readOnlyPlacements);

            if (placements == null) return;

            var snapshot = placements
                .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                .ToList();

            foreach (var kvp in snapshot)
            {
                if (!CanPlacementAcceptCost(kvp.Key, kvp.Value)) continue;
                ApplyPlacementForScope(kvp.Key, kvp.Value, settings.Scope, multiItemConditions, placements);
            }

            ModLog.Info(
                $"Costs applied: Scope={NightmareLogicHandler.ActiveScope}, " +
                $"Loc={NightmareLogicHandler.LocationConditions.Count}, " +
                $"ObjGroup={NightmareLogicHandler.ItemTypeConditions.Count}, " +
                $"Obj={NightmareLogicHandler.ObjectConditions.Count}");
        }

        private static void ApplyPlacementForScope(
            string locName,
            AbstractPlacement placement,
            RequirementScope scope,
            Dictionary<string, List<AccessCondition>> multiItemConditions,
            IDictionary<string, AbstractPlacement> placements)
        {
            switch (scope)
            {
                case RequirementScope.PerLocation:
                    if (NightmareLogicHandler.LocationConditions.TryGetValue(locName, out var locCond) && locCond != null)
                    {
                        ApplyCost(locName, placement, new NightmareAccessCost(locCond), placements);
                    }
                    break;

                case RequirementScope.PerObjectGroup:
                    if (placement is IMultiCostPlacement || IntegrationRegistry.SupportsPerItemCostTags(locName, placement))
                    {
                        foreach (AbstractItem item in placement.Items)
                        {
                            string itemGroupKey = NightmareLogicHandler.GetItemTypeGroup(item.name);
                            if (NightmareLogicHandler.ItemTypeConditions.TryGetValue(itemGroupKey, out var itemGroupCond)
                                && itemGroupCond != null)
                            {
                                var tag = item.GetOrAddTag<CostTag>();
                                tag.Cost = CombineCost(tag.Cost, new NightmareAccessCost(itemGroupCond));
                            }
                        }
                        break;
                    }

                    AbstractItem groupFirst = placement.Items.FirstOrDefault();
                    if (groupFirst == null) break;
                    string groupKey = NightmareLogicHandler.GetItemTypeGroup(groupFirst.name);
                    if (NightmareLogicHandler.ItemTypeConditions.TryGetValue(groupKey, out var groupCond) && groupCond != null)
                    {
                        ApplyCost(locName, placement, new NightmareAccessCost(groupCond), placements);
                    }
                    break;

                case RequirementScope.PerObject:
                    if (multiItemConditions != null && multiItemConditions.TryGetValue(locName, out var itemConds))
                    {
                        ApplyMultiItemConditions(placement, itemConds);
                    }
                    else if (NightmareLogicHandler.ObjectConditions.TryGetValue(locName, out var objCond) && objCond != null)
                    {
                        ApplyCost(locName, placement, new NightmareAccessCost(objCond), placements);
                    }
                    break;
            }
        }

        private static void ApplyMultiItemConditions(AbstractPlacement placement, List<AccessCondition> itemConds)
        {
            int idx = 0;
            foreach (AbstractItem item in placement.Items)
            {
                AccessCondition itemCond = idx < itemConds.Count ? itemConds[idx] : null;
                if (itemCond != null)
                {
                    var tag = item.GetOrAddTag<CostTag>();
                    tag.Cost = CombineCost(tag.Cost, new NightmareAccessCost(itemCond));
                }
                idx++;
            }
        }

        /// <summary>
        /// Rolls PerObjectGroup/PerObject conditions if needed, validates and repairs all scopes against RandomizerCore,
        /// and stores the verification report before ItemChanger placements are mutated.
        /// </summary>
        internal static Dictionary<string, List<AccessCondition>> PrepareAndVerify(
            RandoModContext ctx,
            NightmareAccessSettings settings,
            GenerationSettings gs,
            IReadOnlyDictionary<string, AbstractPlacement> placements = null)
        {
            IntegrationRegistry.RecordSaveState(gs, NightmareLogicHandler.IntegrationInts);

            var applier = new CostApplier(settings, gs);
            Dictionary<string, List<AccessCondition>> multiItemConditions = null;

            Func<string, bool> isEligibleLocation = placements != null
                ? locName => !placements.TryGetValue(locName, out var p) || CanPlacementAcceptCost(locName, p)
                : null;

            if (settings.Scope == RequirementScope.PerLocation)
            {
                PruneLocationConditions(ctx, placements);
            }
            else if (settings.Scope == RequirementScope.PerObjectGroup)
            {
                applier.PrepareObjectGroupConditions(ctx, placements);
            }
            else if (settings.Scope == RequirementScope.PerObject)
            {
                multiItemConditions = applier.PreparePerObjectConditions(ctx, placements);
            }

            NightmareLogicHandler.LastVerificationReport = CostValidator.ValidateAndRepair(
                ctx,
                applier._rng,
                applier._allowed,
                settings,
                gs,
                multiItemConditions,
                isEligibleLocation,
                placements);

            return multiItemConditions;
        }

        private static void PruneLocationConditions(
            RandoModContext ctx, IReadOnlyDictionary<string, AbstractPlacement> placements)
        {
            if (placements != null)
            {
                foreach (string locName in NightmareLogicHandler.LocationConditions.Keys.ToList())
                {
                    if (!placements.TryGetValue(locName, out var p) || !CanPlacementAcceptCost(locName, p))
                    {
                        NightmareLogicHandler.LocationConditions.Remove(locName);
                    }
                }
            }
            else if (ctx != null)
            {
                var activeLocs = new HashSet<string>(ctx.itemPlacements.Select(p => p.Location.Name), StringComparer.Ordinal);
                foreach (string locName in NightmareLogicHandler.LocationConditions.Keys.ToList())
                {
                    if (!activeLocs.Contains(locName))
                    {
                        NightmareLogicHandler.LocationConditions.Remove(locName);
                    }
                }
            }
        }

        private void PrepareObjectGroupConditions(
            RandoModContext ctx, IReadOnlyDictionary<string, AbstractPlacement> placements)
        {
            if (placements != null)
            {
                foreach (var kvp in placements.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    if (!CanPlacementAcceptCost(kvp.Key, kvp.Value)) continue;

                    if (kvp.Value is IMultiCostPlacement
                        || IntegrationRegistry.SupportsPerItemCostTags(kvp.Key, kvp.Value))
                    {
                        foreach (AbstractItem item in kvp.Value.Items)
                        {
                            ResolveObjectGroupCondition(item.name);
                        }
                    }
                    else
                    {
                        AbstractItem firstItem = kvp.Value.Items.FirstOrDefault();
                        if (firstItem != null) ResolveObjectGroupCondition(firstItem.name);
                    }
                }
            }
            else if (ctx != null)
            {
                foreach (ItemPlacement p in ctx.itemPlacements.OrderBy(p => p.Location.Name, StringComparer.Ordinal))
                {
                    if (NightmareLogicHandler.IsExcludedLocation(p.Location.Name) || NightmareLogicHandler.IsShop(p.Location.Name)) continue;
                    ResolveObjectGroupCondition(p.Item.Name);
                }
            }
        }

        private Dictionary<string, List<AccessCondition>> PreparePerObjectConditions(
            RandoModContext ctx, IReadOnlyDictionary<string, AbstractPlacement> placements)
        {
            var multiItemConditions = new Dictionary<string, List<AccessCondition>>(StringComparer.Ordinal);

            if (placements != null)
            {
                foreach (var kvp in placements.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    string locName = kvp.Key;
                    AbstractPlacement placement = kvp.Value;
                    if (!CanPlacementAcceptCost(locName, placement)) continue;

                    AbstractItem firstItem = placement.Items.FirstOrDefault();
                    if (firstItem == null) continue;

                    if (placement is IMultiCostPlacement)
                    {
                        var itemConds = new List<AccessCondition>(placement.Items.Count);
                        foreach (AbstractItem item in placement.Items)
                        {
                            itemConds.Add(RollIndividualObjectCondition(item.name));
                        }
                        multiItemConditions[locName] = itemConds;
                    }
                    else
                    {
                        AccessCondition objCond = RollIndividualObjectCondition(firstItem.name);
                        if (objCond != null)
                        {
                            NightmareLogicHandler.ObjectConditions[locName] = objCond;
                        }
                    }
                }
            }
            else if (ctx != null)
            {
                foreach (ItemPlacement p in ctx.itemPlacements.OrderBy(p => p.Location.Name, StringComparer.Ordinal))
                {
                    string locName = p.Location.Name;
                    if (NightmareLogicHandler.IsExcludedLocation(locName) || NightmareLogicHandler.IsShop(locName)) continue;
                    if (NightmareLogicHandler.ObjectConditions.ContainsKey(locName)) continue;

                    AccessCondition objCond = RollIndividualObjectCondition(p.Item.Name);
                    if (objCond != null)
                    {
                        NightmareLogicHandler.ObjectConditions[locName] = objCond;
                    }
                }
            }

            return multiItemConditions;
        }

        internal static bool CanPlacementAcceptCost(string locName, AbstractPlacement orig)
        {
            if (NightmareLogicHandler.IsExcludedLocation(locName) || NightmareLogicHandler.IsShop(locName)) return false;
            if (orig == null) return false;

            bool? integrationDecision = IntegrationRegistry.CanPlacementAcceptCost(locName, orig);
            if (integrationDecision.HasValue) return integrationDecision.Value;

            if (locName.StartsWith(WhisperingRootPrefix, StringComparison.Ordinal)) return true;

            if (orig is YNShinyPlacement || orig is DualPlacement) return true;

            if (orig is ExistingContainerPlacement ecp)
            {
                return !ecp.Location.nonreplaceable
                       || ecp.Location.HandlesCostBeforeContainer
                       || (Container.GetContainer(ecp.Location.containerType)?.SupportsCost ?? false);
            }

            if (orig is AutoPlacement ap)
            {
                return ap.SupportsCost;
            }

            if (orig is IPrimaryLocationPlacement iplp && iplp.Location is ContainerLocation) return true;
            return orig is ISingleCostPlacement || orig is IMultiCostPlacement;
        }

        internal static Cost CombineCost(Cost existing, NightmareAccessCost added)
        {
            if (added == null) return existing;
            if (existing == null) return added;
            return existing + added;
        }

        private AccessCondition ResolveObjectGroupCondition(string itemName)
        {
            string groupKey = NightmareLogicHandler.GetItemTypeGroup(itemName);
            if (NightmareLogicHandler.ItemTypeConditions.TryGetValue(groupKey, out var cached)) return cached;

            AccessCondition condition = null;
            if (_allowed.Count > 0 && _rng.Next(100) < _settings.RequirementChance)
            {
                condition = NightmareLogicHandler.RollObjectCondition(_rng, _allowed, _settings, _gs, itemName, isGroupMode: true);
            }

            NightmareLogicHandler.ItemTypeConditions[groupKey] = condition;
            return condition;
        }

        private AccessCondition RollIndividualObjectCondition(string itemName)
        {
            if (_allowed.Count == 0 || _rng.Next(100) >= _settings.RequirementChance) return null;
            return NightmareLogicHandler.RollObjectCondition(_rng, _allowed, _settings, _gs, itemName, isGroupMode: false);
        }

        internal static void ApplyCost(
            string locName,
            AbstractPlacement orig,
            NightmareAccessCost cost,
            IDictionary<string, AbstractPlacement> placements)
        {
            try
            {
                if (IntegrationRegistry.TryApplyCost(locName, orig, cost, placements)) return;

                // Whispering roots stay as trees (CostTag only, never spawn a physical shiny)
                if (locName.StartsWith(WhisperingRootPrefix, StringComparison.Ordinal))
                {
                    CostTag tag = orig.GetOrAddTag<CostTag>();
                    tag.Cost = CombineCost(tag.Cost, cost);
                    return;
                }

                if (orig is YNShinyPlacement existing)
                {
                    existing.Cost = CombineCost(existing.Cost, cost);
                }
                else if (orig is DualPlacement dp)
                {
                    dp.Cost = CombineCost(dp.Cost, cost);
                }
                else if (orig is ExistingContainerPlacement ecp)
                {
                    ecp.Cost = CombineCost(ecp.Cost, cost);
                }
                else if (orig is AutoPlacement ap)
                {
                    if (ap.SupportsCost)
                    {
                        ap.Cost = CombineCost(ap.Cost, cost);
                    }
                }
                else if (orig is IPrimaryLocationPlacement iplp && iplp.Location is ContainerLocation cl)
                {
                    Cost existingCost = (orig as ISingleCostPlacement)?.Cost;
                    ReplaceWithYnShiny(locName, orig, CombineCost(existingCost, cost), yn => yn.Location = cl, placements);
                }
                else if (orig is ISingleCostPlacement scp)
                {
                    scp.Cost = CombineCost(scp.Cost, cost);
                }
                else if (orig is IMultiCostPlacement)
                {
                    foreach (AbstractItem item in orig.Items)
                    {
                        CostTag tag = item.GetOrAddTag<CostTag>();
                        tag.Cost = CombineCost(tag.Cost, cost);
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Error($"Failed to apply cost to {locName}", ex);
            }
        }

        internal static void ReplaceWithYnShiny(
            string locName,
            AbstractPlacement orig,
            Cost combinedCost,
            Action<YNShinyPlacement> assignLocation,
            IDictionary<string, AbstractPlacement> placements)
        {
            var yn = new YNShinyPlacement(orig.Name) { Cost = combinedCost };
            assignLocation(yn);

            foreach (Tag tag in orig.GetTags<Tag>())
            {
                if (tag is not CostTag)
                {
                    yn.AddTag(tag);
                }
            }

            foreach (AbstractItem item in orig.Items)
            {
                yn.Add(item);
            }

            placements[locName] = yn;
        }
    }
}
