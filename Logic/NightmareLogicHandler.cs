using System;
using System.Collections.Generic;
using System.Linq;
using NightmareAccess.Integrations;
using NightmareAccess.Settings;
using RandomizerCore.Logic;
using RandomizerMod.RandomizerData;
using RandomizerMod.RC;
using RandomizerMod.Settings;
using Random = System.Random;

namespace NightmareAccess.Logic
{
    public static class NightmareLogicHandler
    {
        public static bool IsSaveActive { get; set; }
        public static RequirementScope ActiveScope { get; set; } = RequirementScope.PerLocation;
        public static Dictionary<string, AccessCondition> LocationConditions = new();
        public static Dictionary<string, List<AccessCondition>> ShopConditions = new();
        public static Dictionary<string, AccessCondition> ItemTypeConditions = new();
        public static Dictionary<string, AccessCondition> ObjectConditions = new();
        public static Dictionary<string, int> IntegrationInts = new();
        public static WorldVerificationReport LastVerificationReport { get; internal set; }

        public static readonly HashSet<string> CharmItemNames = new()
        {
            "Gathering_Swarm", "Wayward_Compass", "Grubsong", "Stalwart_Shell", "Baldur_Shell",
            "Fury_of_the_Fallen", "Quick_Focus", "Lifeblood_Heart", "Lifeblood_Core", "Defender's_Crest",
            "Flukenest", "Thorns_of_Agony", "Mark_of_Pride", "Steady_Body", "Heavy_Blow",
            "Sharp_Shadow", "Spore_Shroom", "Longnail", "Shaman_Stone", "Soul_Catcher",
            "Soul_Eater", "Glowing_Womb", "Fragile_Heart", "Unbreakable_Heart", "Fragile_Greed",
            "Unbreakable_Greed", "Fragile_Strength", "Unbreakable_Strength", "Nailmaster's_Glory",
            "Joni's_Blessing", "Shape_of_Unn", "Hiveblood", "Dream_Wielder", "Dashmaster",
            "Quick_Slash", "Spell_Twister", "Deep_Focus", "Grubberfly's_Elegy", "Queen_Fragment",
            "King_Fragment", "Void_Heart", "Sprintmaster", "Dreamshield", "Weaversong",
            "Grimmchild1", "Grimmchild2", "Carefree_Melody"
        };

        public static readonly HashSet<string> MajorProgressionLocations = new()
        {
            "Mothwing_Cloak", "Mantis_Claw", "Crystal_Heart", "Monarch_Wings",
            "Shade_Cloak", "Isma's_Tear", "Dream_Nail", "Vengeful_Spirit",
            "Desolate_Dive", "Howling_Wraiths"
        };

        public static void Hook()
        {
            // Priority 50_000 ensures execution after ExtraRando (1050), GodhomeRandomizer (4096),
            // CurseRandomizer (9999), and LoreRandomizer (9999), before MoreStags (69420) & RandoPlus AllowSkips (100_000).
            RCData.RuntimeLogicOverride.Subscribe(50_000f, ApplyNightmareLogic);
        }

        public static bool IsExcludedLocation(string locName)
        {
            if (string.IsNullOrEmpty(locName)) return true;
            if (locName == "Start") return true;
            if (locName.StartsWith("Grubfather", StringComparison.Ordinal)) return true;
            if (locName.StartsWith("Seer", StringComparison.Ordinal)) return true;
            if (locName.StartsWith("Egg_Shop", StringComparison.Ordinal)
                || locName.StartsWith("Tuk", StringComparison.Ordinal)
                || locName == "Rancid_Egg-Tuk_Defender's_Crest") return true;

            // Starter area preservation: King's Pass remains free so Sphere 0 is never empty
            if (locName.Contains("King's_Pass") || locName == "Fury_of_the_Fallen") return true;

            // AutoLocations that do not support ItemChanger costs (give directly on trigger without Y/N prompt)
            if (locName.StartsWith("Boss_Essence-", StringComparison.Ordinal)) return true;
            if (locName.StartsWith("Grimmkin_Flame-", StringComparison.Ordinal)) return true;
            if (locName == "Journal_Entry-Seal_of_Binding") return true;

            if (IntegrationRegistry.IsExcludedLocation(locName)) return true;

            return false;
        }

        public static readonly string[] ShopNames = { "Sly", "Sly_(Key)", "Iselda", "Salubra", "Leg_Eater" };

        public const int MaxShopItems = 20;

        public static bool IsShop(string locName) => Array.IndexOf(ShopNames, locName) >= 0;

