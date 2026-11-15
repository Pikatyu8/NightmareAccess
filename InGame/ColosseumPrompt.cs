using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ItemChanger;
using ItemChanger.Placements;
using ItemChanger.Tags;
using Modding;
using NightmareAccess.Logic;

namespace NightmareAccess.InGame
{
    /// <summary>Appends the Nightmare requirement line to Colosseum trial prompts.</summary>
    internal static class ColosseumPrompt
    {
        internal sealed class TrialInfo
        {
            public int Index;
            public string[] TextMarkers;     // matched against the (localized) prompt text
            public string[] KeyMarkers;      // matched against the language key
            public string[] DirectNames;     // known placement names, tried first
            public string[] PrimaryWords;    // fuzzy placement lookup: any of these must be present...
            public string[] SecondaryWords;  // ...together with any of these or the numeric suffix
            public string Suffix;
            public string CanonicalName;     // fallback key into LocationConditions
        }

        private static readonly string[] PromptTextMarkers = { "begin the Trial", "Place a mark", "Fight for" };
        private static readonly string[] PromptKeyMarkers = { "TRIAL", "BOARD", "COLOSSEUM" };

        // Order matters: the first matching trial wins. Cyrillic markers cover the Russian localization.
        private static readonly TrialInfo[] Trials =
        {
            new TrialInfo
            {
                Index = 1,
                TextMarkers = new[] { "Trial of the Warrior", "Воин" },
                KeyMarkers = new[] { "WARRIOR", "BRONZE" },
                DirectNames = new[] { "Charm_Notch-Colosseum", "Colosseum_Bronze", "Trial_of_the_Warrior", "Colosseum_1" },
                PrimaryWords = new[] { "Colosseum", "Warrior" },
                SecondaryWords = new[] { "Notch", "Bronze" },
                Suffix = "1",
                CanonicalName = "Charm_Notch-Colosseum"
            },
            new TrialInfo
            {
                Index = 2,
                TextMarkers = new[] { "Trial of the Conqueror", "Завоевател" },
                KeyMarkers = new[] { "CONQUEROR", "SILVER" },
                DirectNames = new[] { "Pale_Ore-Colosseum", "Colosseum_Silver", "Trial_of_the_Conqueror", "Colosseum_2" },
                PrimaryWords = new[] { "Colosseum", "Conqueror" },
                SecondaryWords = new[] { "Ore", "Silver" },
                Suffix = "2",
                CanonicalName = "Pale_Ore-Colosseum"
            },
            new TrialInfo
            {
                Index = 3,
                TextMarkers = new[] { "Trial of the Fool", "Глупц" },
                KeyMarkers = new[] { "FOOL", "GOLD" },
                DirectNames = new[] { "Colosseum_Gold", "Trial_of_the_Fool", "Colosseum_3", "The_Glory_of_Being_a_Fool-Colosseum", "The_Glory_of_Being_a_Fool" },
                PrimaryWords = new[] { "Colosseum", "Fool" },
                SecondaryWords = new[] { "Gold", "Glory" },
                Suffix = "3",
                CanonicalName = "Colosseum_Gold"
            }
        };

        public static void Hook()
        {
            ModHooks.LanguageGetHook += OnLanguageGet;
        }

        private static string OnLanguageGet(string key, string sheetTitle, string orig)
        {
            try
            {
                return ProcessPrompt(key, orig);
            }
            catch (Exception ex)
            {
                ModLog.Error("Failed to process Colosseum prompt", ex);
                return orig;
            }
        }

        internal static string ProcessPrompt(string key, string orig)
        {
            if (string.IsNullOrEmpty(orig) || !NightmareLogicHandler.IsSaveActive) return orig;
            if (orig.Contains("Requirement:")) return orig;

            TrialInfo trial = IdentifyTrial(key, orig);
            if (trial == null) return orig;

            ResolveTrial(trial, out NightmareAccessCost cost, out bool obtained);

            string reqLine;
            if (obtained)
            {
                reqLine = "Requirement: Completed (Met)";
            }
            else if (cost?.Condition != null)
            {
                string status = cost.CanPay() ? " (Met)" : "";
                reqLine = $"Requirement: {cost.GetCostText()}{status}";
            }
            else
            {
                reqLine = "Requirement: None (Met)";
            }

            ModLog.Debug($"[Colosseum] Trial {trial.Index} prompt: {reqLine}");

            int markIdx = orig.IndexOf("Place a mark", StringComparison.OrdinalIgnoreCase);
            if (markIdx >= 0) return orig.Insert(markIdx, $"{reqLine}\n");

            int newlineIdx = orig.IndexOf('\n');
            if (newlineIdx >= 0) return orig.Insert(newlineIdx + 1, $"{reqLine}\n");

            return $"{orig}\n{reqLine}";
        }

