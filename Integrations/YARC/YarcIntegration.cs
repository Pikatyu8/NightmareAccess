using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ItemChanger;
using ItemChanger.Locations;
using ItemChanger.Placements;
using NightmareAccess.Interop;
using NightmareAccess.Logic;
using RandomizerCore.Logic;
using RandomizerCore.LogicItems;
using RandomizerMod.RC;
using RandomizerMod.Settings;
using UnityEngine;

namespace NightmareAccess.Integrations.YARC
{
    internal sealed class YarcIntegration : ModIntegrationBase
    {
        public static YarcIntegration Instance { get; } = new();

        public const string ModId = "YetAnotherRandoConnection";
        public const string EssenceOrbItem = "Essence_Orb";
        public const string ChainLocation = "Chain-Storerooms";
        public const string SoulJarItem = "Soul_Jar";
        public const string EggBombItem = "Jelly_Egg_Bomb";
        public const string TelescopeLocation = "Lurien's_Telescope";
        public const string ScarecrowLocation = "Scarecrow";

        public const string DreamOrbPrefix = "DreamOrb";
        public const string EggBombPrefix = "Egg_Bomb";
        public const string VinePrefix = "Vine-";
        public const string SoulJarPrefix = "Soul_Jar-";
        public const string HivePlatformPrefix = "Hive_Platform-";
        public const string StalactitePrefix = "Stalactite-";
        public const string LoveJarPrefix = "Love_Jar-";

        private static readonly Regex OrbIndexRegex = new(@"\((\d+)\)", RegexOptions.Compiled);

        private YarcIntegration() { }

        public override string Id => ModId;

        public override void Initialize()
        {
            YarcInGameGating.Hook();
        }

        public override bool ProvidesEssenceSource(GenerationSettings gs) =>
            YarcInterop.IsDreamOrbsRandomized();

        public override bool ReplacesWhisperingRoots(GenerationSettings gs) =>
            YarcInterop.IsDreamOrbsRandomized();

        public override void ModifyLogicBeforeRolling(GenerationSettings gs, LogicManagerBuilder lmb)
        {
            // Overwrite YARC's SingleItem("Essence_Orb", ESSENCE=1) with EmptyItem in logic.
            // Treating 482 individual orbs as progression items overflows SphereBuilder group placement caps.
            if (YarcInterop.IsDreamOrbsRandomized())
            {
                lmb.AddItem(new EmptyItem(EssenceOrbItem));
            }
        }

        public override IEnumerable<string> GetAdditionalLogicLocations(LogicManagerBuilder lmb, GenerationSettings gs)
        {
            if (lmb?.LogicLookup == null) yield break;

            bool dreamOrbs = YarcInterop.IsDreamOrbsRandomized();
            bool vines = YarcInterop.IsVinesRandomized();
            bool soulJars = YarcInterop.IsSoulJarsRandomized();
            bool hivePlats = YarcInterop.IsHivePlatformsRandomized();
            bool eggBombs = YarcInterop.IsEggBombsRandomized();
            bool telescope = YarcInterop.IsTelescopeRandomized();
            bool scarecrow = YarcInterop.IsScarecrowRandomized();
            bool stalactites = YarcInterop.IsStalactitesRandomized();
            bool loveJars = YarcInterop.IsLoveJarsRandomized();

            if (!dreamOrbs && !vines && !soulJars && !hivePlats && !eggBombs
                && !telescope && !scarecrow && !stalactites && !loveJars)
            {
                yield break;
            }

            foreach (string key in lmb.LogicLookup.Keys
                         .Where(k => IsEnabledYarcLocation(
                             k, dreamOrbs, vines, soulJars, hivePlats, eggBombs,
                             telescope, scarecrow, stalactites, loveJars))
                         .OrderBy(k => k, StringComparer.Ordinal))
            {
                yield return key;
            }
        }