        public static bool IsConflictingCondition(AccessConditionType condType, string locName)
        {
            if (string.IsNullOrEmpty(locName)) return false;

            if (IntegrationRegistry.IsConflictingCondition(condType, locName)) return true;

            if ((locName.StartsWith("Whispering_Root", StringComparison.Ordinal)
                 || locName.Contains("Essence"))
                && (condType == AccessConditionType.Essence || condType == AccessConditionType.Roots))
                return true;

            // Deadlock prevention: Dream Nail cannot require Essence because acquiring Essence logically requires Dream Nail
            if ((locName == "Dream_Nail" || locName.StartsWith("Dream_Nail", StringComparison.Ordinal))
                && (condType == AccessConditionType.Essence || condType == AccessConditionType.Roots))
                return true;

            if ((locName == "Grub" || locName.StartsWith("Grub-", StringComparison.Ordinal) || locName.Contains("Grub"))
                && condType == AccessConditionType.Grubs)
                return true;

            if ((locName.Contains("Mask_Shard") || locName.Contains("Full_Mask"))
                && condType == AccessConditionType.MaskShards)
                return true;

            if ((locName.Contains("Vessel_Fragment") || locName.Contains("Full_Soul_Vessel"))
                && condType == AccessConditionType.VesselFragments)
                return true;

            if (locName.Contains("Pale_Ore") && condType == AccessConditionType.PaleOre)
                return true;

            if (locName.Contains("Charm_Notch") && condType == AccessConditionType.Notches)
                return true;

            if (locName.Contains("Simple_Key") && condType == AccessConditionType.SimpleKeys)
                return true;

            if ((locName.Contains("Wanderer's_Journal") || locName.Contains("Hallownest_Seal")
                 || locName.Contains("King's_Idol") || locName.Contains("Arcane_Egg"))
                && condType == AccessConditionType.Relics)
                return true;

            if (locName.Contains("Stag") && (condType == AccessConditionType.StagsCount || condType == AccessConditionType.SpecificStag))
                return true;

            if ((locName.Contains("Journal") || locName.Contains("Hunter"))
                && (condType == AccessConditionType.JournalCount || condType == AccessConditionType.SpecificJournalEntry))
                return true;

            if (locName.Contains("Map") && (condType == AccessConditionType.MapsCount || condType == AccessConditionType.SpecificMap))
                return true;

            if ((CharmItemNames.Any(c => locName.Contains(c)) || locName.StartsWith("Charm", StringComparison.Ordinal))
                && (condType == AccessConditionType.CharmsCount || condType == AccessConditionType.SpecificCharm))
                return true;

            if (locName.Contains("Cloak") && condType is AccessConditionType.Dash or AccessConditionType.LeftDash or AccessConditionType.RightDash)
                return true;
            if (locName.Contains("Claw") && condType is AccessConditionType.WallJump or AccessConditionType.LeftWallJump or AccessConditionType.RightWallJump)
                return true;
            if (locName.Contains("Crystal_Heart") && condType is AccessConditionType.Superdash or AccessConditionType.LeftSuperdash or AccessConditionType.RightSuperdash)
                return true;
            if (locName.Contains("Wings") && condType == AccessConditionType.DoubleJump)
                return true;
            if (locName.Contains("Tear") && condType == AccessConditionType.AcidArmour)
                return true;
            if (locName.Contains("Dream_Nail") && condType == AccessConditionType.DreamNailLevel)
                return true;

            return false;
        }

        private static void ApplyNightmareLogic(GenerationSettings gs, LogicManagerBuilder lmb)
        {
            ApplyNightmareLogic(gs, lmb, NightmareAccess.Settings);
        }