        internal static TrialInfo IdentifyTrial(string key, string text)
        {
            key ??= "";

            bool isTrialPrompt = ContainsAny(text, PromptTextMarkers) || ContainsAny(key, PromptKeyMarkers);
            if (!isTrialPrompt) return null;

            foreach (TrialInfo trial in Trials)
            {
                if (ContainsAny(text, trial.TextMarkers) || ContainsAny(key, trial.KeyMarkers)) return trial;
            }
            return null;
        }

        private static void ResolveTrial(TrialInfo trial, out NightmareAccessCost cost, out bool obtained)
        {
            cost = null;
            obtained = false;

            AbstractPlacement placement = FindPlacement(trial);
            if (placement != null && placement.Items.Count > 0 && placement.AllObtained())
            {
                obtained = true;
                return;
            }

            cost = ExtractCost(placement, trial);
        }

        internal static IReadOnlyDictionary<string, AbstractPlacement> PlacementsOverrideForTests { get; set; }

        private static IReadOnlyDictionary<string, AbstractPlacement> GetPlacements()
        {
            if (PlacementsOverrideForTests != null) return PlacementsOverrideForTests;
            if (NightmareAccess.Instance == null) return null;
            return GetRefPlacements();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IReadOnlyDictionary<string, AbstractPlacement> GetRefPlacements() =>
            ItemChanger.Internal.Ref.Settings?.Placements;

        private static AbstractPlacement FindPlacement(TrialInfo trial)
        {
            var placements = GetPlacements();
            if (placements == null) return null;

            foreach (string name in trial.DirectNames)
            {
                if (placements.TryGetValue(name, out var placement)) return placement;
            }

            foreach (var kvp in placements)
            {
                string key = kvp.Key;
                if (ContainsAny(key, trial.PrimaryWords)
                    && (ContainsAny(key, trial.SecondaryWords) || key.EndsWith(trial.Suffix, StringComparison.Ordinal)))
                {
                    return kvp.Value;
                }
            }
            return null;
        }

        private static NightmareAccessCost ExtractCost(AbstractPlacement placement, TrialInfo trial)
        {
            if (placement != null && ExtractCostFromPlacement(placement) is { } fromPlacement)
            {
                return fromPlacement;
            }

            return ExtractCostFromConditions(trial);
        }

        private static NightmareAccessCost ExtractCostFromPlacement(AbstractPlacement placement)
        {
            if (placement is YNShinyPlacement yn && NightmareAccessCost.Find(yn.Cost) is { } ynCost) return ynCost;
            if (placement is ISingleCostPlacement scp && NightmareAccessCost.Find(scp.Cost) is { } scpCost) return scpCost;

            if (CostFromTags(placement) is { } placementTagCost) return placementTagCost;

            foreach (AbstractItem item in placement.Items)
            {
                if (CostFromTags(item) is { } itemTagCost) return itemTagCost;
            }

            AbstractItem firstItem = placement.Items.FirstOrDefault();
            if (firstItem != null)
            {
                string groupKey = NightmareLogicHandler.GetItemTypeGroup(firstItem.name);
                if (NightmareLogicHandler.ItemTypeConditions.TryGetValue(groupKey, out var groupCond) && groupCond != null)
                {
                    return new NightmareAccessCost(groupCond);
                }
            }

            return LookupConditionCost(placement.Name);
        }

        private static NightmareAccessCost ExtractCostFromConditions(TrialInfo trial)
        {
            foreach (string name in trial.DirectNames)
            {
                if (LookupConditionCost(name) is { } directCost) return directCost;
            }

            return NightmareLogicHandler.LocationConditions.TryGetValue(trial.CanonicalName, out var canonCond) && canonCond != null
                ? new NightmareAccessCost(canonCond)
                : null;
        }

        private static NightmareAccessCost LookupConditionCost(string name)
        {
            if (NightmareLogicHandler.ObjectConditions.TryGetValue(name, out var objCond) && objCond != null)
            {
                return new NightmareAccessCost(objCond);
            }

            if (NightmareLogicHandler.LocationConditions.TryGetValue(name, out var locCond) && locCond != null)
            {
                return new NightmareAccessCost(locCond);
            }

            return null;
        }

        private static NightmareAccessCost CostFromTags(TaggableObject obj)
        {
            if (obj.GetTag<CostTag>()?.Cost is NightmareAccessCost cost) return cost;

            AccessCondition condition = obj.GetTag<NightmareAccessTag>()?.Condition;
            return condition != null ? new NightmareAccessCost(condition) : null;
        }

        private static bool ContainsAny(string text, string[] needles)
        {
            if (string.IsNullOrEmpty(text)) return false;

            foreach (string needle in needles)
            {
                if (text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }
    }
}