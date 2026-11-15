using System;
using Newtonsoft.Json;
using NightmareAccess.Integrations;
using RandomizerMod.Settings;

namespace NightmareAccess.Logic
{
    // NOTE: values are serialized into save files; never reorder or remove members.
    public enum AccessConditionType
    {
        // 1. Currencies & Quantities
        Geo,
        RancidEggs,
        Essence,
        Grubs,
        Roots,
        SimpleKeys,
        MaskShards,
        VesselFragments,
        PaleOre,
        Notches,
        Relics,
        CharmsCount,
        MapsCount,
        JournalCount,
        StagsCount,

        // 2. Movement abilities & Nail arts
        Dash,
        LeftDash,
        RightDash,
        WallJump,
        LeftWallJump,
        RightWallJump,
        Superdash,
        LeftSuperdash,
        RightSuperdash,
        DoubleJump,
        AcidArmour,
        DreamNailLevel,
        NailArt,

        // 3. Spells
        FireballLevel,
        QuakeLevel,
        ScreamLevel,

        // 4. Dreamers
        Dreamer,

        // 5. Special progression chains
        KingsoulLevel,
        GrimmchildLevel,

        // 6. Key items
        CityCrest,
        LumaflyLantern,

        // 7. Specific collectibles
        SpecificCharm,
        SpecificMap,
        SpecificJournalEntry,
        SpecificStag
    }

    public class AccessCondition
    {
        [JsonProperty] public AccessConditionType Type { get; set; }
        [JsonProperty] public int RequiredAmount { get; set; }
        [JsonProperty] public int SpecificId { get; set; }
        [JsonProperty] public string SpecificKey { get; set; }

        public AccessCondition() { }

        public AccessCondition(AccessConditionType type, int amount = 1, int specificId = 0, string specificKey = "")
        {
            Type = type;
            RequiredAmount = amount;
            SpecificId = specificId;
            SpecificKey = specificKey;
        }

        // ------------------------------------------------------------------
        // Classification
        // ------------------------------------------------------------------

        public static bool IsProgressiveType(AccessConditionType type) => type is
            AccessConditionType.KingsoulLevel or AccessConditionType.GrimmchildLevel or
            AccessConditionType.DreamNailLevel or AccessConditionType.FireballLevel or
            AccessConditionType.QuakeLevel or AccessConditionType.ScreamLevel;

        public static bool IsNumericType(AccessConditionType type) => type is
            AccessConditionType.Geo or AccessConditionType.RancidEggs or AccessConditionType.Essence or
            AccessConditionType.Grubs or AccessConditionType.Roots or AccessConditionType.SimpleKeys or
            AccessConditionType.MaskShards or AccessConditionType.VesselFragments or AccessConditionType.PaleOre or
            AccessConditionType.Notches or AccessConditionType.Relics or AccessConditionType.CharmsCount or
            AccessConditionType.MapsCount or AccessConditionType.JournalCount or AccessConditionType.StagsCount;

        public bool IsProgressive() => IsProgressiveType(Type);

        public bool IsNumeric() => IsNumericType(Type);

        // ------------------------------------------------------------------
        // Randomizer logic
        // ------------------------------------------------------------------