        internal static void ApplyNightmareLogic(GenerationSettings gs, LogicManagerBuilder lmb, NightmareAccessSettings settings)
        {
            LocationConditions.Clear();
            ShopConditions.Clear();
            ItemTypeConditions.Clear();
            ObjectConditions.Clear();
            IntegrationInts.Clear();
            LastVerificationReport = null;

            IsSaveActive = settings.Enabled;
            ActiveScope = settings.Scope;

            if (!IsSaveActive) return;

            IntegrationRegistry.RecordSaveState(gs, IntegrationInts);

            Random rng = new(gs.Seed + 0x6E61);
            var allowedTypes = GetAllowedTypes(settings, gs);
            if (allowedTypes.Count == 0) return;

            IntegrationRegistry.ModifyLogicBeforeRolling(gs, lmb);

            settings.Normalize();

            var resourceTypes = allowedTypes
                .Where(t => t != AccessConditionType.JournalCount && AccessCondition.IsNumericType(t))
                .ToList();
            if (resourceTypes.Count == 0) resourceTypes = allowedTypes;

            if (settings.Scope != RequirementScope.PerLocation) return;

            foreach (var locName in Data.Locations.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                if (IsExcludedLocation(locName) || IsShop(locName)) continue;
                if (rng.Next(100) >= settings.RequirementChance) continue;

                bool isMajor = MajorProgressionLocations.Contains(locName);
                var basePool = isMajor ? resourceTypes : allowedTypes;
                var pool = basePool.Where(t => !IsConflictingCondition(t, locName)).ToList();
                if (pool.Count == 0) pool = resourceTypes.Where(t => !IsConflictingCondition(t, locName)).ToList();
                if (pool.Count == 0) pool = basePool;

                var cond = RollCondition(rng, pool, settings, gs, isMajor);
                if (cond == null) continue;
                LocationConditions[locName] = cond;

                if (lmb.LogicLookup.ContainsKey(locName) && !lmb.Waypoints.Contains(locName))
                {
                    string expr = cond.ToLogicExpression(gs);
                    lmb.DoLogicEdit(new RawLogicDef(locName, $"ORIG + ({expr})"));
                }
            }

            foreach (string extraLoc in IntegrationRegistry.GetAdditionalLogicLocations(lmb, gs))
            {
                if (Data.Locations != null && Data.Locations.ContainsKey(extraLoc)) continue;
                if (IsExcludedLocation(extraLoc) || IsShop(extraLoc)) continue;
                if (rng.Next(100) >= settings.RequirementChance) continue;

                var pool = allowedTypes.Where(t => !IsConflictingCondition(t, extraLoc)).ToList();
                if (pool.Count == 0) pool = allowedTypes;

                var cond = RollCondition(rng, pool, settings, gs, false);
                if (cond == null) continue;
                LocationConditions[extraLoc] = cond;

                // Never mutate waypoint logic entries (e.g. Bench-*) in LogicLookup, as waypoints are shared by routing/Benchwarp.
                if (lmb.LogicLookup.ContainsKey(extraLoc) && !lmb.Waypoints.Contains(extraLoc))
                {
                    string expr = cond.ToLogicExpression(gs);
                    if (expr != "TRUE")
                    {
                        lmb.DoLogicEdit(new RawLogicDef(extraLoc, $"ORIG + ({expr})"));
                    }
                }
            }

            ModLog.Info($"Logic generated: Scope={ActiveScope}, LocationConditions={LocationConditions.Count}");
        }

        public static string GetItemTypeGroup(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return "Unknown";

            string integrationGroup = IntegrationRegistry.TryGetItemTypeGroup(itemName);
            if (!string.IsNullOrEmpty(integrationGroup)) return integrationGroup;

            if (itemName.Contains("Geo") || itemName.Contains("Rock")) return "Geo";
            if (itemName.Contains("Soul_Totem") || itemName.Contains("Soul_Refill") || itemName.Contains("Totem")) return "Soul";
            if (itemName.Contains("Whispering_Root") || itemName.Contains("Essence")) return "Essence";
            if (itemName.Contains("Rancid_Egg")) return "RancidEggs";
            if (itemName.Contains("Pale_Ore")) return "PaleOre";
            if (itemName.Contains("Charm_Notch")) return "Notches";
            if (itemName.Contains("Simple_Key")) return "SimpleKeys";

            if (itemName.Contains("Mask_Shard") || itemName == "Full_Mask" || itemName == "Double_Mask_Shard") return "MaskShards";
            if (itemName.Contains("Vessel_Fragment") || itemName == "Full_Soul_Vessel" || itemName == "Double_Vessel_Fragment") return "VesselFragments";

            if (itemName.StartsWith("Lore_Tablet", StringComparison.Ordinal)) return "Lore";
            if (itemName.StartsWith("Lifeblood_Cocoon", StringComparison.Ordinal)) return "Cocoons";

            if (itemName == "Grub" || itemName == "Mimic_Grub") return "Grubs";

            if (itemName.Contains("Wanderer's_Journal") || itemName.Contains("Hallownest_Seal") || itemName.Contains("King's_Idol") || itemName.Contains("Arcane_Egg")) return "Relics";

            if (itemName.StartsWith("Journal_Entry", StringComparison.Ordinal) || itemName == "Hunter's_Journal") return "JournalEntries";

            if (itemName != "Collector's_Map" && (itemName.Contains("Map") || itemName == "Quill")) return "Maps";

            if (itemName.Contains("Stag")) return "Stags";

            if (itemName == "Lurien" || itemName == "Monomon" || itemName == "Herrah" || itemName == "Dreamer") return "Dreamers";

            if (itemName.Contains("Mothwing_Cloak") || itemName == "Split_Shade_Cloak") return "Mothwing_Cloak";
            if (itemName.Contains("Mantis_Claw")) return "Mantis_Claw";
            if (itemName.Contains("Crystal_Heart")) return "Crystal_Heart";
            if (itemName == "Leftslash" || itemName == "Rightslash" || itemName == "Upslash" || itemName == "Downslash") return "CursedNail";

            if (CharmItemNames.Contains(itemName) || itemName.StartsWith("Charm", StringComparison.Ordinal) || itemName.Contains("Fragile_") || itemName.Contains("Unbreakable_")) return "Charms";

            return itemName;
        }