        private static bool IsEnabledYarcLocation(
            string key,
            bool dreamOrbs,
            bool vines,
            bool soulJars,
            bool hivePlats,
            bool eggBombs,
            bool telescope,
            bool scarecrow,
            bool stalactites,
            bool loveJars)
        {
            if (dreamOrbs && key.StartsWith("DreamOrb_", StringComparison.Ordinal)) return true;
            if (vines && (key.StartsWith(VinePrefix, StringComparison.Ordinal) || key == ChainLocation)) return true;
            if (soulJars && key.StartsWith(SoulJarPrefix, StringComparison.Ordinal)) return true;
            if (hivePlats && key.StartsWith(HivePlatformPrefix, StringComparison.Ordinal)) return true;
            if (eggBombs && key.StartsWith("Egg_Bomb-", StringComparison.Ordinal)) return true;
            if (telescope && key == TelescopeLocation) return true;
            if (scarecrow && key == ScarecrowLocation) return true;
            if (stalactites && key.StartsWith(StalactitePrefix, StringComparison.Ordinal)) return true;
            if (loveJars && key.StartsWith(LoveJarPrefix, StringComparison.Ordinal)) return true;
            return false;
        }

        public override bool IsExcludedLocation(string locationName)
        {
            if (string.IsNullOrEmpty(locationName)) return false;

            // Whispering Roots items/locations are removed by YARC when DreamOrbs is active
            if (locationName.StartsWith("Whispering_Root", StringComparison.Ordinal)
                && YarcInterop.IsDreamOrbsRandomized())
            {
                return true;
            }

            return false;
        }

        public override bool IsConflictingCondition(AccessConditionType conditionType, string locationName)
        {
            if (string.IsNullOrEmpty(locationName)) return false;

            if (locationName.StartsWith(DreamOrbPrefix, StringComparison.Ordinal)
                && conditionType is AccessConditionType.Essence or AccessConditionType.Roots)
            {
                return true;
            }

            return false;
        }

        public override string TryGetItemTypeGroup(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return null;

            if (itemName == EssenceOrbItem) return "Essence";
            if (itemName == SoulJarItem || itemName.Contains("Soul_Vial")) return "Soul";
            if (itemName.StartsWith(VinePrefix, StringComparison.Ordinal) || itemName == ChainLocation) return "Vines";
            if (itemName == EggBombItem) return "EggBombs";
            if (itemName.StartsWith(LoveJarPrefix, StringComparison.Ordinal)) return "LoveJars";
            if (itemName == TelescopeLocation) return TelescopeLocation;
            if (itemName == ScarecrowLocation) return ScarecrowLocation;

            return null;
        }

        public override AccessConditionType? TryGetItemResourceType(string itemName)
        {
            if (itemName == EssenceOrbItem) return AccessConditionType.Essence;
            return null;
        }

        public override bool IsItemSelfConflicting(string itemName, AccessConditionType conditionType, bool isGroupMode)
        {
            if (itemName == EssenceOrbItem && conditionType is AccessConditionType.Essence or AccessConditionType.Roots)
            {
                return true;
            }

            return false;
        }

        public override bool? CanPlacementAcceptCost(string locationName, AbstractPlacement placement)
        {
            if (string.IsNullOrEmpty(locationName)) return null;

            if (locationName.StartsWith(DreamOrbPrefix, StringComparison.Ordinal)) return true;
            if (locationName.StartsWith(EggBombPrefix, StringComparison.Ordinal))
            {
                return YarcInterop.TryGetEggBombPosition(locationName, out _, out _);
            }

            if (IsInPlaceGatedLocation(locationName)) return true;

            // Soul_Jar-* and Love_Jar-* fall through to standard MutablePlacement -> YNShinyPlacement handling
            return null;
        }

        public override bool TryApplyCost(
            string locationName,
            AbstractPlacement placement,
            NightmareAccessCost cost,
            IDictionary<string, AbstractPlacement> placements)
        {
            if (string.IsNullOrEmpty(locationName)) return false;

            if (locationName.StartsWith(DreamOrbPrefix, StringComparison.Ordinal)
                || IsInPlaceGatedLocation(locationName))
            {
                // Gated at interaction/trigger time by EssenceChoicePrompt / YarcInGameGating
                return true;
            }

            // YARC egg bomb: convert the auto-location into a physical shiny container
            if (locationName.StartsWith(EggBombPrefix, StringComparison.Ordinal))
            {
                if (YarcInterop.TryGetEggBombPosition(locationName, out string scene, out Vector2 pos))
                {
                    var coordLoc = new CoordinateLocation
                    {
                        sceneName = scene,
                        x = pos.x,
                        y = pos.y,
                        name = locationName
                    };
                    Cost existingCost = (placement as ISingleCostPlacement)?.Cost;
                    CostApplier.ReplaceWithYnShiny(
                        locationName,
                        placement,
                        CostApplier.CombineCost(existingCost, cost),
                        yn => yn.Location = coordLoc,
                        placements);
                }
                return true;
            }

            return false;
        }

