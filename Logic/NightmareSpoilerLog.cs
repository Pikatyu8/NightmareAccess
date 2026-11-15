using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NightmareAccess.Logic;
using NightmareAccess.Settings;
using RandomizerMod.Logging;
using RandomizerMod.RC;

namespace NightmareAccess.Interop
{
    internal sealed class NightmareSpoilerLog : RandoLogger
    {
        public override void Log(LogArguments args)
        {
            if (!NightmareLogicHandler.IsSaveActive) return;

            string reportText = BuildSpoilerText(
                args?.ctx,
                NightmareLogicHandler.ActiveScope,
                NightmareAccess.Settings.RequirementChance,
                NightmareLogicHandler.LastVerificationReport);

            LogManager.Write(reportText, "NightmareAccessSpoiler.txt");
        }

        internal static string BuildSpoilerText(
            RandoModContext ctx,
            RequirementScope scope,
            int requirementChance,
            WorldVerificationReport verificationReport)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Nightmare Access Spoiler Log");
            sb.AppendLine($"Scope: {scope}");
            sb.AppendLine($"Requirement Chance: {requirementChance}%");
            sb.AppendLine();

            if (verificationReport != null)
            {
                sb.AppendLine("=== World Verification ===");
                sb.AppendLine(verificationReport.FormatSummary());
                foreach (string loc in verificationReport.UnreachableLocations)
                {
                    sb.AppendLine($"  Unreachable location: {loc}");
                }
                foreach (string wp in verificationReport.UnreachableWaypoints)
                {
                    sb.AppendLine($"  Unreachable waypoint: {wp}");
                }
                foreach (string tr in verificationReport.UnreachableTransitions)
                {
                    sb.AppendLine($"  Unreachable transition: {tr}");
                }
                foreach (string issue in verificationReport.ConsistencyIssues)
                {
                    sb.AppendLine($"  Consistency issue: {issue}");
                }
                sb.AppendLine();
            }

            var itemsByLocation = BuildLocationItemsLookup(ctx);

            switch (scope)
            {
                case RequirementScope.PerLocation:
                    sb.AppendLine("=== Location Conditions ===");
                    foreach (var kvp in NightmareLogicHandler.LocationConditions.OrderBy(k => k.Key, StringComparer.Ordinal))
                    {
                        string items = FormatItems(itemsByLocation, kvp.Key);
                        sb.AppendLine($"{kvp.Key}{items}: {kvp.Value.GetRequirementText()}");
                    }
                    break;

                case RequirementScope.PerObjectGroup:
                    sb.AppendLine("=== Object Group Conditions ===");
                    foreach (var kvp in NightmareLogicHandler.ItemTypeConditions.OrderBy(k => k.Key, StringComparer.Ordinal))
                    {
                        string text = kvp.Value != null ? kvp.Value.GetRequirementText() : "Free";
                        sb.AppendLine($"{kvp.Key}: {text}");
                    }
                    break;

                case RequirementScope.PerObject:
                    sb.AppendLine("=== Per-Object Conditions ===");
                    foreach (var kvp in NightmareLogicHandler.ObjectConditions.OrderBy(k => k.Key, StringComparer.Ordinal))
                    {
                        string text = kvp.Value != null ? kvp.Value.GetRequirementText() : "Free";
                        string items = FormatItems(itemsByLocation, kvp.Key);
                        sb.AppendLine($"{kvp.Key}{items}: {text}");
                    }
                    break;
            }

            return sb.ToString();
        }

        private static Dictionary<string, List<string>> BuildLocationItemsLookup(RandoModContext ctx)
        {
            var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            if (ctx?.itemPlacements == null) return map;

            foreach (ItemPlacement p in ctx.itemPlacements)
            {
                string loc = p.Location.Name;
                if (!map.TryGetValue(loc, out var list))
                {
                    list = new List<string>();
                    map[loc] = list;
                }
                list.Add(p.Item.Name);
            }
            return map;
        }

        private static string FormatItems(Dictionary<string, List<string>> lookup, string locName)
        {
            return lookup.TryGetValue(locName, out var list) && list.Count > 0
                ? $" [{string.Join(", ", list)}]"
                : string.Empty;
        }
    }
}