        public string ToLogicExpression(GenerationSettings gs)
        {
            if (IntegrationRegistry.TryOverrideLogicExpression(this, gs, out string customExpr))
            {
                return customExpr;
            }

            return Type switch
            {
                // Currencies & counters: to avoid SphereBuilder OutOfLocationsException (negative cap increment),
                // we require the prerequisite tool/item presence rather than multi-item sums.
                AccessConditionType.Geo => "TRUE",
                AccessConditionType.Essence => "DREAMNAIL",
                AccessConditionType.Roots => "DREAMNAIL",
                AccessConditionType.RancidEggs => "RANCIDEGGS",
                AccessConditionType.Grubs => "GRUBS",
                AccessConditionType.SimpleKeys => "SIMPLE",
                AccessConditionType.MaskShards => "MASKSHARDS",
                AccessConditionType.VesselFragments => "VESSELFRAGMENTS",
                AccessConditionType.PaleOre => "PALEORE",
                AccessConditionType.Notches => "NOTCHES",
                AccessConditionType.Relics => "(WANDERERSJOURNALS | HALLOWNESTSEALS | KINGSIDOLS | ARCANEEGGS)",
                AccessConditionType.CharmsCount => "CHARMS",
                AccessConditionType.MapsCount => "MAPS",
                AccessConditionType.JournalCount => "JOURNAL",
                AccessConditionType.StagsCount => "STAGS",

                // Movement abilities (macros.json: DASH, CLAW, SUPERDASH, WINGS, ACID)
                AccessConditionType.Dash => gs?.NoveltySettings?.SplitCloak == true ? "(LEFTDASH + RIGHTDASH)" : "DASH",
                AccessConditionType.LeftDash => "LEFTDASH",
                AccessConditionType.RightDash => "RIGHTDASH",
                AccessConditionType.WallJump => gs?.NoveltySettings?.SplitClaw == true ? "(LEFTCLAW + RIGHTCLAW)" : "CLAW",
                AccessConditionType.LeftWallJump => "LEFTCLAW",
                AccessConditionType.RightWallJump => "RIGHTCLAW",
                AccessConditionType.Superdash => gs?.NoveltySettings?.SplitSuperdash == true ? "(LEFTSUPERDASH + RIGHTSUPERDASH)" : "SUPERDASH",
                AccessConditionType.LeftSuperdash => "LEFTSUPERDASH",
                AccessConditionType.RightSuperdash => "RIGHTSUPERDASH",
                AccessConditionType.DoubleJump => "WINGS",
                AccessConditionType.AcidArmour => "ACID",
                AccessConditionType.DreamNailLevel => RequiredAmount switch
                {
                    1 => "DREAMNAIL",
                    2 => "DREAMGATE",
                    _ => "AWOKEN"
                },
                AccessConditionType.NailArt => SpecificKey switch
                {
                    "Cyclone" => "CYCLONE",
                    "Dash" => "DASHSLASH",
                    _ => "GREATSLASH"
                },

                // Spells
                AccessConditionType.FireballLevel => RequiredAmount == 1 ? "FIREBALL" : "FIREBALL>1",
                AccessConditionType.QuakeLevel => RequiredAmount == 1 ? "QUAKE" : "QUAKE>1",
                AccessConditionType.ScreamLevel => RequiredAmount == 1 ? "SCREAM" : "SCREAM>1",

                // Dreamers
                AccessConditionType.Dreamer => SpecificKey switch
                {
                    "Lurien" => "Lurien",
                    "Monomon" => "Monomon",
                    _ => "Herrah"
                },

                // Kingsoul / Void Heart (Rando 4 term is WHITEFRAGMENT)
                AccessConditionType.KingsoulLevel => RequiredAmount switch
                {
                    1 => "WHITEFRAGMENT",
                    2 => "WHITEFRAGMENT>1",
                    _ => "WHITEFRAGMENT>2"
                },

                // Grimmchild (flames only matter if the GrimmkinFlames pool is randomized)
                AccessConditionType.GrimmchildLevel => gs?.PoolSettings?.GrimmkinFlames == true
                    ? RequiredAmount switch { 1 => "Grimmchild", 2 => "Grimmchild + 3FLAMES", _ => "Grimmchild + 6FLAMES" }
                    : "Grimmchild",

                // Key items
                AccessConditionType.CityCrest => "CREST",
                AccessConditionType.LumaflyLantern => "LANTERN",

                // Specific collectibles
                AccessConditionType.SpecificCharm => ConditionCatalog.CharmLogicTerm(SpecificId),
                AccessConditionType.SpecificMap => ConditionCatalog.Maps.Find(SpecificKey)?.LogicTerm ?? "MAPS",
                AccessConditionType.SpecificJournalEntry => "JOURNAL",
                AccessConditionType.SpecificStag => ConditionCatalog.Stags.Find(SpecificKey)?.LogicTerm ?? "STAGS",

                _ => "TRUE"
            };
        }

        // ------------------------------------------------------------------
        // Runtime evaluation
        // ------------------------------------------------------------------

        public bool IsSatisfied(out int currentAmount)
        {
            return IsSatisfied(PlayerData.instance, out currentAmount);
        }

        internal bool IsSatisfied(PlayerData pd, out int currentAmount)
        {
            currentAmount = 0;
            if (pd == null) return false;

            int? amount = GetAmount(pd);
            if (amount == null) return true; // unknown types never lock anything

            currentAmount = amount.Value;
            return (IsNumeric() || IsProgressive()) ? currentAmount >= RequiredAmount : currentAmount > 0;
        }

        private static bool Has(string itemName) => ObtainedItems.Has(itemName);

        private static int Flag(bool value) => value ? 1 : 0;

