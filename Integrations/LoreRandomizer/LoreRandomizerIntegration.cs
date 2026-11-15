using System;
using System.Collections.Generic;
using System.Linq;
using ItemChanger;
using ItemChanger.Placements;
using NightmareAccess.Interop;
using NightmareAccess.Logic;
using RandomizerCore.Logic;
using RandomizerMod.Settings;

namespace NightmareAccess.Integrations.LoreRandomizer
{
    internal sealed class LoreRandomizerIntegration : ModIntegrationBase
    {
        public static LoreRandomizerIntegration Instance { get; } = new();

        public const string ModId = "LoreRandomizer";
        public const string ShopLocationName = "Elderbug_Shop";
        public const string ReadAbilityItem = "Read_Ability";
        public const string ListenAbilityItem = "Listen_Ability";

        private static readonly string[] NpcLocations =
        {
            "Bretta", "Bardoon", "Vespa", "Midwife", "Myla", "Willoh", "Marissa",
            "Joni", "Grasshopper", "Mask_Maker", "Emilitia", "Fluke_Hermit",
            "Moss_Prophet", "Queen", "Dung_Defender", "Menderbug_Diary", "Gravedigger",
            "Poggy", "Godseeker", "Millibelle", "Hidden_Moth", "Iselda_Talk",
            "Salubra_Talk", "Sly_Talk", "Brumm", "Little_Fool"
        };

        private static readonly string[] DreamLocations =
        {
            "Ancient_Nailsmith_Golem_Dream", "Aspid_Queen_Dream", "Crystalized_Shaman_Dream",
            "Dashmaster_Statue_Dream", "Dream_Shield_Statue_Dream", "Dryya_Dream",
            "Grimm_Summoner_Dream", "Hopper_Dummy_Dream", "Isma_Dream",
            "Kings_Mould_Machine_Dream", "Mine_Golem_Dream", "Overgrown_Shaman_Dream",
            "Pale_King_Dream", "Radiance_Statue_Dream", "Shade_Golem_Dream_Normal",
            "Shade_Golem_Dream_Void", "Shriek_Statue_Dream", "Shroom_King_Dream",
            "Snail_Shaman_Tomb_Dream", "Key_Thief_Dream", "Lighthouse_Keeper_Dream"
        };

        private static readonly string[] PointOfInterestLocations =
        {
            "City_Fountain", "Dreamer_Tablet", "Weaver_Seal", "Grimm_Machine",
            "Beast_Den_Altar", "Garden_Golem", "Grub_Seal", "White_Palace_Nursery",
            "Grimm_Summoner_Corpse", "Stag_Nest", "Lore_Tablet-Record_Bela",
            "Traitor_Grave", "Elder_Hu_Grave", "Gorb_Grave", "Marmu_Grave",
            "Xero_Grave", "No_Eyes_Statue", "Markoth_Corpse", "Galien_Corpse"
        };

        private static readonly string[] TravellerLocations =
        {
            "Quirrel_Crossroads", "Quirrel_Greenpath", "Quirrel_Queen_Station",
            "Quirrel_Mantis_Village", "Quirrel_City", "Quirrel_Deepnest",
            "Quirrel_Peaks", "Quirrel_Outside_Archive", "Quirrel_After_Monomon",
            "Quirrel_Blue_Lake", "Cloth_Fungal_Wastes", "Cloth_Basin",
            "Cloth_Deepnest", "Cloth_Garden", "Cloth_End", "Tiso_Dirtmouth",
            "Tiso_Crossroads", "Tiso_Blue_Lake", "Tiso_Colosseum", "Tiso_Corpse",
            "Zote_Greenpath", "Zote_Dirtmouth_Intro", "Zote_City", "Zote_Deepnest",
            "Zote_Colosseum", "Zote_Dirtmouth_After_Colosseum", "Hornet_Greenpath",
            "Hornet_Fountain", "Hornet_Edge", "Hornet_Abyss", "Hornet_Deepnest",
            "Hornet_Temple"
        };