        public static List<AccessConditionType> GetAllowedTypes(NightmareAccessSettings s, GenerationSettings gs)
        {
            var list = new List<AccessConditionType>();
            if (gs == null) return list;

            bool externalEssenceSource = IntegrationRegistry.ProvidesEssenceSource(gs);
            bool replacesRoots = IntegrationRegistry.ReplacesWhisperingRoots(gs);
            bool junkReplaced = gs.CursedSettings.ReplaceJunkWithOneGeo;

            if (s.AllowGeo && (gs.PoolSettings.GeoChests || gs.PoolSettings.GeoRocks || junkReplaced)) list.Add(AccessConditionType.Geo);
            if (s.AllowEggs && gs.PoolSettings.RancidEggs && (!junkReplaced || gs.NoveltySettings.EggShop)) list.Add(AccessConditionType.RancidEggs);

            bool hasEssenceSource = gs.PoolSettings.BossEssence || gs.PoolSettings.WhisperingRoots || externalEssenceSource;
            if (s.AllowEssence && hasEssenceSource) list.Add(AccessConditionType.Essence);

            if (s.AllowGrubs && gs.PoolSettings.Grubs) list.Add(AccessConditionType.Grubs);
            if (s.AllowRoots && !replacesRoots && gs.PoolSettings.WhisperingRoots) list.Add(AccessConditionType.Roots);
            if (s.AllowSimpleKeys && gs.PoolSettings.Keys) list.Add(AccessConditionType.SimpleKeys);

            if (!junkReplaced)
            {
                if (s.AllowMaskShards && gs.PoolSettings.MaskShards) list.Add(AccessConditionType.MaskShards);
                if (s.AllowVesselFragments && gs.PoolSettings.VesselFragments) list.Add(AccessConditionType.VesselFragments);
                if (s.AllowPaleOre && gs.PoolSettings.PaleOre) list.Add(AccessConditionType.PaleOre);
                if (s.AllowNotches && gs.PoolSettings.CharmNotches) list.Add(AccessConditionType.Notches);
                if (s.AllowRelics && gs.PoolSettings.Relics) list.Add(AccessConditionType.Relics);
            }

            if (gs.PoolSettings.Skills)
            {
                if (s.AllowDash)
                {
                    if (gs.NoveltySettings.SplitCloak || IntegrationRegistry.IsCloakSplit(gs))
                    {
                        list.Add(AccessConditionType.LeftDash);
                        list.Add(AccessConditionType.RightDash);
                    }
                    else list.Add(AccessConditionType.Dash);
                }


                if (s.AllowClaw)
                {
                    if (gs.NoveltySettings.SplitClaw)
                    {
                        list.Add(AccessConditionType.LeftWallJump);
                        list.Add(AccessConditionType.RightWallJump);
                    }
                    else list.Add(AccessConditionType.WallJump);
                }

                if (s.AllowSuperdash)
                {
                    if (gs.NoveltySettings.SplitSuperdash)
                    {
                        list.Add(AccessConditionType.LeftSuperdash);
                        list.Add(AccessConditionType.RightSuperdash);
                    }
                    else list.Add(AccessConditionType.Superdash);
                }

                if (s.AllowDoubleJump) list.Add(AccessConditionType.DoubleJump);
                if (s.AllowAcidArmour) list.Add(AccessConditionType.AcidArmour);
                if (s.AllowDreamNail) list.Add(AccessConditionType.DreamNailLevel);
                if (s.AllowNailArts) list.Add(AccessConditionType.NailArt);

                if (s.AllowFireball) list.Add(AccessConditionType.FireballLevel);
                if (s.AllowQuake) list.Add(AccessConditionType.QuakeLevel);
                if (s.AllowScream) list.Add(AccessConditionType.ScreamLevel);
            }

            if (gs.PoolSettings.Keys)
            {
                if (s.AllowCityCrest) list.Add(AccessConditionType.CityCrest);
                if (s.AllowLantern) list.Add(AccessConditionType.LumaflyLantern);
            }

            if (s.AllowDreamers && gs.PoolSettings.Dreamers) list.Add(AccessConditionType.Dreamer);

            if (gs.PoolSettings.Charms)
            {
                if (s.AllowKingsoul) list.Add(AccessConditionType.KingsoulLevel);
                if (s.AllowGrimmchild) list.Add(AccessConditionType.GrimmchildLevel);
            }

            if (s.AllowCharms && gs.PoolSettings.Charms)
            {
                if (s.AllowSpecificCharms) list.Add(AccessConditionType.SpecificCharm);
                if (s.AllowCharmsCount) list.Add(AccessConditionType.CharmsCount);
            }

            if (s.AllowMaps && gs.PoolSettings.Maps)
            {
                if (s.AllowSpecificMaps) list.Add(AccessConditionType.SpecificMap);
                if (s.AllowMapsCount) list.Add(AccessConditionType.MapsCount);
            }

            if (s.AllowJournal && gs.PoolSettings.JournalEntries)
            {
                if (s.AllowSpecificJournal) list.Add(AccessConditionType.SpecificJournalEntry);
                if (s.AllowJournalCount) list.Add(AccessConditionType.JournalCount);
            }

            if (s.AllowStags && gs.PoolSettings.Stags)
            {
                if (s.AllowSpecificStags) list.Add(AccessConditionType.SpecificStag);
                if (s.AllowStagsCount) list.Add(AccessConditionType.StagsCount);
            }

            list.RemoveAll(t => IntegrationRegistry.IsConditionTypeDisallowed(t, gs));

            if (list.Count == 0 && s.AllowGeo) list.Add(AccessConditionType.Geo);
            return list;
        }

