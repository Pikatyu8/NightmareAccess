using System;
using System.Collections.Generic;
using System.Linq;
using ItemChanger;
using UnityEngine;

namespace NightmareAccess.Logic
{
    internal sealed class CatalogEntry
    {
        public string Key { get; }
        public string DisplayName { get; }
        public string LogicTerm { get; }
        public Func<PlayerData, bool> IsOwned { get; }

        public CatalogEntry(string key, string displayName, string logicTerm, Func<PlayerData, bool> isOwned)
        {
            Key = key;
            DisplayName = displayName;
            LogicTerm = logicTerm;
            IsOwned = isOwned;
        }
    }

    internal sealed class EntryTable
    {
        private readonly Dictionary<string, CatalogEntry> _byKey;

        public IReadOnlyList<CatalogEntry> Entries { get; }
        public string[] Keys { get; }

        public EntryTable(params CatalogEntry[] entries)
        {
            Entries = entries;
            Keys = entries.Select(e => e.Key).ToArray();
            _byKey = entries.ToDictionary(e => e.Key);
        }

        public CatalogEntry Find(string key) =>
            key != null && _byKey.TryGetValue(key, out var entry) ? entry : null;

        public int CountOwned(PlayerData pd) => Entries.Count(e => e.IsOwned(pd));
    }

    /// <summary>Caches the set of obtained ItemChanger items once per frame (placements scan is O(n)).</summary>
    internal static class ObtainedItems
    {
        private static readonly HashSet<string> Names = new();
        private static int _frame = -1;

        internal static Func<string, bool> OverrideForTests { get; set; }

        public static bool Has(string itemName)
        {
            if (OverrideForTests != null) return OverrideForTests(itemName);
            Refresh();
            return Names.Contains(itemName);
        }

        private static void Refresh()
        {
            int frame = Time.frameCount;
            if (frame == _frame) return;

            _frame = frame;
            Names.Clear();

            var placements = ItemChanger.Internal.Ref.Settings?.Placements;
            if (placements == null) return;

            foreach (var placement in placements.Values)
            {
                foreach (var item in placement.Items)
                {
                    if (item.WasEverObtained()) Names.Add(item.name);
                }
            }
        }
    }

    internal static class ConditionCatalog
    {
        /// <summary>Index = gotCharm_N - 1. Order matches the game's internal charm IDs.</summary>
        public static readonly string[] CharmNames =
        {
            "Gathering Swarm", "Wayward Compass", "Grubsong", "Stalwart Shell", "Baldur Shell",
            "Fury of the Fallen", "Quick Focus", "Lifeblood Heart", "Lifeblood Core", "Defender's Crest",
            "Flukenest", "Thorns of Agony", "Mark of Pride", "Steady Body", "Heavy Blow",
            "Sharp Shadow", "Spore Shroom", "Longnail", "Shaman Stone", "Soul Catcher",
            "Soul Eater", "Glowing Womb", "Fragile Heart", "Fragile Greed", "Fragile Strength",
            "Nailmaster's Glory", "Joni's Blessing", "Shape of Unn", "Hiveblood", "Dream Wielder",
            "Dashmaster", "Quick Slash", "Spell Twister", "Deep Focus", "Grubberfly's Elegy",
            "Kingsoul", "Sprintmaster", "Dreamshield", "Weaversong", "Grimmchild"
        };

        public const int KingsoulCharmId = 36;

        public static string CharmDisplayName(int id) =>
            id >= 1 && id <= CharmNames.Length ? CharmNames[id - 1] : null;

        public static string CharmLogicTerm(int id)
        {
            // Kingsoul has no standalone logic item; it is the second white fragment
            if (id == KingsoulCharmId) return "WHITEFRAGMENT>1";

            string name = CharmDisplayName(id);
            return name == null ? "CHARMS" : name.Replace(' ', '_');
        }

