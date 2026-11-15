using System;
using System.Collections.Generic;
using System.Linq;
using ItemChanger;
using NightmareAccess.Integrations;
using NightmareAccess.Settings;
using RandomizerCore;
using RandomizerCore.Logic;
using RandomizerMod.RC;
using RandomizerMod.Settings;
using Random = System.Random;

namespace NightmareAccess.Logic
{
    public sealed class WorldVerificationReport
    {
        public RequirementScope Scope { get; set; }
        public int BaselineLocationsCount { get; set; }
        public int VerifiedLocationsCount { get; set; }
        public int BaselineWaypointsCount { get; set; }
        public int VerifiedWaypointsCount { get; set; }
        public int BaselineTransitionsCount { get; set; }
        public int VerifiedTransitionsCount { get; set; }
        public int RepairedCount { get; set; }
        public bool HitRepairLimit { get; set; }
        public List<string> UnreachableLocations { get; } = new();
        public List<string> UnreachableWaypoints { get; } = new();
        public List<string> UnreachableTransitions { get; } = new();
        public List<string> ConsistencyIssues { get; } = new();

        public bool IsCompletable =>
            !HitRepairLimit
            && UnreachableLocations.Count == 0
            && UnreachableWaypoints.Count == 0
            && UnreachableTransitions.Count == 0
            && ConsistencyIssues.Count == 0;

        public string FormatSummary()
        {
            string status = IsCompletable ? "PASS" : "FAIL";
            return $"{status} (Scope={Scope}, Locations={VerifiedLocationsCount}/{BaselineLocationsCount}, " +
                   $"Waypoints={VerifiedWaypointsCount}/{BaselineWaypointsCount}, " +
                   $"Transitions={VerifiedTransitionsCount}/{BaselineTransitionsCount}, " +
                   $"Repairs={RepairedCount})";
        }
    }

    /// <summary>
    /// Simulates full seed progression against RandomizerCore's ProgressionManager across all scopes,
    /// repairs any bottlenecks or impossible thresholds, and verifies world completability.
    /// </summary>
    internal static class CostValidator
    {
        private const int MaxRepairIterations = 512;

        public static WorldVerificationReport ValidateAndRepair(
            RandoController rc,
            Random rng,
            List<AccessConditionType> allowed,
            NightmareAccessSettings settings,
            GenerationSettings gs,
            Dictionary<string, List<AccessCondition>> multiItemConditions = null,
            Func<string, bool> isEligibleLocation = null)
        {
            return ValidateAndRepair(
                rc?.ctx,
                rng,
                allowed,
                settings,
                gs,
                multiItemConditions,
                isEligibleLocation);
        }

        internal static WorldVerificationReport ValidateAndRepair(
            RandoModContext ctx,
            Random rng,
            List<AccessConditionType> allowed,
            NightmareAccessSettings settings,
            GenerationSettings gs,
            Dictionary<string, List<AccessCondition>> multiItemConditions = null,
            Func<string, bool> isEligibleLocation = null,
            IReadOnlyDictionary<string, AbstractPlacement> placements = null)
        {
            if (ctx == null || allowed == null)
            {
                return new WorldVerificationReport { Scope = settings?.Scope ?? RequirementScope.PerLocation };
            }

            var terms = new TermCache(ctx.LM);
            ProgressionManager pm = CreateProgressionManager(ctx);
            BaselineSnapshot baseline = MeasureBaseline(pm, ctx);

            int repairedCount = 0;
            bool hitLimit = true;

            for (int iter = 0; iter < MaxRepairIterations; iter++)
            {
                var bindings = BuildBindings(ctx, settings.Scope, multiItemConditions, isEligibleLocation, placements);
                var sim = Simulate(pm, bindings, terms, gs);

                List<PlacementBinding> frontier = new();
                for (int i = 0; i < bindings.Count; i++)
                {
                    if (!baseline.ItemsReachable[i]) continue;
                    PlacementBinding b = bindings[i];
                    if (b.Entry.obtained) continue;
                    if (b.Placement.Location.CanGet(pm))
                    {
                        frontier.Add(b);
                    }
                }

                if (frontier.Count == 0)
                {
                    hitLimit = false;
                    break;
                }

                repairedCount++;

                switch (settings.Scope)
                {
                    case RequirementScope.PerLocation:
                        RepairLocationFrontier(
                            frontier, pm, sim, rng, allowed, settings, gs, terms);
                        break;

                    case RequirementScope.PerObjectGroup:
                        RepairGroupFrontier(
                            frontier, bindings, pm, sim, rng, allowed, settings, gs, terms);
                        break;

                    case RequirementScope.PerObject:
                        RepairObjectFrontier(
                            frontier, pm, sim, rng, allowed, settings, gs, terms, multiItemConditions);
                        break;
                }
            }

            WorldVerificationReport report = VerifyInternal(
                ctx, pm, terms, baseline, settings.Scope, gs, multiItemConditions, isEligibleLocation, placements);
            report.RepairedCount = repairedCount;
            report.HitRepairLimit = hitLimit && !report.IsCompletable;

            if (report.IsCompletable)
            {
                ModLog.Info($"[Verifier] {report.FormatSummary()}");
            }
            else
            {
                ModLog.Warn($"[Verifier] {report.FormatSummary()}");
            }

            return report;
        }