        private static bool IsInPlaceGatedLocation(string locationName)
        {
            return locationName.StartsWith(VinePrefix, StringComparison.Ordinal)
                   || locationName == ChainLocation
                   || locationName.StartsWith(HivePlatformPrefix, StringComparison.Ordinal)
                   || locationName == ScarecrowLocation
                   || locationName.StartsWith(StalactitePrefix, StringComparison.Ordinal)
                   || locationName == TelescopeLocation;
        }

        public override void OnSimulationItemAdded(string itemName, VerificationSimulationContext sim)
        {
            if (itemName == EssenceOrbItem)
            {
                sim.ExtraEssence++;
                sim.NeedsFullRecheck = true;
            }
        }

        public override bool HasAdditionalProgressionEffect(RandoModItem item)
        {
            return item?.Name == EssenceOrbItem;
        }

        public override int GetAdditionalNumericAmount(
            AccessConditionType type, ProgressionManager pm, VerificationSimulationContext sim)
        {
            return type == AccessConditionType.Essence ? sim.ExtraEssence : 0;
        }

        public override bool HasExternalEssenceOrbSupport()
        {
            return YarcInterop.IsDreamOrbsRandomized();
        }

        public override string TryResolveDreamOrbPlacementName(DreamPlantOrb orb)
        {
            if (!YarcInterop.IsDreamOrbsRandomized() || orb == null) return null;
            return TryBuildDreamOrbPlacementName(orb.gameObject.scene.name, orb.gameObject.name);
        }

        internal static string TryBuildDreamOrbPlacementName(string sceneName, string objName)
        {
            if (string.IsNullOrEmpty(objName)) return null;

            int num = -1;
            if (objName == "Dream Plant Orb")
            {
                num = 1;
            }
            else
            {
                Match match = OrbIndexRegex.Match(objName);
                if (match.Success)
                {
                    num = int.Parse(match.Groups[1].Value) + 1;
                }
            }

            if (num <= 0) return null;

            string area = GetOrbAreaFromScene(sceneName);
            return $"DreamOrb_{area}_{num}";
        }

        public override bool ShouldBlockAutoGive(AbstractPlacement placement, GiveInfo info)
        {
            if (placement is YNShinyPlacement yn
                && NightmareAccessCost.Find(yn.Cost) != null
                && placement.Name.StartsWith("Egg_Bomb-", StringComparison.Ordinal))
            {
                // Prevent YARC auto-triggers (e.g. Jelly Egg bursting) from bypassing the YNShiny prompt
                return info?.Container == Container.Unknown || info?.FlingType == FlingType.DirectDeposit;
            }
            return false;
        }

        private static readonly Dictionary<string, string> OrbAreaByScene = new(StringComparer.Ordinal)
        {
            ["Crossroads_07"] = "Crossroads",
            ["Fungus1_13"] = "Greenpath",
            ["Fungus2_33"] = "FungalWastes_Canyon",
            ["Fungus2_17"] = "FungalWastes_Village",
            ["Deepnest_39"] = "Deepnest",
            ["Fungus3_11"] = "QueensGardens",
            ["Deepnest_East_07"] = "KingdomsEdge",
            ["Abyss_01"] = "Waterways",
            ["Ruins1_17"] = "City",
            ["RestingGrounds_05"] = "RestingGrounds",
            ["RestingGrounds_08"] = "SpiritsGlade",
            ["Mines_23"] = "CrystalPeak",
            ["Cliffs_01"] = "HowlingCliffs",
            ["Crossroads_ShamanTemple"] = "AncestralMound",
            ["Hive_02"] = "Hive"
        };

        internal static string GetOrbAreaFromScene(string sceneName) =>
            !string.IsNullOrEmpty(sceneName) && OrbAreaByScene.TryGetValue(sceneName, out string area)
                ? area
                : sceneName;
    }
}