        public static bool HasCharm(PlayerData pd, int charmId)
        {
            if (pd == null || charmId < 1 || charmId > CharmNames.Length) return false;
            if (NightmareAccess.Instance != null)
            {
                return HasCharmViaPlayerData(pd, charmId);
            }

            var field = typeof(PlayerData).GetField(
                $"gotCharm_{charmId}",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(pd) is bool b && b;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static bool HasCharmViaPlayerData(PlayerData pd, int charmId) =>
            pd.GetBool($"gotCharm_{charmId}");

        public static int CountCharms(PlayerData pd)
        {
            int count = 0;
            for (int i = 1; i <= CharmNames.Length; i++)
            {
                if (HasCharm(pd, i)) count++;
            }
            return Math.Max(count, pd.charmsOwned);
        }

        public static readonly EntryTable Maps = new(
            new CatalogEntry("Crossroads", "Crossroads", "Crossroads_Map", pd => pd.mapCrossroads),
            new CatalogEntry("Greenpath", "Greenpath", "Greenpath_Map", pd => pd.mapGreenpath),
            new CatalogEntry("FogCanyon", "Fog Canyon", "Fog_Canyon_Map", pd => pd.mapFogCanyon),
            new CatalogEntry("FungalWastes", "Fungal Wastes", "Fungal_Wastes_Map", pd => pd.mapFungalWastes),
            new CatalogEntry("City", "City of Tears", "City_of_Tears_Map", pd => pd.mapCity),
            new CatalogEntry("CrystalPeaks", "Crystal Peak", "Crystal_Peak_Map", pd => pd.mapMines),
            new CatalogEntry("RestingGrounds", "Resting Grounds", "Resting_Grounds_Map", pd => pd.mapRestingGrounds),
            new CatalogEntry("AncientBasin", "Ancient Basin", "Ancient_Basin_Map", pd => pd.mapAbyss),
            new CatalogEntry("Deepnest", "Deepnest", "Deepnest_Map", pd => pd.mapDeepnest),
            new CatalogEntry("KingdomsEdge", "Kingdom's Edge", "Kingdom's_Edge_Map", pd => pd.mapOutskirts),
            new CatalogEntry("QueensGardens", "Queen's Gardens", "Queen's_Gardens_Map", pd => pd.mapRoyalGardens),
            new CatalogEntry("HowlingCliffs", "Howling Cliffs", "Howling_Cliffs_Map", pd => pd.mapCliffs),
            new CatalogEntry("Waterways", "Royal Waterways", "Royal_Waterways_Map", pd => pd.mapWaterways));

        public static readonly EntryTable Stags = new(
            new CatalogEntry("Dirtmouth", "Dirtmouth", "Dirtmouth_Stag", pd => pd.openedTown),
            new CatalogEntry("Crossroads", "Crossroads", "Crossroads_Stag", pd => pd.openedCrossroads),
            new CatalogEntry("Greenpath", "Greenpath", "Greenpath_Stag", pd => pd.openedGreenpath),
            new CatalogEntry("QueensStation", "Queen's Station", "Queen's_Station_Stag", pd => pd.openedFungalWastes),
            new CatalogEntry("QueensGardens", "Queen's Gardens", "Queen's_Gardens_Stag", pd => pd.openedRoyalGardens),
            new CatalogEntry("CityStorerooms", "City Storerooms", "City_Storerooms_Stag", pd => pd.openedRuins1),
            new CatalogEntry("KingsStation", "King's Station", "King's_Station_Stag", pd => pd.openedRuins2),
            new CatalogEntry("RestingGrounds", "Resting Grounds", "Resting_Grounds_Stag", pd => pd.openedRestingGrounds),
            new CatalogEntry("DistantVillage", "Distant Village", "Distant_Village_Stag", pd => pd.openedDeepnest),
            new CatalogEntry("HiddenStation", "Hidden Station", "Hidden_Station_Stag", pd => pd.openedHiddenStation),
            new CatalogEntry("StagsNest", "Stag Nest", "Stag_Nest_Stag", pd => pd.openedStagNest));

        public const string JournalItemKey = "Journal";

        public static readonly string[] RollableJournalKeys =
        {
            JournalItemKey, "Goam", "Garpede", "ChargedLumafly", "VoidTendrils", "SealOfBinding"
        };

        /// <summary>All journal entries share the JOURNAL logic term.</summary>
        public static readonly EntryTable Journal = new(
            new CatalogEntry(JournalItemKey, "Hunter's Journal", "JOURNAL", pd => pd.hasJournal),
            new CatalogEntry("Goam", "Goam", "JOURNAL", pd => pd.killedWorm || ObtainedItems.Has("Journal_Entry-Goam")),
            new CatalogEntry("Garpede", "Garpede", "JOURNAL", pd => pd.killedBigCentipede || ObtainedItems.Has("Journal_Entry-Garpede")),
            new CatalogEntry("ChargedLumafly", "Charged Lumafly", "JOURNAL", pd => pd.killedZapBug || ObtainedItems.Has("Journal_Entry-Charged_Lumafly")),
            new CatalogEntry("VoidTendrils", "Void Tendrils", "JOURNAL", pd => pd.killedAbyssTendril || ObtainedItems.Has("Journal_Entry-Void_Tendrils")),
            new CatalogEntry("SealOfBinding", "Seal of Binding", "JOURNAL", pd => pd.killedBindingSeal || ObtainedItems.Has("Journal_Entry-Seal_of_Binding")),
            new CatalogEntry("HuntersMark", "Hunter's Mark", "JOURNAL", pd => pd.hasHuntersMark));

        private static readonly (string ItemName, string SceneName)[] RootDefinitions =
        {
            ("Whispering_Root-Crossroads", "Crossroads_07"),
            ("Whispering_Root-Greenpath", "Fungus1_13"),
            ("Whispering_Root-Leg_Eater", "Fungus2_33"),
            ("Whispering_Root-Mantis_Village", "Fungus2_17"),
            ("Whispering_Root-Deepnest", "Deepnest_39"),
            ("Whispering_Root-Queens_Gardens", "Fungus3_11"),
            ("Whispering_Root-Kingdoms_Edge", "Deepnest_East_07"),
            ("Whispering_Root-Waterways", "Abyss_01"),
            ("Whispering_Root-City", "Ruins1_17"),
            ("Whispering_Root-Resting_Grounds", "RestingGrounds_05"),
            ("Whispering_Root-Spirits_Glade", "RestingGrounds_08"),
            ("Whispering_Root-Crystal_Peak", "Mines_23"),
            ("Whispering_Root-Howling_Cliffs", "Cliffs_01"),
            ("Whispering_Root-Ancestral_Mound", "Crossroads_ShamanTemple"),
            ("Whispering_Root-Hive", "Hive_02")
        };

        private static readonly Dictionary<string, string> RootAreaByScene = RootDefinitions.ToDictionary(
            r => r.SceneName,
            r => r.ItemName.Substring("Whispering_Root-".Length),
            StringComparer.Ordinal);

        public static string GetRootAreaFromScene(string sceneName) =>
            !string.IsNullOrEmpty(sceneName) && RootAreaByScene.TryGetValue(sceneName, out string area)
                ? area
                : sceneName;

        public static int CountCompletedRoots(PlayerData pd)
        {
            int count = 0;
            List<string> completedScenes = pd?.scenesEncounteredDreamPlantC;
            foreach (var (itemName, sceneName) in RootDefinitions)
            {
                if (ObtainedItems.Has(itemName)
                    || (completedScenes != null && completedScenes.Contains(sceneName)))
                {
                    count++;
                }
            }
            return count;
        }
    }
}