        internal static WorldVerificationReport Verify(
            RandoModContext ctx,
            RequirementScope scope,
            GenerationSettings gs,
            Dictionary<string, List<AccessCondition>> multiItemConditions = null,
            Func<string, bool> isEligibleLocation = null,
            IReadOnlyDictionary<string, AbstractPlacement> placements = null)
        {
            if (ctx == null)
            {
                return new WorldVerificationReport { Scope = scope };
            }

            var terms = new TermCache(ctx.LM);
            ProgressionManager pm = CreateProgressionManager(ctx);
            BaselineSnapshot baseline = MeasureBaseline(pm, ctx);
            return VerifyInternal(ctx, pm, terms, baseline, scope, gs, multiItemConditions, isEligibleLocation, placements);
        }

        private static WorldVerificationReport VerifyInternal(
            RandoModContext ctx,
            ProgressionManager pm,
            TermCache terms,
            BaselineSnapshot baseline,
            RequirementScope scope,
            GenerationSettings gs,
            Dictionary<string, List<AccessCondition>> multiItemConditions,
            Func<string, bool> isEligibleLocation,
            IReadOnlyDictionary<string, AbstractPlacement> placements)
        {
            var bindings = BuildBindings(ctx, scope, multiItemConditions, isEligibleLocation, placements);
            Simulate(pm, bindings, terms, gs);

            var report = new WorldVerificationReport
            {
                Scope = scope,
                BaselineLocationsCount = baseline.ReachableItemCount,
                BaselineWaypointsCount = baseline.ReachableWaypoints.Count,
                BaselineTransitionsCount = baseline.ReachableTransitions.Count
            };

            for (int i = 0; i < bindings.Count; i++)
            {
                if (!baseline.ItemsReachable[i]) continue;

                if (bindings[i].Entry.obtained)
                {
                    report.VerifiedLocationsCount++;
                }
                else
                {
                    report.UnreachableLocations.Add($"{bindings[i].Placement.Location.Name} [{bindings[i].Placement.Item.Name}]");
                }
            }

            foreach (LogicWaypoint lw in ctx.LM.Waypoints)
            {
                if (!baseline.ReachableWaypoints.Contains(lw.Name)) continue;

                if (IsTermReached(pm, lw.term))
                {
                    report.VerifiedWaypointsCount++;
                }
                else
                {
                    report.UnreachableWaypoints.Add(lw.Name);
                }
            }

            foreach (LogicTransition lt in ctx.LM.TransitionLookup.Values)
            {
                if (!baseline.ReachableTransitions.Contains(lt.Name)) continue;

                if (IsTermReached(pm, lt.term))
                {
                    report.VerifiedTransitionsCount++;
                }
                else
                {
                    report.UnreachableTransitions.Add(lt.Name);
                }
            }

            CheckConsistency(bindings, report);
            return report;
        }

        private static void CheckConsistency(List<PlacementBinding> bindings, WorldVerificationReport report)
        {
            foreach (PlacementBinding b in bindings)
            {
                if (b.Condition == null) continue;

                if (!b.IsEligible)
                {
                    report.ConsistencyIssues.Add($"Condition assigned to ineligible location {b.Placement.Location.Name}");
                    continue;
                }

                if ((b.Condition.IsNumeric() || b.Condition.IsProgressive()) && b.Condition.RequiredAmount <= 0)
                {
                    report.ConsistencyIssues.Add($"Non-positive threshold {b.Condition.RequiredAmount} at {b.Placement.Location.Name}");
                }
            }
        }

        private static ProgressionManager CreateProgressionManager(RandoModContext ctx)
        {
            ProgressionManager pm = new(ctx.LM, ctx);
            pm.mu.AddWaypoints(ctx.LM.Waypoints);
            pm.mu.AddTransitions(ctx.LM.TransitionLookup.Values);
            pm.mu.AddPlacements(ctx.Vanilla);
            pm.mu.AddPlacements(ctx.EnumerateExistingPlacements().Skip(ctx.Vanilla.Count + ctx.itemPlacements.Count));
            pm.mu.SetLongTermRevertPoint();
            return pm;
        }