        /// <summary>Counts for numeric conditions, levels for progressive ones, 0/1 for everything else.</summary>
        internal int? GetAmount(PlayerData pd)
        {
            return GetResourceAmount(pd)
                   ?? GetMovementAmount(pd)
                   ?? GetAbilityAndProgressionAmount(pd)
                   ?? GetSpecificCollectibleAmount(pd);
        }

        private int? GetResourceAmount(PlayerData pd) => Type switch
        {
            AccessConditionType.Geo => pd.geo,
            AccessConditionType.RancidEggs => pd.rancidEggs,
            AccessConditionType.Essence => pd.dreamOrbs,
            AccessConditionType.Grubs => pd.grubsCollected,
            AccessConditionType.Roots => ConditionCatalog.CountCompletedRoots(pd),
            AccessConditionType.SimpleKeys => IntegrationRegistry.TryGetNumericAmountOverride(AccessConditionType.SimpleKeys, pd, out int customSimpleKeys)
                ? customSimpleKeys
                : pd.simpleKeys + CountUsedSimpleKeys(pd),
            AccessConditionType.MaskShards => Math.Max(0, (pd.maxHealthBase - 5) * 4 + pd.heartPieces),
            AccessConditionType.VesselFragments => IntegrationRegistry.TryGetNumericAmountOverride(AccessConditionType.VesselFragments, pd, out int customVessels)
                ? customVessels
                : Math.Max(0, (pd.MPReserveMax / 33) * 3) + pd.vesselFragments,
            AccessConditionType.PaleOre => IntegrationRegistry.TryGetNumericAmountOverride(AccessConditionType.PaleOre, pd, out int customOre)
                ? customOre
                : pd.ore + CountSpentOre(pd),
            AccessConditionType.Notches => Math.Max(0, pd.charmSlots - 3),
            AccessConditionType.Relics => pd.trinket1 + pd.trinket2 + pd.trinket3 + pd.trinket4
                                         + pd.soldTrinket1 + pd.soldTrinket2 + pd.soldTrinket3 + pd.soldTrinket4,
            AccessConditionType.CharmsCount => ConditionCatalog.CountCharms(pd),
            AccessConditionType.MapsCount => ConditionCatalog.Maps.CountOwned(pd),
            AccessConditionType.JournalCount => pd.journalEntriesCompleted,
            AccessConditionType.StagsCount => pd.stationsOpened,
            _ => null
        };

        private int? GetMovementAmount(PlayerData pd) => Type switch
        {
            AccessConditionType.Dash => IntegrationRegistry.TryGetNumericAmountOverride(AccessConditionType.Dash, pd, out int customDash)
                ? customDash
                : Flag(pd.hasDash || pd.hasShadowDash || Has("Mothwing_Cloak") || Has("Shade_Cloak")),
            AccessConditionType.LeftDash => IntegrationRegistry.TryGetNumericAmountOverride(AccessConditionType.LeftDash, pd, out int customLeftDash)
                ? customLeftDash
                : Flag(Has("Left_Mothwing_Cloak") || pd.hasDash || pd.hasShadowDash),
            AccessConditionType.RightDash => IntegrationRegistry.TryGetNumericAmountOverride(AccessConditionType.RightDash, pd, out int customRightDash)
                ? customRightDash
                : Flag(Has("Right_Mothwing_Cloak") || pd.hasDash || pd.hasShadowDash),
            AccessConditionType.WallJump => Flag(pd.hasWalljump || Has("Mantis_Claw")),
            AccessConditionType.LeftWallJump => Flag(Has("Left_Mantis_Claw") || pd.hasWalljump),
            AccessConditionType.RightWallJump => Flag(Has("Right_Mantis_Claw") || pd.hasWalljump),
            AccessConditionType.Superdash => Flag(pd.hasSuperDash || Has("Crystal_Heart")),
            AccessConditionType.LeftSuperdash => Flag(Has("Left_Crystal_Heart") || pd.hasSuperDash),
            AccessConditionType.RightSuperdash => Flag(Has("Right_Crystal_Heart") || pd.hasSuperDash),
            AccessConditionType.DoubleJump => Flag(pd.hasDoubleJump || Has("Monarch_Wings")),
            AccessConditionType.AcidArmour => Flag(pd.hasAcidArmour || Has("Isma's_Tear")),
            _ => null
        };