        private static int RollRange(Random rng, int min, int max, int cap = int.MaxValue)
        {
            max = Math.Min(max, cap);
            min = Math.Min(min, max);
            return rng.Next(min, max + 1);
        }

        private static string PickKey(Random rng, string[] keys) => keys[rng.Next(keys.Length)];

        private static string PickJournalKey(Random rng, GenerationSettings gs)
        {
            var wp = gs?.LongLocationSettings?.WhitePalaceRando;
            bool excludeSeal = wp is LongLocationSettings.WPSetting.ExcludeWhitePalace or LongLocationSettings.WPSetting.ExcludePathOfPain;
            if (!excludeSeal) return PickKey(rng, ConditionCatalog.RollableJournalKeys);

            string[] keys = ConditionCatalog.RollableJournalKeys.Where(k => k != "SealOfBinding").ToArray();
            return PickKey(rng, keys);
        }

        public static AccessCondition RollCondition(Random rng, List<AccessConditionType> allowed, NightmareAccessSettings s, GenerationSettings gs, bool isMajor = false)
        {
            if (allowed == null || allowed.Count == 0) return null;

            var type = allowed[rng.Next(allowed.Count)];
            bool noSpellUpgrades = gs?.CursedSettings?.RemoveSpellUpgrades == true;

            if (isMajor)
            {
                return type switch
                {
                    AccessConditionType.Geo => new AccessCondition(type, RollRange(rng, s.MinGeo, s.MaxGeo, 300)),
                    AccessConditionType.RancidEggs => new AccessCondition(type, RollRange(rng, 1, s.MaxEggs, 2)),
                    AccessConditionType.Essence => new AccessCondition(type, RollRange(rng, 20, s.MaxEssence, 100)),
                    AccessConditionType.Grubs => new AccessCondition(type, RollRange(rng, 1, s.MaxGrubs, 4)),
                    AccessConditionType.MaskShards => new AccessCondition(type, RollRange(rng, 1, s.MaxMaskShards, 2)),
                    AccessConditionType.Relics => new AccessCondition(type, RollRange(rng, 1, s.MaxRelics, 2)),
                    AccessConditionType.CharmsCount => new AccessCondition(type, rng.Next(1, 3)),
                    AccessConditionType.Roots or AccessConditionType.SimpleKeys or AccessConditionType.VesselFragments
                        or AccessConditionType.PaleOre or AccessConditionType.Notches or AccessConditionType.MapsCount
                        or AccessConditionType.JournalCount or AccessConditionType.StagsCount => new AccessCondition(type, 1),
                    _ => RollCondition(rng, new List<AccessConditionType> { type }, s, gs, false)
                };
            }

            return type switch
            {
                AccessConditionType.Geo => new AccessCondition(type, RollRange(rng, s.MinGeo, s.MaxGeo)),
                AccessConditionType.RancidEggs => new AccessCondition(type, RollRange(rng, s.MinEggs, s.MaxEggs)),
                AccessConditionType.Essence => new AccessCondition(type, RollRange(rng, s.MinEssence, s.MaxEssence)),
                AccessConditionType.Grubs => new AccessCondition(type, RollRange(rng, s.MinGrubs, s.MaxGrubs)),
                AccessConditionType.Roots => new AccessCondition(type, RollRange(rng, s.MinRoots, s.MaxRoots)),
                AccessConditionType.SimpleKeys => new AccessCondition(type, RollRange(rng, s.MinSimpleKeys, s.MaxSimpleKeys, 3)),
                AccessConditionType.MaskShards => new AccessCondition(type, RollRange(rng, s.MinMaskShards, s.MaxMaskShards)),
                AccessConditionType.VesselFragments => new AccessCondition(type, RollRange(rng, s.MinVesselFragments, s.MaxVesselFragments)),
                AccessConditionType.PaleOre => new AccessCondition(type, RollRange(rng, s.MinPaleOre, s.MaxPaleOre)),
                AccessConditionType.Notches => new AccessCondition(type, RollRange(rng, s.MinNotches, s.MaxNotches)),
                AccessConditionType.Relics => new AccessCondition(type, RollRange(rng, s.MinRelics, s.MaxRelics)),
                AccessConditionType.CharmsCount => new AccessCondition(type, rng.Next(1, 35)),
                AccessConditionType.MapsCount => new AccessCondition(type, rng.Next(1, 10)),
                AccessConditionType.JournalCount => new AccessCondition(type, 1),
                AccessConditionType.StagsCount => new AccessCondition(type, rng.Next(1, 8)),

                AccessConditionType.DreamNailLevel => new AccessCondition(type, rng.Next(1, 4)),
                AccessConditionType.NailArt => new AccessCondition(type, 1, 0, PickKey(rng, new[] { "Great", "Dash", "Cyclone" })),

                AccessConditionType.FireballLevel => new AccessCondition(type, noSpellUpgrades ? 1 : rng.Next(1, 3)),
                AccessConditionType.QuakeLevel => new AccessCondition(type, noSpellUpgrades ? 1 : rng.Next(1, 3)),
                AccessConditionType.ScreamLevel => new AccessCondition(type, noSpellUpgrades ? 1 : rng.Next(1, 3)),

                AccessConditionType.Dreamer => new AccessCondition(type, 1, 0, PickKey(rng, new[] { "Lurien", "Monomon", "Herrah" })),
                AccessConditionType.KingsoulLevel => new AccessCondition(type, rng.Next(1, 4)),
                // Cap rolled Grimmchild at Lv3 because Lv4 requires NKG/banish, which base Rando 4 logic does not track
                AccessConditionType.GrimmchildLevel => new AccessCondition(type, rng.Next(1, 4)),

                AccessConditionType.SpecificCharm => new AccessCondition(type, 1, rng.Next(1, ConditionCatalog.CharmNames.Length + 1)),
                AccessConditionType.SpecificMap => new AccessCondition(type, 1, 0, PickKey(rng, ConditionCatalog.Maps.Keys)),
                AccessConditionType.SpecificJournalEntry => new AccessCondition(type, 1, 0, PickJournalKey(rng, gs)),
                AccessConditionType.SpecificStag => new AccessCondition(type, 1, 0, PickKey(rng, ConditionCatalog.Stags.Keys)),

                _ => new AccessCondition(type)
            };
        }