        private static readonly string[] ShrineLocations =
        {
            "All_Grubs-Shrine", "Drown-Shrine", "Flower-Shrine", "Full_Stag-Shrine",
            "Genocide-Shrine", "Geo-Shrine", "Half_Grubs-Shrine", "Herrah-Shrine",
            "Longest_Nail-Shrine", "Lurien-Shrine", "Monomon-Shrine", "Myla-Shrine",
            "Nailsmith-Shrine", "Respect_Elder-Shrine", "Scammed-Shrine",
            "Shade_Kill-Shrine", "Shaman-Shrine", "Slug_In_Tub-Shrine",
            "Zote-Shrine", "Deep_Dive-Shrine"
        };

        private static readonly HashSet<string> AllLoreLocationNames = new(
            NpcLocations
                .Concat(DreamLocations)
                .Concat(PointOfInterestLocations)
                .Concat(TravellerLocations)
                .Concat(ShrineLocations),
            StringComparer.Ordinal);

        private LoreRandomizerIntegration() { }

        public override string Id => ModId;

        public override void Initialize()
        {
            LoreRandomizerInGameGating.Hook();
        }

        public override IEnumerable<string> GetAdditionalLogicLocations(LogicManagerBuilder lmb, GenerationSettings gs)
        {
            if (lmb?.LogicLookup == null) yield break;

            var cfg = LoreRandomizerInterop.GetConfig();
            if (cfg == null || !cfg.Enabled) yield break;

            IEnumerable<string> YieldPresent(string[] list)
            {
                foreach (string name in list.OrderBy(n => n, StringComparer.Ordinal))
                {
                    if (lmb.LogicLookup.ContainsKey(name)) yield return name;
                }
            }

            if (cfg.RandomizeNpc)
            {
                foreach (string loc in YieldPresent(NpcLocations)) yield return loc;
            }
            if (cfg.RandomizeDreamNailDialogue)
            {
                foreach (string loc in YieldPresent(DreamLocations)) yield return loc;
            }
            if (cfg.RandomizePointOfInterest)
            {
                foreach (string loc in YieldPresent(PointOfInterestLocations)) yield return loc;
            }
            if (cfg.RandomizeTravellerDialogues)
            {
                foreach (string loc in YieldPresent(TravellerLocations)) yield return loc;
            }
            if (cfg.RandomizeShrineOfBelievers)
            {
                foreach (string loc in YieldPresent(ShrineLocations)) yield return loc;
            }
        }

        public override bool IsExcludedLocation(string locationName)
        {
            return locationName == ShopLocationName;
        }

        public override bool? CanPlacementAcceptCost(string locationName, AbstractPlacement placement)
        {
            if (string.IsNullOrEmpty(locationName) || locationName == ShopLocationName) return null;

            if (AllLoreLocationNames.Contains(locationName)) return true;

            if (placement is IPrimaryLocationPlacement iplp
                && iplp.Location?.GetType().FullName?.StartsWith("LoreCore.", StringComparison.Ordinal) == true)
            {
                return true;
            }

            return null;
        }

        public override bool TryApplyCost(
            string locationName,
            AbstractPlacement placement,
            NightmareAccessCost cost,
            IDictionary<string, AbstractPlacement> placements)
        {
            if (CanPlacementAcceptCost(locationName, placement) != true) return false;

            if (placement != null && cost != null)
            {
                CostTag tag = placement.GetOrAddTag<CostTag>();
                tag.Cost = CostApplier.CombineCost(tag.Cost, cost);
            }

            return true;
        }

        public override string TryGetItemTypeGroup(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return null;

            if (itemName == ReadAbilityItem) return ReadAbilityItem;
            if (itemName == ListenAbilityItem) return ListenAbilityItem;

            if (itemName.StartsWith("Precept_", StringComparison.Ordinal))
            {
                return "Zote_Precepts";
            }

            if (itemName.StartsWith("Dialogue-", StringComparison.Ordinal)
                || itemName.StartsWith("Dream_Dialogue-", StringComparison.Ordinal)
                || itemName.StartsWith("Inscription-", StringComparison.Ordinal)
                || itemName.StartsWith("Inspect-", StringComparison.Ordinal)
                || itemName == "Traitor_Grave"
                || itemName.StartsWith("Lore_Tablet-", StringComparison.Ordinal))
            {
                return "Lore";
            }

            return null;
        }

        public override bool IsNonResourceItem(string itemName)
        {
            // Prevents items like Inspect-Stag_Egg or Inspect-Grub_Seal from matching Stag/Egg/Grub substrings
            return TryGetItemTypeGroup(itemName) != null;
        }
    }
}