        private int? GetAbilityAndProgressionAmount(PlayerData pd) => Type switch
        {
            AccessConditionType.DreamNailLevel => (pd.dreamNailUpgraded || Has("Awoken_Dream_Nail")) ? 3
                : (pd.hasDreamGate || Has("Dream_Gate")) ? 2
                : (pd.hasDreamNail || Has("Dream_Nail")) ? 1
                : 0,
            AccessConditionType.NailArt => Flag(SpecificKey switch
            {
                "Cyclone" => pd.hasCyclone,
                "Dash" => pd.hasDashSlash,
                _ => pd.hasUpwardSlash
            }),
            AccessConditionType.FireballLevel => GetSpellLevel(pd.fireballLevel, "Vengeful_Spirit", "Shade_Soul"),
            AccessConditionType.QuakeLevel => GetSpellLevel(pd.quakeLevel, "Desolate_Dive", "Descending_Dark"),
            AccessConditionType.ScreamLevel => GetSpellLevel(pd.screamLevel, "Howling_Wraiths", "Abyss_Shriek"),
            AccessConditionType.Dreamer => Flag(SpecificKey switch
            {
                "Lurien" => pd.lurienDefeated,
                "Monomon" => pd.monomonDefeated,
                _ => pd.hegemolDefeated
            }),
            AccessConditionType.KingsoulLevel => GetKingsoulLevel(pd),
            AccessConditionType.GrimmchildLevel => (pd.gotCharm_40 && (pd.grimmChildLevel == 5 || pd.destroyedNightmareLantern))
                ? 4
                : (pd.gotCharm_40 ? pd.grimmChildLevel : 0),
            AccessConditionType.CityCrest => Flag(pd.hasCityKey || Has("City_Crest")),
            AccessConditionType.LumaflyLantern => Flag(pd.hasLantern || Has("Lumafly_Lantern")),
            _ => null
        };

        private int? GetSpecificCollectibleAmount(PlayerData pd) => Type switch
        {
            AccessConditionType.SpecificCharm => SpecificId == ConditionCatalog.KingsoulCharmId
                ? Flag(GetKingsoulLevel(pd) >= 2)
                : Flag(GetCharmBool(pd, SpecificId)),
            AccessConditionType.SpecificMap => Flag(ConditionCatalog.Maps.Find(SpecificKey)?.IsOwned(pd) == true),
            AccessConditionType.SpecificJournalEntry => Flag(ConditionCatalog.Journal.Find(SpecificKey)?.IsOwned(pd) == true),
            AccessConditionType.SpecificStag => Flag(ConditionCatalog.Stags.Find(SpecificKey)?.IsOwned(pd) == true),
            _ => null
        };