        public static AccessCondition RerollExcluding(Random rng, List<AccessConditionType> allowed, NightmareAccessSettings s, GenerationSettings gs, AccessConditionType excludedType, bool isMajor = false)
        {
            if (allowed == null || allowed.Count == 0) return null;

            var filtered = allowed.Where(t => t != excludedType).ToList();
            if (filtered.Count == 0) filtered = allowed;
            return RollCondition(rng, filtered, s, gs, isMajor);
        }

        public static AccessCondition RollObjectCondition(
            Random rng, List<AccessConditionType> allowed, NightmareAccessSettings s, GenerationSettings gs, string itemName, bool isGroupMode)
        {
            if (allowed == null || allowed.Count == 0) return null;

            var eligible = allowed
                .Where(t => !IsAlwaysConflictingObjectType(itemName, t, isGroupMode))
                .ToList();
            if (eligible.Count == 0) eligible = allowed;

            AccessCondition candidate = null;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                candidate = RollCondition(rng, eligible, s, gs);
                if (!IsSpecificSelfConflict(itemName, candidate))
                {
                    return candidate;
                }
            }

            return candidate;
        }

        internal static bool IsAlwaysConflictingObjectType(string itemName, AccessConditionType condType, bool isGroupMode)
        {
            if (string.IsNullOrEmpty(itemName)) return false;

            if (IntegrationRegistry.IsItemSelfConflicting(itemName, condType, isGroupMode)) return true;

            AccessConditionType? resourceType = GetItemResourceType(itemName);
            if (resourceType.HasValue && condType == resourceType.Value) return true;

            if (itemName.Contains("Essence") || itemName.Contains("Whispering_Root"))
            {
                if (condType is AccessConditionType.Essence or AccessConditionType.Roots) return true;
            }

            if (isGroupMode)
            {
                string group = GetItemTypeGroup(itemName);
                switch (group)
                {
                    case "Charms":
                        return condType is AccessConditionType.CharmsCount or AccessConditionType.SpecificCharm
                            or AccessConditionType.KingsoulLevel or AccessConditionType.GrimmchildLevel;
                    case "Maps":
                        return condType is AccessConditionType.MapsCount or AccessConditionType.SpecificMap;
                    case "Stags":
                        return condType is AccessConditionType.StagsCount or AccessConditionType.SpecificStag;
                    case "JournalEntries":
                        return condType is AccessConditionType.JournalCount or AccessConditionType.SpecificJournalEntry;
                    case "Dreamers":
                        return condType == AccessConditionType.Dreamer;
                    case "Mothwing_Cloak":
                        return condType is AccessConditionType.Dash or AccessConditionType.LeftDash or AccessConditionType.RightDash;
                    case "Mantis_Claw":
                        return condType is AccessConditionType.WallJump or AccessConditionType.LeftWallJump or AccessConditionType.RightWallJump;
                    case "Crystal_Heart":
                        return condType is AccessConditionType.Superdash or AccessConditionType.LeftSuperdash or AccessConditionType.RightSuperdash;
                }
            }

            return itemName switch
            {
                "Mothwing_Cloak" or "Shade_Cloak" or "Split_Shade_Cloak" =>
                    condType is AccessConditionType.Dash or AccessConditionType.LeftDash or AccessConditionType.RightDash,
                "Left_Mothwing_Cloak" => condType is AccessConditionType.Dash or AccessConditionType.LeftDash,
                "Right_Mothwing_Cloak" => condType is AccessConditionType.Dash or AccessConditionType.RightDash,
                "Mantis_Claw" =>
                    condType is AccessConditionType.WallJump or AccessConditionType.LeftWallJump or AccessConditionType.RightWallJump,
                "Left_Mantis_Claw" => condType is AccessConditionType.WallJump or AccessConditionType.LeftWallJump,
                "Right_Mantis_Claw" => condType is AccessConditionType.WallJump or AccessConditionType.RightWallJump,
                "Crystal_Heart" =>
                    condType is AccessConditionType.Superdash or AccessConditionType.LeftSuperdash or AccessConditionType.RightSuperdash,
                "Left_Crystal_Heart" => condType is AccessConditionType.Superdash or AccessConditionType.LeftSuperdash,
                "Right_Crystal_Heart" => condType is AccessConditionType.Superdash or AccessConditionType.RightSuperdash,
                "Monarch_Wings" => condType == AccessConditionType.DoubleJump,
                "Isma's_Tear" => condType == AccessConditionType.AcidArmour,
                "Dream_Nail" => condType is AccessConditionType.DreamNailLevel or AccessConditionType.Essence or AccessConditionType.Roots,
                "Dream_Gate" or "Awoken_Dream_Nail" => condType == AccessConditionType.DreamNailLevel,
                "Vengeful_Spirit" or "Shade_Soul" => condType == AccessConditionType.FireballLevel,
                "Desolate_Dive" or "Descending_Dark" => condType == AccessConditionType.QuakeLevel,
                "Howling_Wraiths" or "Abyss_Shriek" => condType == AccessConditionType.ScreamLevel,
                "City_Crest" => condType == AccessConditionType.CityCrest,
                "Lumafly_Lantern" => condType == AccessConditionType.LumaflyLantern,
                "Queen_Fragment" or "King_Fragment" or "Void_Heart" =>
                    condType is AccessConditionType.KingsoulLevel or AccessConditionType.CharmsCount,
                "Grimmchild1" or "Grimmchild2" or "Carefree_Melody" =>
                    condType is AccessConditionType.GrimmchildLevel or AccessConditionType.CharmsCount,
                "Dreamer" => condType == AccessConditionType.Dreamer,
                _ => false
            };
        }