        private static BaselineSnapshot MeasureBaseline(ProgressionManager pm, RandoModContext ctx)
        {
            pm.Reset();
            var entries = new PrePlacedItemUpdateEntry[ctx.itemPlacements.Count];
            for (int i = 0; i < ctx.itemPlacements.Count; i++)
            {
                ItemPlacement p = ctx.itemPlacements[i];
                entries[i] = new PrePlacedItemUpdateEntry(p.Item, p.Location);
                pm.mu.AddEntry(entries[i]);
            }

            pm.mu.StartUpdating();

            bool[] reachable = new bool[entries.Length];
            int count = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].obtained)
                {
                    reachable[i] = true;
                    count++;
                }
            }

            var waypoints = new HashSet<string>(StringComparer.Ordinal);
            foreach (LogicWaypoint lw in ctx.LM.Waypoints)
            {
                if (IsTermReached(pm, lw.term)) waypoints.Add(lw.Name);
            }

            var transitions = new HashSet<string>(StringComparer.Ordinal);
            foreach (LogicTransition lt in ctx.LM.TransitionLookup.Values)
            {
                if (IsTermReached(pm, lt.term)) transitions.Add(lt.Name);
            }

            return new BaselineSnapshot
            {
                ItemsReachable = reachable,
                ReachableItemCount = count,
                ReachableWaypoints = waypoints,
                ReachableTransitions = transitions
            };
        }

        private static bool IsTermReached(ProgressionManager pm, Term term)
        {
            return term.Type == TermType.State
                ? pm.GetState(term.Id) != null
                : pm.Has(term.Id);
        }

        private static List<PlacementBinding> BuildBindings(
            RandoModContext ctx,
            RequirementScope scope,
            Dictionary<string, List<AccessCondition>> multiItemConditions,
            Func<string, bool> isEligibleLocation,
            IReadOnlyDictionary<string, AbstractPlacement> placements)
        {
            var bindings = new List<PlacementBinding>(ctx.itemPlacements.Count);
            var consumedMulti = new Dictionary<string, bool[]>();
            var icPlacements = placements ?? ItemChanger.Internal.Ref.Settings?.Placements;

            foreach (ItemPlacement p in ctx.itemPlacements)
            {
                string locName = p.Location.Name;
                string itemName = p.Item.Name;
                bool isEligible = !NightmareLogicHandler.IsExcludedLocation(locName)
                                  && !NightmareLogicHandler.IsShop(locName)
                                  && (isEligibleLocation == null || isEligibleLocation(locName));

                AccessCondition cond = null;
                int multiIndex = -1;

                if (isEligible)
                {
                    if (scope == RequirementScope.PerLocation)
                    {
                        NightmareLogicHandler.LocationConditions.TryGetValue(locName, out cond);
                    }
                    else if (scope == RequirementScope.PerObjectGroup)
                    {
                        string groupKey = NightmareLogicHandler.GetItemTypeGroup(itemName);
                        NightmareLogicHandler.ItemTypeConditions.TryGetValue(groupKey, out cond);
                    }
                    else if (scope == RequirementScope.PerObject)
                    {
                        if (multiItemConditions != null
                            && multiItemConditions.TryGetValue(locName, out var itemConds)
                            && icPlacements != null
                            && icPlacements.TryGetValue(locName, out AbstractPlacement icPlacement))
                        {
                            if (!consumedMulti.TryGetValue(locName, out bool[] used))
                            {
                                used = new bool[icPlacement.Items.Count];
                                consumedMulti[locName] = used;
                            }

                            multiIndex = MatchMultiItemIndex(icPlacement.Items, used, itemName);
                            if (multiIndex >= 0 && multiIndex < itemConds.Count)
                            {
                                cond = itemConds[multiIndex];
                            }
                        }
                        else
                        {
                            NightmareLogicHandler.ObjectConditions.TryGetValue(locName, out cond);
                        }
                    }
                }

                bindings.Add(new PlacementBinding
                {
                    Placement = p,
                    Condition = cond,
                    IsEligible = isEligible,
                    MultiIndex = multiIndex,
                    GroupKey = scope == RequirementScope.PerObjectGroup
                        ? NightmareLogicHandler.GetItemTypeGroup(itemName)
                        : null
                });
            }

            return bindings;
        }

        private static int MatchMultiItemIndex(IReadOnlyList<AbstractItem> icItems, bool[] used, string randoItemName)
        {
            for (int i = 0; i < icItems.Count; i++)
            {
                if (!used[i] && string.Equals(icItems[i].name, randoItemName, StringComparison.Ordinal))
                {
                    used[i] = true;
                    return i;
                }
            }

            for (int i = 0; i < icItems.Count; i++)
            {
                if (!used[i])
                {
                    used[i] = true;
                    return i;
                }
            }

            return -1;
        }

        private static SimulationResult Simulate(
            ProgressionManager pm,
            List<PlacementBinding> bindings,
            TermCache terms,
            GenerationSettings gs)
        {
            pm.Reset();
            var sim = new SimulationResult();

            pm.AfterAddItem += item => TrackItem(item, sim);
            pm.AfterAddRange += items =>
            {
                foreach (ILogicItem item in items)
                {
                    TrackItem(item, sim);
                }
            };

            foreach (PlacementBinding b in bindings)
            {
                RandoLocation simLoc = b.Placement.Location.Clone();
                if (b.Condition != null)
                {
                    simLoc.AddCost(new NightmareAccessLogicCost(b.Condition, terms, gs, sim));
                }

                b.Entry = new PrePlacedItemUpdateEntry(b.Placement.Item, simLoc);
                pm.mu.AddEntry(b.Entry);
            }

            pm.mu.StartUpdating();

            // Items without logic terms (Journal_Entry-*, or integration items such as YARC Essence_Orb)
            // do not trigger term-indexed MainUpdater notifications, so re-evaluate pending checks until convergence.
            while (sim.NeedsFullRecheck || sim.Integration.NeedsFullRecheck)
            {
                sim.NeedsFullRecheck = false;
                sim.Integration.NeedsFullRecheck = false;
                pm.mu.DoUpdateAll();
            }

            return sim;
        }

        private static void TrackItem(ILogicItem item, SimulationResult sim)
        {
            string name = item?.Name;
            if (string.IsNullOrEmpty(name)) return;

            sim.ObtainedItems.Add(name);

            if (name.StartsWith("Whispering_Root-", StringComparison.Ordinal))
            {
                sim.RootsObtained++;
                sim.NeedsFullRecheck = true;
            }
            else if (name.StartsWith("Journal_Entry-", StringComparison.Ordinal))
            {
                sim.NeedsFullRecheck = true;
            }

            IntegrationRegistry.OnSimulationItemAdded(name, sim.Integration);
        }

        private static void RepairLocationFrontier(
            List<PlacementBinding> frontier,
            ProgressionManager pm,
            SimulationResult sim,
            Random rng,
            List<AccessConditionType> allowed,
            NightmareAccessSettings settings,
            GenerationSettings gs,
            TermCache terms)
        {
            var progressionFrontier = frontier
                .Where(b => b.IsEligible && HasProgressionEffect(b.Placement.Item))
                .OrderBy(b => b.Placement.Location.Name, StringComparer.Ordinal)
                .ToList();

            if (progressionFrontier.Count > 0)
            {
                ApplyLocationRepair(progressionFrontier[0], pm, sim, rng, allowed, settings, gs, terms);
                return;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (PlacementBinding b in frontier
                         .Where(b => b.IsEligible)
                         .OrderBy(b => b.Placement.Location.Name, StringComparer.Ordinal))
            {
                if (seen.Add(b.Placement.Location.Name))
                {
                    ApplyLocationRepair(b, pm, sim, rng, allowed, settings, gs, terms);
                }
            }
        }

        private static void ApplyLocationRepair(
            PlacementBinding binding,
            ProgressionManager pm,
            SimulationResult sim,
            Random rng,
            List<AccessConditionType> allowed,
            NightmareAccessSettings settings,
            GenerationSettings gs,
            TermCache terms)
        {
            string locName = binding.Placement.Location.Name;
            AccessCondition orig = binding.Condition;

            // If the blocking condition is a numeric resource whose threshold is simply too high,
            // try lowering the threshold to a reachable value before replacing the condition type.
            if (orig != null && orig.Type != AccessConditionType.Geo && orig.IsNumeric())
            {
                int avail = GetAvailableNumericAmount(pm, sim, terms, gs, orig.Type);
                (int min, int max) = GetNumericBounds(settings, orig.Type);
                int cap = Math.Min(max, Math.Min(orig.RequiredAmount - 1, avail));
                if (cap >= min)
                {
                    NightmareLogicHandler.LocationConditions[locName] = new AccessCondition(orig.Type, RollRange(rng, min, cap));
                    return;
                }
            }

            AccessCondition replacement = RollSatisfiedCondition(
                pm, sim, rng, allowed, settings, gs, terms,
                new[] { binding.Placement.Item.Name },
                isGroupMode: false,
                locName: locName);

            if (replacement != null)
            {
                NightmareLogicHandler.LocationConditions[locName] = replacement;
            }
            else
            {
                NightmareLogicHandler.LocationConditions.Remove(locName);
            }
        }

        private static void RepairGroupFrontier(
            List<PlacementBinding> frontier,
            List<PlacementBinding> allBindings,
            ProgressionManager pm,
            SimulationResult sim,
            Random rng,
            List<AccessConditionType> allowed,
            NightmareAccessSettings settings,
            GenerationSettings gs,
            TermCache terms)
        {
            var progressionFrontier = frontier
                .Where(b => b.IsEligible && HasProgressionEffect(b.Placement.Item))
                .OrderBy(b => b.GroupKey, StringComparer.Ordinal)
                .ThenBy(b => b.Placement.Location.Name, StringComparer.Ordinal)
                .ToList();

            if (progressionFrontier.Count > 0)
            {
                string targetGroup = progressionFrontier[0].GroupKey;
                List<string> groupItemNames = allBindings
                    .Where(b => b.GroupKey == targetGroup)
                    .Select(b => b.Placement.Item.Name)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                NightmareLogicHandler.ItemTypeConditions[targetGroup] = RollSatisfiedCondition(
                    pm, sim, rng, allowed, settings, gs, terms, groupItemNames, isGroupMode: true);
                return;
            }

            foreach (string groupKey in frontier
                         .Where(b => b.IsEligible)
                         .Select(b => b.GroupKey)
                         .Distinct(StringComparer.Ordinal)
                         .OrderBy(k => k, StringComparer.Ordinal))
            {
                List<string> groupItemNames = allBindings
                    .Where(b => b.GroupKey == groupKey)
                    .Select(b => b.Placement.Item.Name)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                NightmareLogicHandler.ItemTypeConditions[groupKey] = RollSatisfiedCondition(
                    pm, sim, rng, allowed, settings, gs, terms, groupItemNames, isGroupMode: true);
            }
        }

        private static void RepairObjectFrontier(
            List<PlacementBinding> frontier,
            ProgressionManager pm,
            SimulationResult sim,
            Random rng,
            List<AccessConditionType> allowed,
            NightmareAccessSettings settings,
            GenerationSettings gs,
            TermCache terms,
            Dictionary<string, List<AccessCondition>> multiItemConditions)
        {
            var progressionFrontier = frontier
                .Where(b => b.IsEligible && HasProgressionEffect(b.Placement.Item))
                .OrderBy(b => b.Placement.Location.Name, StringComparer.Ordinal)
                .ThenBy(b => b.MultiIndex)
                .ToList();

            if (progressionFrontier.Count > 0)
            {
                ApplyObjectRepair(progressionFrontier[0], pm, sim, rng, allowed, settings, gs, terms, multiItemConditions);
                return;
            }

            foreach (PlacementBinding b in frontier
                         .Where(b => b.IsEligible)
                         .OrderBy(b => b.Placement.Location.Name, StringComparer.Ordinal)
                         .ThenBy(b => b.MultiIndex))
            {
                ApplyObjectRepair(b, pm, sim, rng, allowed, settings, gs, terms, multiItemConditions);
            }
        }

        private static void ApplyObjectRepair(
            PlacementBinding binding,
            ProgressionManager pm,
            SimulationResult sim,
            Random rng,
            List<AccessConditionType> allowed,
            NightmareAccessSettings settings,
            GenerationSettings gs,
            TermCache terms,
            Dictionary<string, List<AccessCondition>> multiItemConditions)
        {
            string locName = binding.Placement.Location.Name;
            AccessCondition replacement = RollSatisfiedCondition(
                pm,
                sim,
                rng,
                allowed,
                settings,
                gs,
                terms,
                new[] { binding.Placement.Item.Name },
                isGroupMode: false,
                locName: locName);

            if (binding.MultiIndex >= 0
                && multiItemConditions != null
                && multiItemConditions.TryGetValue(locName, out var list)
                && binding.MultiIndex < list.Count)
            {
                list[binding.MultiIndex] = replacement;
            }
            else if (replacement != null)
            {
                NightmareLogicHandler.ObjectConditions[locName] = replacement;
            }
            else
            {
                NightmareLogicHandler.ObjectConditions.Remove(locName);
            }
        }

        private static bool HasProgressionEffect(RandoModItem item)
        {
            return item.GetAffectedTerms().Any()
                   || item.Name.StartsWith("Whispering_Root-", StringComparison.Ordinal)
                   || item.Name.StartsWith("Journal_Entry-", StringComparison.Ordinal)
                   || IntegrationRegistry.HasAdditionalProgressionEffect(item);
        }

        private static AccessCondition RollSatisfiedCondition(
            ProgressionManager pm,
            SimulationResult sim,
            Random rng,
            List<AccessConditionType> allowed,
            NightmareAccessSettings s,
            GenerationSettings gs,
            TermCache terms,
            IReadOnlyCollection<string> itemNames,
            bool isGroupMode,
            string locName = null)
        {
            var builders = new List<Func<AccessCondition>>();
            bool noSpellUpgrades = gs?.CursedSettings?.RemoveSpellUpgrades == true;

            foreach (AccessConditionType type in allowed)
            {
                if (!string.IsNullOrEmpty(locName) && NightmareLogicHandler.IsConflictingCondition(type, locName))
                {
                    continue;
                }
                if (itemNames.Any(n => NightmareLogicHandler.IsAlwaysConflictingObjectType(n, type, isGroupMode)))
                {
                    continue;
                }

                if (type == AccessConditionType.Geo)
                {
                    int availGeo = GetAvailableNumericAmount(pm, sim, terms, gs, AccessConditionType.Geo);
                    int sphere0Cap = HasTerm(pm, terms.CanReplenishGeo) && GetVal(pm, terms.Dash) == 0 && GetVal(pm, terms.Claw) == 0
                        ? 300
                        : int.MaxValue;
                    int cap = Math.Min(s.MaxGeo, Math.Min(availGeo, sphere0Cap));
                    if (cap >= s.MinGeo)
                    {
                        builders.Add(() => new AccessCondition(type, RollRange(rng, s.MinGeo, cap)));
                    }
                    continue;
                }

                if (AccessCondition.IsNumericType(type))
                {
                    int avail = GetAvailableNumericAmount(pm, sim, terms, gs, type);
                    (int min, int max) = GetNumericBounds(s, type);
                    int effMax = Math.Min(max, avail);
                    if (effMax >= min)
                    {
                        builders.Add(() => new AccessCondition(type, RollRange(rng, min, effMax)));
                    }
                    continue;
                }

                if (AccessCondition.IsProgressiveType(type))
                {
                    int maxLevel = type switch
                    {
                        AccessConditionType.DreamNailLevel or AccessConditionType.KingsoulLevel => 3,
                        AccessConditionType.GrimmchildLevel => 3,
                        AccessConditionType.FireballLevel or AccessConditionType.QuakeLevel or AccessConditionType.ScreamLevel when noSpellUpgrades => 1,
                        _ => 2
                    };

                    var validLevels = new List<int>();
                    for (int lvl = 1; lvl <= maxLevel; lvl++)
                    {
                        var cond = new AccessCondition(type, lvl);
                        if (!itemNames.Any(n => NightmareLogicHandler.IsSpecificSelfConflict(n, cond))
                            && EvaluateConditionInPm(pm, sim, terms, gs, cond))
                        {
                            validLevels.Add(lvl);
                        }
                    }

                    if (validLevels.Count > 0)
                    {
                        builders.Add(() => new AccessCondition(type, validLevels[rng.Next(validLevels.Count)]));
                    }
                    continue;
                }

                if (type is AccessConditionType.SpecificCharm or AccessConditionType.SpecificMap
                    or AccessConditionType.SpecificStag or AccessConditionType.SpecificJournalEntry
                    or AccessConditionType.NailArt or AccessConditionType.Dreamer)
                {
                    List<AccessCondition> validSpecific = GetSatisfiedSpecificConditions(
                        pm, sim, terms, gs, type, itemNames);
                    if (validSpecific.Count > 0)
                    {
                        builders.Add(() => validSpecific[rng.Next(validSpecific.Count)]);
                    }
                    continue;
                }

                var simpleCond = new AccessCondition(type);
                if (!itemNames.Any(n => NightmareLogicHandler.IsSpecificSelfConflict(n, simpleCond))
                    && EvaluateConditionInPm(pm, sim, terms, gs, simpleCond))
                {
                    builders.Add(() => simpleCond);
                }
            }

            if (builders.Count == 0) return null;
            return builders[rng.Next(builders.Count)]();
        }

        private static List<AccessCondition> GetSatisfiedSpecificConditions(
            ProgressionManager pm,
            SimulationResult sim,
            TermCache terms,
            GenerationSettings gs,
            AccessConditionType type,
            IReadOnlyCollection<string> itemNames)
        {
            var candidates = new List<AccessCondition>();

            switch (type)
            {
                case AccessConditionType.SpecificCharm:
                    for (int id = 1; id <= ConditionCatalog.CharmNames.Length; id++)
                    {
                        candidates.Add(new AccessCondition(type, 1, id));
                    }
                    break;
                case AccessConditionType.SpecificMap:
                    foreach (string key in ConditionCatalog.Maps.Keys)
                    {
                        candidates.Add(new AccessCondition(type, 1, 0, key));
                    }
                    break;
                case AccessConditionType.SpecificStag:
                    foreach (string key in ConditionCatalog.Stags.Keys)
                    {
                        candidates.Add(new AccessCondition(type, 1, 0, key));
                    }
                    break;
                case AccessConditionType.SpecificJournalEntry:
                    var wp = gs?.LongLocationSettings?.WhitePalaceRando;
                    bool excludeSeal = wp is LongLocationSettings.WPSetting.ExcludeWhitePalace or LongLocationSettings.WPSetting.ExcludePathOfPain;
                    foreach (string key in ConditionCatalog.RollableJournalKeys)
                    {
                        if (excludeSeal && key == "SealOfBinding") continue;
                        candidates.Add(new AccessCondition(type, 1, 0, key));
                    }
                    break;
                case AccessConditionType.NailArt:
                    foreach (string key in new[] { "Great", "Dash", "Cyclone" })
                    {
                        candidates.Add(new AccessCondition(type, 1, 0, key));
                    }
                    break;
                case AccessConditionType.Dreamer:
                    foreach (string key in new[] { "Lurien", "Monomon", "Herrah" })
                    {
                        candidates.Add(new AccessCondition(type, 1, 0, key));
                    }
                    break;
            }

            return candidates
                .Where(c => !itemNames.Any(n => NightmareLogicHandler.IsSpecificSelfConflict(n, c))
                            && EvaluateConditionInPm(pm, sim, terms, gs, c))
                .ToList();
        }

        private static (int min, int max) GetNumericBounds(NightmareAccessSettings s, AccessConditionType type) => type switch
        {
            AccessConditionType.Geo => (s.MinGeo, s.MaxGeo),
            AccessConditionType.RancidEggs => (s.MinEggs, s.MaxEggs),
            AccessConditionType.Essence => (s.MinEssence, s.MaxEssence),
            AccessConditionType.Grubs => (s.MinGrubs, s.MaxGrubs),
            AccessConditionType.Roots => (s.MinRoots, s.MaxRoots),
            AccessConditionType.SimpleKeys => (s.MinSimpleKeys, Math.Min(s.MaxSimpleKeys, 3)),
            AccessConditionType.MaskShards => (s.MinMaskShards, s.MaxMaskShards),
            AccessConditionType.VesselFragments => (s.MinVesselFragments, s.MaxVesselFragments),
            AccessConditionType.PaleOre => (s.MinPaleOre, s.MaxPaleOre),
            AccessConditionType.Notches => (s.MinNotches, s.MaxNotches),
            AccessConditionType.Relics => (s.MinRelics, s.MaxRelics),
            AccessConditionType.CharmsCount => (1, 34),
            AccessConditionType.MapsCount => (1, 9),
            AccessConditionType.JournalCount => (1, 1),
            AccessConditionType.StagsCount => (1, 7),
            _ => (1, 1)
        };

        private static int RollRange(Random rng, int min, int max, int cap = int.MaxValue)
        {
            max = Math.Min(max, cap);
            min = Math.Min(min, max);
            return rng.Next(min, max + 1);
        }

        private static int GetAvailableNumericAmount(
            ProgressionManager pm, SimulationResult sim, TermCache terms, GenerationSettings gs, AccessConditionType type)
        {
            int extra = IntegrationRegistry.GetAdditionalNumericAmount(type, pm, sim.Integration);
            if (IntegrationRegistry.TryOverrideLogicNumericAmount(type, pm, sim.Integration, out int overriddenAmount))
            {
                return Math.Max(0, overriddenAmount) + extra;
            }

            int grubTol = gs?.CostSettings?.GrubTolerance ?? 0;
            int eggTol = gs?.CostSettings?.EggTolerance ?? 0;
            int essTol = gs?.CostSettings?.EssenceTolerance ?? 0;
            int charmTol = gs?.CostSettings?.CharmTolerance ?? 0;

            return type switch
            {
                AccessConditionType.Geo => HasTerm(pm, terms.CanReplenishGeo) ? int.MaxValue : Math.Max(0, GetVal(pm, terms.Geo)) + extra,
                AccessConditionType.RancidEggs => Math.Max(0, GetVal(pm, terms.RancidEggs) + eggTol) + extra,
                AccessConditionType.Essence => HasTerm(pm, terms.DreamNail)
                    ? Math.Max(0, GetVal(pm, terms.Essence) + essTol) + extra
                    : 0,
                AccessConditionType.Roots => HasTerm(pm, terms.DreamNail) ? sim.RootsObtained + extra : 0,
                AccessConditionType.Grubs => Math.Max(0, GetVal(pm, terms.Grubs) + grubTol) + extra,
                AccessConditionType.SimpleKeys => Math.Max(0, GetVal(pm, terms.Simple)) + extra,
                // ProgressionInitializer starts MASKSHARDS at 20 - 4*CursedMasks and NOTCHES at 3 - CursedNotches
                AccessConditionType.MaskShards => Math.Max(0, GetVal(pm, terms.MaskShards) - 20) + extra,
                AccessConditionType.VesselFragments => Math.Max(0, GetVal(pm, terms.VesselFragments)) + extra,
                AccessConditionType.PaleOre => Math.Max(0, GetVal(pm, terms.PaleOre)) + extra,
                AccessConditionType.Notches => Math.Max(0, GetVal(pm, terms.Notches) - 3) + extra,
                AccessConditionType.Relics => Math.Max(0,
                    GetVal(pm, terms.Journals) + GetVal(pm, terms.Seals)
                    + GetVal(pm, terms.Idols) + GetVal(pm, terms.ArcaneEggs)) + extra,
                AccessConditionType.CharmsCount => Math.Max(0, GetVal(pm, terms.Charms) + charmTol) + extra,
                AccessConditionType.MapsCount => Math.Max(0, GetVal(pm, terms.Maps)) + extra,
                AccessConditionType.JournalCount => Math.Max(0, GetVal(pm, terms.Journal)) + extra,
                AccessConditionType.StagsCount => Math.Max(0, GetVal(pm, terms.Stags)) + extra,
                _ => extra
            };
        }

        private static bool EvaluateConditionInPm(
            ProgressionManager pm, SimulationResult sim, TermCache terms, GenerationSettings gs, AccessCondition cond)
        {
            if (cond.IsNumeric())
            {
                return GetAvailableNumericAmount(pm, sim, terms, gs, cond.Type) >= cond.RequiredAmount;
            }
            if (cond.Type == AccessConditionType.SpecificJournalEntry)
            {
                return EvaluateSpecificJournalInPm(pm, sim, terms, gs, cond.SpecificKey);
            }

            LogicDef def = terms.GetOrCompileLogic(cond.ToLogicExpression(gs));
            return def != null && def.CanGet(pm);
        }

        private static bool EvaluateSpecificJournalInPm(
            ProgressionManager pm, SimulationResult sim, TermCache terms, GenerationSettings gs, string specificKey)
        {
            if (specificKey == ConditionCatalog.JournalItemKey)
            {
                return HasTerm(pm, terms.Journal);
            }
            if (specificKey == "HuntersMark")
            {
                return false;
            }

            string displayName = ConditionCatalog.Journal.Find(specificKey)?.DisplayName;
            if (string.IsNullOrEmpty(displayName)) return false;

            string itemName = $"Journal_Entry-{displayName.Replace(' ', '_')}";
            return sim.ObtainedItems.Contains(itemName) || gs?.PoolSettings?.JournalEntries == false;
        }

        private static int GetVal(ProgressionManager pm, Term term) => term != null ? pm.Get(term.Id) : 0;

        private static bool HasTerm(ProgressionManager pm, Term term) => term != null && pm.Has(term.Id);

        private sealed class BaselineSnapshot
        {
            public bool[] ItemsReachable;
            public int ReachableItemCount;
            public HashSet<string> ReachableWaypoints;
            public HashSet<string> ReachableTransitions;
        }

        private sealed class SimulationResult
        {
            public int RootsObtained;
            public bool NeedsFullRecheck;
            public readonly HashSet<string> ObtainedItems = new(StringComparer.Ordinal);
            public readonly VerificationSimulationContext Integration = new();
        }

        private sealed class PlacementBinding
        {
            public ItemPlacement Placement;
            public AccessCondition Condition;
            public PrePlacedItemUpdateEntry Entry;
            public bool IsEligible;
            public int MultiIndex;
            public string GroupKey;
        }

        private sealed class TermCache
        {
            public readonly LogicManager Lm;
            private LogicManager _lm => Lm;
            private readonly Dictionary<string, LogicDef> _logicCache = new(StringComparer.Ordinal);

            public readonly Term Dash;
            public readonly Term Claw;
            public readonly Term DreamNail;
            public readonly Term Essence;
            public readonly Term RancidEggs;
            public readonly Term Grubs;
            public readonly Term Simple;
            public readonly Term MaskShards;
            public readonly Term VesselFragments;
            public readonly Term PaleOre;
            public readonly Term Notches;
            public readonly Term Journals;
            public readonly Term Seals;
            public readonly Term Idols;
            public readonly Term ArcaneEggs;
            public readonly Term Charms;
            public readonly Term Maps;
            public readonly Term Journal;
            public readonly Term Stags;
            public readonly Term Geo;
            public readonly Term CanReplenishGeo;

            public TermCache(LogicManager lm)
            {
                Lm = lm;
                Dash = lm.GetTerm("LEFTDASH");
                Claw = lm.GetTerm("LEFTCLAW");
                DreamNail = lm.GetTerm("DREAMNAIL");
                Essence = lm.GetTerm("ESSENCE");
                RancidEggs = lm.GetTerm("RANCIDEGGS");
                Grubs = lm.GetTerm("GRUBS");
                Simple = lm.GetTerm("SIMPLE");
                MaskShards = lm.GetTerm("MASKSHARDS");
                VesselFragments = lm.GetTerm("VESSELFRAGMENTS");
                PaleOre = lm.GetTerm("PALEORE");
                Notches = lm.GetTerm("NOTCHES");
                Journals = lm.GetTerm("WANDERERSJOURNALS");
                Seals = lm.GetTerm("HALLOWNESTSEALS");
                Idols = lm.GetTerm("KINGSIDOLS");
                ArcaneEggs = lm.GetTerm("ARCANEEGGS");
                Charms = lm.GetTerm("CHARMS");
                Maps = lm.GetTerm("MAPS");
                Journal = lm.GetTerm("JOURNAL");
                Stags = lm.GetTerm("STAGS");
                Geo = lm.GetTerm("GEO");
                CanReplenishGeo = lm.GetTerm("Can_Replenish_Geo");
            }

            public LogicDef GetOrCompileLogic(string expr)
            {
                if (string.IsNullOrEmpty(expr)) return null;
                if (!_logicCache.TryGetValue(expr, out LogicDef def))
                {
                    def = _lm.FromString(new RawLogicDef("NA_Cond", expr));
                    _logicCache[expr] = def;
                }
                return def;
            }
        }

        private sealed class NightmareAccessLogicCost : LogicCost
        {
            private readonly AccessCondition _condition;
            private readonly TermCache _terms;
            private readonly GenerationSettings _gs;
            private readonly SimulationResult _sim;
            private readonly LogicDef _logicDef;
            private readonly Term[] _subscribedTerms;

            public NightmareAccessLogicCost(
                AccessCondition condition, TermCache terms, GenerationSettings gs, SimulationResult sim)
            {
                _condition = condition;
                _terms = terms;
                _gs = gs;
                _sim = sim;

                if (condition.Type == AccessConditionType.Geo)
                {
                    _subscribedTerms = new[] { terms.CanReplenishGeo, terms.Geo }
                        .Concat(IntegrationRegistry.GetAdditionalNumericTerms(AccessConditionType.Geo, terms.Lm))
                        .Where(t => t != null)
                        .Distinct()
                        .ToArray();
                }
                else if (condition.IsNumeric())
                {
                    _subscribedTerms = ResolveNumericTerms(condition.Type, terms);
                }
                else if (condition.Type == AccessConditionType.SpecificJournalEntry)
                {
                    _subscribedTerms = terms.Journal != null ? new[] { terms.Journal } : Array.Empty<Term>();
                }
                else
                {
                    _logicDef = terms.GetOrCompileLogic(condition.ToLogicExpression(gs));
                    _subscribedTerms = _logicDef?.GetTerms().Where(t => t != null).Distinct().ToArray()
                                       ?? Array.Empty<Term>();
                }
            }

            public override bool CanGet(ProgressionManager pm)
            {
                return EvaluateConditionInPm(pm, _sim, _terms, _gs, _condition);
            }

            public override IEnumerable<Term> GetTerms() => _subscribedTerms;

            private static Term[] ResolveNumericTerms(AccessConditionType type, TermCache terms)
            {
                Term[] raw = type switch
                {
                    AccessConditionType.RancidEggs => new[] { terms.RancidEggs },
                    AccessConditionType.Essence or AccessConditionType.Roots => new[] { terms.DreamNail, terms.Essence },
                    AccessConditionType.Grubs => new[] { terms.Grubs },
                    AccessConditionType.SimpleKeys => new[] { terms.Simple },
                    AccessConditionType.MaskShards => new[] { terms.MaskShards },
                    AccessConditionType.VesselFragments => new[] { terms.VesselFragments },
                    AccessConditionType.PaleOre => new[] { terms.PaleOre },
                    AccessConditionType.Notches => new[] { terms.Notches },
                    AccessConditionType.Relics => new[] { terms.Journals, terms.Seals, terms.Idols, terms.ArcaneEggs },
                    AccessConditionType.CharmsCount => new[] { terms.Charms },
                    AccessConditionType.MapsCount => new[] { terms.Maps },
                    AccessConditionType.JournalCount => new[] { terms.Journal },
                    AccessConditionType.StagsCount => new[] { terms.Stags },
                    _ => Array.Empty<Term>()
                };

                return raw
                    .Concat(IntegrationRegistry.GetAdditionalNumericTerms(type, terms.Lm))
                    .Where(t => t != null)
                    .Distinct()
                    .ToArray();
            }
        }
    }
}