        private static bool GetCharmBool(PlayerData pd, int charmId)
        {
            if (NightmareAccess.Instance != null)
            {
                return GetCharmBoolViaPlayerData(pd, charmId);
            }

            var field = typeof(PlayerData).GetField(
                $"gotCharm_{charmId}",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(pd) is bool b && b;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static bool GetCharmBoolViaPlayerData(PlayerData pd, int charmId) =>
            pd.GetBool($"gotCharm_{charmId}");

        private static int CountUsedSimpleKeys(PlayerData pd)
        {
            int used = 0;
            if (pd.openedWaterwaysManhole) used++;
            if (pd.jijiDoorUnlocked) used++;
            if (pd.bathHouseOpened) used++;
            if (pd.bossDoorCageUnlocked) used++;
            return used;
        }

        private static int CountSpentOre(PlayerData pd) => pd.nailSmithUpgrades switch
        {
            2 => 1,
            3 => 3,
            4 => 6,
            _ => 0
        };

        private static int GetSpellLevel(int playerDataLevel, string level1Item, string level2Item)
        {
            int level = playerDataLevel;
            if (level == 0 && (Has(level1Item) || Has(level2Item))) level = 1;
            if (Has(level2Item)) level = 2;
            return level;
        }

        private static int GetKingsoulLevel(PlayerData pd)
        {
            if (pd.royalCharmState == 4 || Has("Void_Heart")) return 3;
            if ((pd.gotCharm_36 && pd.royalCharmState >= 3) || (pd.gotQueenFragment && pd.gotKingFragment)) return 2;
            if (pd.gotQueenFragment || pd.gotKingFragment || Has("Queen_Fragment") || Has("King_Fragment")) return 1;
            return 0;
        }

        // ------------------------------------------------------------------
        // Display text
        // ------------------------------------------------------------------

        private string Have(string singular, string plural = null) =>
            $"have {RequiredAmount} {(RequiredAmount > 1 ? plural ?? singular : singular)}";

        public string GetRequirementText()
        {
            return Type switch
            {
                AccessConditionType.Geo => Have("geo"),
                AccessConditionType.RancidEggs => Have("egg", "eggs"),
                AccessConditionType.Essence => Have("essence"),
                AccessConditionType.Grubs => Have("grub", "grubs"),
                AccessConditionType.Roots => Have("dream tree", "dream trees"),
                AccessConditionType.SimpleKeys => Have("simple key", "simple keys"),
                AccessConditionType.MaskShards => Have("mask shard", "mask shards"),
                AccessConditionType.VesselFragments => Have("vessel frag", "vessel frags"),
                AccessConditionType.PaleOre => Have("pale ore"),
                AccessConditionType.Notches => Have("charm notch", "charm notches"),
                AccessConditionType.Relics => Have("relic", "relics"),
                AccessConditionType.CharmsCount => Have("charm", "charms"),
                AccessConditionType.MapsCount => Have("map", "maps"),
                AccessConditionType.JournalCount => Have("journal entry", "journal entries"),
                AccessConditionType.StagsCount => Have("stag station", "stag stations"),

                AccessConditionType.Dash => "have Dash",
                AccessConditionType.LeftDash => "have Left Dash",
                AccessConditionType.RightDash => "have Right Dash",
                AccessConditionType.WallJump => "have Mantis Claw",
                AccessConditionType.LeftWallJump => "have Left Mantis Claw",
                AccessConditionType.RightWallJump => "have Right Mantis Claw",
                AccessConditionType.Superdash => "have Crystal Heart",
                AccessConditionType.LeftSuperdash => "have Left Superdash",
                AccessConditionType.RightSuperdash => "have Right Superdash",
                AccessConditionType.DoubleJump => "have Monarch Wings",
                AccessConditionType.AcidArmour => "have Isma's Tear",
                AccessConditionType.DreamNailLevel => RequiredAmount switch
                {
                    1 => "have Dream Nail (Lv1)",
                    2 => "have Dream Gate (Lv2)",
                    _ => "have Awoken Dream Nail (Lv3)"
                },
                AccessConditionType.NailArt => SpecificKey switch
                {
                    "Cyclone" => "have Cyclone Slash",
                    "Dash" => "have Dash Slash",
                    _ => "have Great Slash"
                },

                AccessConditionType.FireballLevel => RequiredAmount == 1 ? "have Vengeful Spirit (Lv1)" : "have Shade Soul (Lv2)",
                AccessConditionType.QuakeLevel => RequiredAmount == 1 ? "have Desolate Dive (Lv1)" : "have Descending Dark (Lv2)",
                AccessConditionType.ScreamLevel => RequiredAmount == 1 ? "have Howling Wraiths (Lv1)" : "have Abyss Shriek (Lv2)",

                AccessConditionType.Dreamer => $"have {SpecificKey}",
                AccessConditionType.KingsoulLevel => RequiredAmount switch
                {
                    1 => "have Kingsoul Fragment (Lv1)",
                    2 => "have Kingsoul (Lv2)",
                    _ => "have Void Heart (Lv3)"
                },
                AccessConditionType.GrimmchildLevel => RequiredAmount == 4
                    ? "have Grimmchild Lv4 (or Carefree Melody)"
                    : $"have Grimmchild Lv{RequiredAmount}",

                AccessConditionType.CityCrest => "have City Crest",
                AccessConditionType.LumaflyLantern => "have Lumafly Lantern",

                AccessConditionType.SpecificCharm => ConditionCatalog.CharmDisplayName(SpecificId) is { } charm
                    ? $"have {charm}"
                    : "have Charm",
                AccessConditionType.SpecificMap => $"have {FormatMapName(SpecificKey)} Map",
                AccessConditionType.SpecificJournalEntry => SpecificKey == ConditionCatalog.JournalItemKey
                    ? "have Hunter's Journal"
                    : $"have {FormatJournalName(SpecificKey)} Entry",
                AccessConditionType.SpecificStag => $"have {FormatStagName(SpecificKey)} Stag",

                _ => "Nightmare Seal"
            };
        }

        public static string FormatMapName(string key) => ConditionCatalog.Maps.Find(key)?.DisplayName ?? key;

        public static string FormatStagName(string key) => ConditionCatalog.Stags.Find(key)?.DisplayName ?? key;

        public static string FormatJournalName(string key) => ConditionCatalog.Journal.Find(key)?.DisplayName ?? key;
    }
}