        internal static bool IsSpecificSelfConflict(string itemName, AccessCondition cond)
        {
            if (string.IsNullOrEmpty(itemName) || cond == null) return false;

            switch (cond.Type)
            {
                case AccessConditionType.SpecificCharm:
                    if (cond.SpecificId == ConditionCatalog.KingsoulCharmId
                        && itemName is "Queen_Fragment" or "King_Fragment" or "Void_Heart")
                    {
                        return true;
                    }
                    if (cond.SpecificId == 40 && itemName is "Grimmchild1" or "Grimmchild2" or "Carefree_Melody")
                    {
                        return true;
                    }
                    string charmItem = ConditionCatalog.CharmDisplayName(cond.SpecificId)?.Replace(' ', '_');
                    return charmItem == itemName;

                case AccessConditionType.SpecificMap:
                    return ConditionCatalog.Maps.Find(cond.SpecificKey)?.LogicTerm == itemName;

                case AccessConditionType.SpecificStag:
                    return ConditionCatalog.Stags.Find(cond.SpecificKey)?.LogicTerm == itemName;

                case AccessConditionType.SpecificJournalEntry:
                    if (cond.SpecificKey == ConditionCatalog.JournalItemKey)
                    {
                        return itemName == "Hunter's_Journal";
                    }
                    string entryDisplay = ConditionCatalog.Journal.Find(cond.SpecificKey)?.DisplayName?.Replace(' ', '_');
                    return entryDisplay != null && itemName == $"Journal_Entry-{entryDisplay}";

                case AccessConditionType.NailArt:
                    return (cond.SpecificKey == "Cyclone" && itemName == "Cyclone_Slash")
                        || (cond.SpecificKey == "Dash" && itemName == "Dash_Slash")
                        || (cond.SpecificKey == "Great" && itemName == "Great_Slash");

                case AccessConditionType.Dreamer:
                    return cond.SpecificKey == itemName;

                default:
                    return false;
            }
        }

        public static AccessConditionType? GetItemResourceType(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return null;

            AccessConditionType? integrationResource = IntegrationRegistry.TryGetItemResourceType(itemName);
            if (integrationResource.HasValue) return integrationResource;
            if (IntegrationRegistry.IsNonResourceItem(itemName)) return null;

            if (itemName.Contains("Geo") || itemName.Contains("Rock")) return AccessConditionType.Geo;
            if (itemName.Contains("Rancid_Egg")) return AccessConditionType.RancidEggs;
            if (itemName == "Grub" || itemName == "Mimic_Grub") return AccessConditionType.Grubs;
            if (itemName.Contains("Simple_Key")) return AccessConditionType.SimpleKeys;
            if (itemName.Contains("Mask_Shard") || itemName == "Full_Mask") return AccessConditionType.MaskShards;
            if (itemName.Contains("Vessel_Fragment") || itemName == "Full_Soul_Vessel") return AccessConditionType.VesselFragments;
            if (itemName.Contains("Pale_Ore")) return AccessConditionType.PaleOre;
            if (itemName.Contains("Charm_Notch")) return AccessConditionType.Notches;
            if (itemName.Contains("Essence") || itemName.Contains("Whispering_Root")) return AccessConditionType.Essence;
            if (itemName.Contains("Wanderer's_Journal") || itemName.Contains("Hallownest_Seal") || itemName.Contains("King's_Idol") || itemName.Contains("Arcane_Egg")) return AccessConditionType.Relics;
            if (itemName.StartsWith("Journal_Entry", StringComparison.Ordinal) || itemName == "Hunter's_Journal") return AccessConditionType.JournalCount;
            if (itemName != "Collector's_Map" && (itemName.Contains("Map") || itemName == "Quill")) return AccessConditionType.MapsCount;
            if (itemName.Contains("Stag")) return AccessConditionType.StagsCount;
            if (CharmItemNames.Contains(itemName) || itemName.StartsWith("Charm", StringComparison.Ordinal) || itemName.Contains("Fragile_") || itemName.Contains("Unbreakable_")) return AccessConditionType.CharmsCount;

            return null;
        }
    }
}
