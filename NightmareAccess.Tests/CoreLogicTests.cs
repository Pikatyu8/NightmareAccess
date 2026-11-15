using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using ItemChanger;
using ItemChanger.Locations;
using ItemChanger.Placements;
using NightmareAccess.InGame;
using NightmareAccess.Integrations;
using NightmareAccess.Interop;
using NightmareAccess.Logic;
using NightmareAccess.Settings;
using RandomizerMod.RandomizerData;
using RandomizerMod.RC;
using RandomizerMod.Settings;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace NightmareAccess.Tests
{
    public sealed class CoreLogicTests : IDisposable
    {
        private readonly HashSet<string> _obtained = new(StringComparer.Ordinal);

        public CoreLogicTests()
        {
            ResetOverrides();
            ObtainedItems.OverrideForTests = name => _obtained.Contains(name);
            NightmareLogicHandler.IsSaveActive = true;
        }

        public void Dispose()
        {
            ResetOverrides();
            ObtainedItems.OverrideForTests = null;
            NightmareLogicHandler.IsSaveActive = false;
        }

        private static void ResetOverrides()
        {
            YarcInterop.DreamOrbsOverrideForTests = () => false;
            YarcInterop.PoolOverrideForTests = null;
            AccessRandomizerInterop.IsLoadedOverrideForTests = null;
            AccessRandomizerInterop.PoolOverrideForTests = null;
            AccessRandomizerInterop.UniqueKeyCountOverrideForTests = null;
            CombatRandomizerInterop.IsLoadedOverrideForTests = null;
            CombatRandomizerInterop.FragmentsPerNotchOverrideForTests = null;
            TheGloryOfBeingAFoolInterop.IsLoadedOverrideForTests = null;
            TheGloryOfBeingAFoolInterop.EnabledOverrideForTests = null;
            FishingInterop.IsLoadedOverrideForTests = null;
            FishingInterop.EnabledOverrideForTests = null;
            MoreStagsInterop.IsLoadedOverrideForTests = null;
            MoreStagsInterop.ActiveStagsOverrideForTests = null;
            BenchRandoInterop.IsLoadedOverrideForTests = null;
            BenchRandoInterop.RandomizedBenchesOverrideForTests = null;
            MoreLocationsInterop.IsLoadedOverrideForTests = null;
            MoreLocationsInterop.MiscOverrideForTests = null;
            CurseRandomizerInterop.IsLoadedOverrideForTests = null;
            CurseRandomizerInterop.WalletCapacitiesOverrideForTests = null;
            CurseRandomizerInterop.CursedVesselCountOverrideForTests = null;
            CurseRandomizerInterop.SavedCursedVesselOverrideForTests = null;
            CurseRandomizerInterop.CurrentSoulVesselOverrideForTests = null;
            RandoPlusInterop.IsLoadedOverrideForTests = null;
            RandoPlusInterop.PoolOverrideForTests = null;
            RandoPlusInterop.NailUpgradesInSaveOverrideForTests = null;
            RandoPlusInterop.AreaBlitzInvalidLocationsOverrideForTests = null;
            BreakableWallRandomizerInterop.IsLoadedOverrideForTests = null;
            BreakableWallRandomizerInterop.WallSettingOverrideForTests = null;
            ExtraRandoInterop.IsLoadedOverrideForTests = null;
            ExtraRandoInterop.SettingOverrideForTests = null;
            ExtraRandoInterop.SplitDashStateOverrideForTests = null;
            RopeRandoInterop.IsLoadedOverrideForTests = null;
            RopeRandoInterop.EnabledOverrideForTests = null;
            CorpseDreamRandoInterop.IsLoadedOverrideForTests = null;
            CorpseDreamRandoInterop.EnabledOverrideForTests = null;
            FlowerRandomizerInterop.IsLoadedOverrideForTests = null;
            FlowerRandomizerInterop.EnabledOverrideForTests = null;
            FlowerRandomizerInterop.QuestEnabledOverrideForTests = null;
            GodhomeRandomizerInterop.IsLoadedOverrideForTests = null;
            GodhomeRandomizerInterop.ConfigOverrideForTests = null;
            LoreRandomizerInterop.IsLoadedOverrideForTests = null;
            LoreRandomizerInterop.ConfigOverrideForTests = null;
            CornerPromptHelper.PlayerDataOverrideForTests = null;

            NightmareLogicHandler.LocationConditions.Clear();
            NightmareLogicHandler.ItemTypeConditions.Clear();
            NightmareLogicHandler.ObjectConditions.Clear();
            NightmareLogicHandler.IntegrationInts.Clear();
        }

        private static PlayerData CreateFreshPlayerData()
        {
            var pd = (PlayerData)FormatterServices.GetUninitializedObject(typeof(PlayerData));
            pd.maxHealthBase = 5;
            pd.health = 5;
            pd.maxHealth = 5;
            pd.charmSlots = 3;
            return pd;
        }

        [Fact]
        public void Settings_NormalizeAndCopyFrom_FixesInvertedBoundsAndClampsChance()
        {
            var src = new NightmareAccessSettings
            {
                Enabled = true,
                Scope = RequirementScope.PerObject,
                RequirementChance = 150,
                MinGeo = 900,
                MaxGeo = 100,
                MinGrubs = 20,
                MaxGrubs = 3
            };
            src.Normalize();

            Assert.Equal(100, src.RequirementChance);
            Assert.Equal(100, src.MinGeo);
            Assert.Equal(900, src.MaxGeo);
            Assert.Equal(3, src.MinGrubs);
            Assert.Equal(20, src.MaxGrubs);

            var dst = new NightmareAccessSettings();
            dst.CopyFrom(src);
            Assert.True(dst.Enabled);
            Assert.Equal(RequirementScope.PerObject, dst.Scope);
            Assert.Equal(100, dst.MinGeo);
            Assert.Equal(900, dst.MaxGeo);
        }

        [Fact]
        public void AccessCondition_AllTypesHaveDisplayTextAndLogicExpression()
        {
            var gs = new GenerationSettings();
            gs.PoolSettings.GrimmkinFlames = true;
            gs.NoveltySettings.SplitCloak = true;
            gs.NoveltySettings.SplitClaw = true;
            gs.NoveltySettings.SplitSuperdash = true;

            foreach (AccessConditionType type in Enum.GetValues(typeof(AccessConditionType)))
            {
                var cond = new AccessCondition(type, amount: 2, specificId: 5, specificKey: "Crossroads");
                string text = cond.GetRequirementText();
                string expr = cond.ToLogicExpression(gs);

                Assert.False(string.IsNullOrWhiteSpace(text));
                Assert.False(string.IsNullOrWhiteSpace(expr));
            }

            Assert.Equal("(LEFTDASH + RIGHTDASH)", new AccessCondition(AccessConditionType.Dash).ToLogicExpression(gs));
            Assert.Equal("Grimmchild + 3FLAMES", new AccessCondition(AccessConditionType.GrimmchildLevel, 2).ToLogicExpression(gs));
            Assert.Equal("WHITEFRAGMENT>1", new AccessCondition(AccessConditionType.SpecificCharm, 1, ConditionCatalog.KingsoulCharmId).ToLogicExpression(gs));
        }

        [Theory]
        [InlineData(3, 3, 0)]
        [InlineData(4, 0, 0)]
        [InlineData(5, 0, 0)]
        [InlineData(5, 2, 2)]
        [InlineData(6, 1, 5)]
        public void AccessCondition_MaskShards_IsMonotonicWithCursedMasks(int maxHealthBase, int heartPieces, int expectedShards)
        {
            PlayerData pd = CreateFreshPlayerData();
            pd.maxHealthBase = maxHealthBase;
            pd.heartPieces = heartPieces;

            var cond = new AccessCondition(AccessConditionType.MaskShards, expectedShards == 0 ? 1 : expectedShards);
            bool satisfied = cond.IsSatisfied(pd, out int current);

            Assert.Equal(expectedShards, current);
            Assert.Equal(expectedShards > 0, satisfied);
        }

        [Fact]
        public void AccessCondition_IsSatisfied_HandlesResourcesAndItemsCorrectly()
        {
            PlayerData pd = CreateFreshPlayerData();
            pd.geo = 450;
            pd.simpleKeys = 1;
            pd.openedWaterwaysManhole = true;
            pd.jijiDoorUnlocked = true;
            pd.ore = 1;
            pd.nailSmithUpgrades = 3; // +3 spent ore -> total 4
            pd.charmSlots = 6;        // 6 - 3 = 3 notches
            pd.trinket1 = 2;
            pd.soldTrinket4 = 1;
            _obtained.Add("Whispering_Root-Crossroads");
            pd.scenesEncounteredDreamPlantC = new List<string> { "Fungus1_13" };
            pd.hasDreamGate = true;
            pd.gotCharm_40 = true;
            pd.grimmChildLevel = 5;   // Carefree Melody -> Lv4
            pd.mapCity = true;
            pd.openedTown = true;
            pd.killedWorm = true;

            Assert.True(new AccessCondition(AccessConditionType.Geo, 450).IsSatisfied(pd, out _));
            Assert.False(new AccessCondition(AccessConditionType.Geo, 451).IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.SimpleKeys, 3).IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.PaleOre, 4).IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.Notches, 3).IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.Relics, 3).IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.Roots, 2).IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.DreamNailLevel, 2).IsSatisfied(pd, out _));
            Assert.False(new AccessCondition(AccessConditionType.DreamNailLevel, 3).IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.GrimmchildLevel, 4).IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.SpecificMap, 1, 0, "City").IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.SpecificStag, 1, 0, "Dirtmouth").IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.SpecificJournalEntry, 1, 0, "Goam").IsSatisfied(pd, out _));

            _obtained.Add("Journal_Entry-Garpede");
            Assert.True(new AccessCondition(AccessConditionType.SpecificJournalEntry, 1, 0, "Garpede").IsSatisfied(pd, out _));

            _obtained.Add("Queen_Fragment");
            Assert.True(new AccessCondition(AccessConditionType.KingsoulLevel, 1).IsSatisfied(pd, out _));
            Assert.False(new AccessCondition(AccessConditionType.SpecificCharm, 1, ConditionCatalog.KingsoulCharmId).IsSatisfied(pd, out _));

            pd.gotCharm_36 = true;
            pd.royalCharmState = 3;
            Assert.True(new AccessCondition(AccessConditionType.SpecificCharm, 1, ConditionCatalog.KingsoulCharmId).IsSatisfied(pd, out _));
        }

        [Fact]
        public void ConditionCatalog_And_CostFind_WorkAsExpected()
        {
            Assert.DoesNotContain("HuntersMark", ConditionCatalog.RollableJournalKeys);
            Assert.Equal("WHITEFRAGMENT>1", ConditionCatalog.CharmLogicTerm(ConditionCatalog.KingsoulCharmId));
            Assert.Equal("Wayward_Compass", ConditionCatalog.CharmLogicTerm(2));

            var naCost = new NightmareAccessCost(new AccessCondition(AccessConditionType.Geo, 100));
            var multi = new MultiCost(new GeoCost(300), naCost);

            Assert.Same(naCost, NightmareAccessCost.Find(naCost));
            Assert.Same(naCost, NightmareAccessCost.Find(multi));
            Assert.Null(NightmareAccessCost.Find(new GeoCost(300)));
            Assert.False(naCost.HasPayEffects());
            Assert.True(multi.HasPayEffects());
        }

        [Fact]
        public void LogicHandler_ExclusionsAndSelfConflicts_CoverUnsupportedLocationsAndSettings()
        {
            Assert.True(NightmareLogicHandler.IsExcludedLocation("Start"));
            Assert.True(NightmareLogicHandler.IsExcludedLocation("Boss_Essence-Xero"));
            Assert.True(NightmareLogicHandler.IsExcludedLocation("Grimmkin_Flame-City_Storerooms"));
            Assert.True(NightmareLogicHandler.IsExcludedLocation("Rancid_Egg-Tuk_Defender's_Crest"));
            Assert.True(NightmareLogicHandler.IsExcludedLocation("Stalactite-King's_Pass_Hidden"));
            Assert.False(NightmareLogicHandler.IsExcludedLocation("Vine-Root_Left"));
            Assert.False(NightmareLogicHandler.IsExcludedLocation("Chain-Storerooms"));
            Assert.False(NightmareLogicHandler.IsExcludedLocation("Hive_Platform-Lower_East"));
            Assert.False(NightmareLogicHandler.IsExcludedLocation("Scarecrow"));
            Assert.False(NightmareLogicHandler.IsExcludedLocation("Stalactite-Hornet_1"));
            Assert.False(NightmareLogicHandler.IsExcludedLocation("Lurien's_Telescope"));
            Assert.False(NightmareLogicHandler.IsExcludedLocation("Soul_Jar-Shade_Soul"));
            Assert.False(NightmareLogicHandler.IsExcludedLocation("Love_Jar-Outer_Lower"));
            Assert.False(NightmareLogicHandler.IsExcludedLocation("DreamOrb_Crossroads_1"));
            Assert.False(NightmareLogicHandler.IsExcludedLocation("Egg_Bomb-Map_Right"));
            Assert.False(NightmareLogicHandler.IsExcludedLocation("Whispering_Root-Crossroads"));

            YarcInterop.DreamOrbsOverrideForTests = () => true;
            Assert.True(NightmareLogicHandler.IsExcludedLocation("Whispering_Root-Crossroads"));
            YarcInterop.DreamOrbsOverrideForTests = () => false;

            Assert.True(NightmareLogicHandler.IsAlwaysConflictingObjectType("Mantis_Claw", AccessConditionType.WallJump, false));
            Assert.True(NightmareLogicHandler.IsAlwaysConflictingObjectType("Gathering_Swarm", AccessConditionType.SpecificCharm, true));
            Assert.True(NightmareLogicHandler.IsSpecificSelfConflict("Journal_Entry-Goam", new AccessCondition(AccessConditionType.SpecificJournalEntry, 1, 0, "Goam")));
            Assert.True(NightmareLogicHandler.IsSpecificSelfConflict("Cyclone_Slash", new AccessCondition(AccessConditionType.NailArt, 1, 0, "Cyclone")));
        }

        [Fact]
        public void LogicHandler_RollRespectsRemoveSpellUpgrades_ReplaceJunk_AndGrimmchildCap()
        {
            var settings = new NightmareAccessSettings();
            var gs = new GenerationSettings();
            gs.CursedSettings.RemoveSpellUpgrades = true;
            gs.CursedSettings.ReplaceJunkWithOneGeo = true;

            List<AccessConditionType> allowed = NightmareLogicHandler.GetAllowedTypes(settings, gs);
            Assert.Contains(AccessConditionType.Geo, allowed);
            Assert.DoesNotContain(AccessConditionType.MaskShards, allowed);
            Assert.DoesNotContain(AccessConditionType.PaleOre, allowed);
            Assert.DoesNotContain(AccessConditionType.Relics, allowed);

            var rng = new Random(42);
            for (int i = 0; i < 30; i++)
            {
                var fb = NightmareLogicHandler.RollCondition(rng, new List<AccessConditionType> { AccessConditionType.FireballLevel }, settings, gs);
                var gc = NightmareLogicHandler.RollCondition(rng, new List<AccessConditionType> { AccessConditionType.GrimmchildLevel }, settings, gs);
                var je = NightmareLogicHandler.RollCondition(rng, new List<AccessConditionType> { AccessConditionType.SpecificJournalEntry }, settings, gs);

                Assert.Equal(1, fb.RequiredAmount);
                Assert.InRange(gc.RequiredAmount, 1, 3);
                Assert.NotEqual("HuntersMark", je.SpecificKey);
            }
        }

        [Fact]
        public void ColosseumPrompt_AndSpoilerLog_FormatExpectedOutput()
        {
            Assert.NotNull(ColosseumPrompt.IdentifyTrial("TRIAL_BOARD_BRONZE", "begin the Trial of the Warrior"));
            Assert.NotNull(ColosseumPrompt.IdentifyTrial("", "Испытание Глупца\nPlace a mark"));

            NightmareLogicHandler.LocationConditions["Charm_Notch-Colosseum"] = new AccessCondition(AccessConditionType.Grubs, 5);
            string formatted = ColosseumPrompt.ProcessPrompt("TRIAL_BOARD_BRONZE", "Trial of the Warrior\nPlace a mark to begin?");
            Assert.Contains("Requirement: have 5 grubs", formatted);

            // Direct match for TheGloryOfBeingAFoolRandomizer's Colosseum 3 location name
            NightmareLogicHandler.LocationConditions["The_Glory_of_Being_a_Fool-Colosseum"] = new AccessCondition(AccessConditionType.Essence, 300);
            string colo3 = ColosseumPrompt.ProcessPrompt("TRIAL_BOARD_GOLD", "Place a mark to begin?");
            Assert.Contains("Requirement: have 300 essence", colo3);

            var report = new WorldVerificationReport
            {
                Scope = RequirementScope.PerLocation,
                BaselineLocationsCount = 10,
                VerifiedLocationsCount = 10,
                BaselineWaypointsCount = 5,
                VerifiedWaypointsCount = 5,
                BaselineTransitionsCount = 2,
                VerifiedTransitionsCount = 2,
                RepairedCount = 1
            };
            string spoiler = NightmareSpoilerLog.BuildSpoilerText(null, RequirementScope.PerLocation, 100, report);
            Assert.Contains("=== World Verification ===", spoiler);
            Assert.Contains("PASS", spoiler);
            Assert.Contains("Charm_Notch-Colosseum: have 5 grubs", spoiler);
        }

        private static NightmareAccessSettings CreateAllDisabledSettings()
        {
            var settings = new NightmareAccessSettings { Enabled = true };
            foreach (PropertyInfo prop in typeof(NightmareAccessSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.PropertyType == typeof(bool) && prop.Name.StartsWith("Allow", StringComparison.Ordinal))
                {
                    prop.SetValue(settings, false, null);
                }
            }
            return settings;
        }

        [Fact]
        public void GetAllowedTypes_CollectibleSubTogglesDisabled_NeverBreaksAndRespectsGeoFallback()
        {
            var gs = new GenerationSettings();
            gs.PoolSettings.Charms = true;
            gs.PoolSettings.Maps = true;
            gs.PoolSettings.JournalEntries = true;
            gs.PoolSettings.Stags = true;
            gs.PoolSettings.GeoChests = false;
            gs.PoolSettings.GeoRocks = false;
            gs.CursedSettings.ReplaceJunkWithOneGeo = false;

            NightmareAccessSettings noGeoSettings = CreateAllDisabledSettings();
            noGeoSettings.AllowCharms = true;
            noGeoSettings.AllowMaps = true;
            noGeoSettings.AllowJournal = true;
            noGeoSettings.AllowStags = true;

            List<AccessConditionType> emptyAllowed = NightmareLogicHandler.GetAllowedTypes(noGeoSettings, gs);
            Assert.Empty(emptyAllowed);
            Assert.Null(NightmareLogicHandler.RollCondition(new Random(1), emptyAllowed, noGeoSettings, gs));
            Assert.Null(NightmareLogicHandler.RollObjectCondition(new Random(1), emptyAllowed, noGeoSettings, gs, "Mantis_Claw", false));
            Assert.Null(NightmareLogicHandler.RerollExcluding(new Random(1), emptyAllowed, noGeoSettings, gs, AccessConditionType.Geo));

            NightmareAccessSettings geoFallbackSettings = CreateAllDisabledSettings();
            geoFallbackSettings.AllowGeo = true;
            geoFallbackSettings.AllowCharms = true;
            geoFallbackSettings.AllowMaps = true;
            geoFallbackSettings.AllowJournal = true;
            geoFallbackSettings.AllowStags = true;

            List<AccessConditionType> geoAllowed = NightmareLogicHandler.GetAllowedTypes(geoFallbackSettings, gs);
            AccessConditionType single = Assert.Single(geoAllowed);
            Assert.Equal(AccessConditionType.Geo, single);

            NightmareAccessSettings inconsistentSettings = CreateAllDisabledSettings();
            inconsistentSettings.AllowGeo = true;
            inconsistentSettings.AllowCharms = false;
            inconsistentSettings.AllowSpecificCharms = true;
            inconsistentSettings.AllowCharmsCount = true;

            List<AccessConditionType> onlyGeo = NightmareLogicHandler.GetAllowedTypes(inconsistentSettings, gs);
            Assert.DoesNotContain(AccessConditionType.SpecificCharm, onlyGeo);
            Assert.DoesNotContain(AccessConditionType.CharmsCount, onlyGeo);
        }

        private sealed class FakeMenuToggle
        {
            public bool Value { get; private set; }
            public bool Locked { get; private set; }
            public event Action<bool> SelfChanged;

            public void SetValue(bool val)
            {
                if (Locked) return;
                Value = val;
                SelfChanged?.Invoke(val);
            }

            public void UserClick() => SetValue(!Value);
            public void Lock() => Locked = true;
            public void Unlock() => Locked = false;
        }

        [Fact]
        public void FlexibleGroupController_SynchronizesMasterAndChildrenWithoutLoops()
        {
            var settings = new NightmareAccessSettings
            {
                AllowCharms = true,
                AllowSpecificCharms = true,
                AllowCharmsCount = true
            };

            var master = new FakeMenuToggle();
            var specific = new FakeMenuToggle();
            var count = new FakeMenuToggle();

            var controller = new ConnectionMenu.FlexibleGroupController(
                () => settings.AllowCharms, v => settings.AllowCharms = v,
                () => settings.AllowSpecificCharms, v => settings.AllowSpecificCharms = v,
                () => settings.AllowCharmsCount, v => settings.AllowCharmsCount = v,
                val => { if (master.Locked) master.Unlock(); master.SetValue(val); },
                val => specific.SetValue(val),
                val => count.SetValue(val),
                locked =>
                {
                    if (locked) { specific.Lock(); count.Lock(); }
                    else { specific.Unlock(); count.Unlock(); }
                });

            controller.InitializeUi();
            master.SelfChanged += controller.OnMasterChanged;
            specific.SelfChanged += controller.OnSpecificChanged;
            count.SelfChanged += controller.OnCountChanged;

            Assert.True(master.Value);
            Assert.True(specific.Value);
            Assert.True(count.Value);
            Assert.False(specific.Locked);
            Assert.False(count.Locked);

            specific.UserClick();
            Assert.False(settings.AllowSpecificCharms);
            Assert.True(settings.AllowCharms);
            Assert.True(master.Value);

            count.UserClick();
            Assert.False(settings.AllowCharmsCount);
            Assert.False(settings.AllowCharms);
            Assert.False(master.Value);
            Assert.True(specific.Locked);
            Assert.True(count.Locked);

            specific.UserClick();
            Assert.False(specific.Value);
            Assert.False(settings.AllowSpecificCharms);

            master.UserClick();
            Assert.True(settings.AllowCharms);
            Assert.True(settings.AllowSpecificCharms);
            Assert.True(settings.AllowCharmsCount);
            Assert.True(master.Value);
            Assert.True(specific.Value);
            Assert.True(count.Value);
            Assert.False(specific.Locked);
            Assert.False(count.Locked);

            master.UserClick();
            Assert.False(settings.AllowCharms);
            Assert.True(specific.Locked);

            settings.AllowCharms = true;
            settings.AllowSpecificCharms = true;
            settings.AllowCharmsCount = false;
            controller.Refresh();

            Assert.True(master.Value);
            Assert.True(specific.Value);
            Assert.False(count.Value);
            Assert.False(specific.Locked);
            Assert.False(count.Locked);
        }

        [Fact]
        public void IntegrationRegistry_RoutesYarcHooksCleanly()
        {
            Assert.Equal("Soul", NightmareLogicHandler.GetItemTypeGroup("Soul_Jar"));
            Assert.Equal("Essence", NightmareLogicHandler.GetItemTypeGroup("Essence_Orb"));
            Assert.Equal(AccessConditionType.Essence, NightmareLogicHandler.GetItemResourceType("Essence_Orb"));
            Assert.True(NightmareLogicHandler.IsAlwaysConflictingObjectType("Essence_Orb", AccessConditionType.Essence, false));
            Assert.True(NightmareLogicHandler.IsConflictingCondition(AccessConditionType.Essence, "DreamOrb_Crossroads_1"));

            var eggPlacement = new YNShinyPlacement("Egg_Bomb-Map_Right")
            {
                Location = new CoordinateLocation { name = "Egg_Bomb-Map_Right", sceneName = "Fungus3_25b" },
                Cost = new NightmareAccessCost(new AccessCondition(AccessConditionType.Grubs, 3))
            };
            var vanillaPlacement = new YNShinyPlacement("Mantis_Claw")
            {
                Location = new CoordinateLocation { name = "Mantis_Claw", sceneName = "Fungus2_14" },
                Cost = new NightmareAccessCost(new AccessCondition(AccessConditionType.Grubs, 3))
            };

            Assert.True(IntegrationRegistry.ShouldBlockAutoGive(
                eggPlacement,
                new GiveInfo { Container = Container.Unknown, FlingType = FlingType.DirectDeposit }));
            Assert.False(IntegrationRegistry.ShouldBlockAutoGive(
                vanillaPlacement,
                new GiveInfo { Container = Container.Unknown, FlingType = FlingType.DirectDeposit }));

            Assert.Equal("Vines", NightmareLogicHandler.GetItemTypeGroup("Vine-Root_Left"));
            Assert.Equal("Vines", NightmareLogicHandler.GetItemTypeGroup("Chain-Storerooms"));
            Assert.Equal("EggBombs", NightmareLogicHandler.GetItemTypeGroup("Jelly_Egg_Bomb"));
            Assert.Equal("LoveJars", NightmareLogicHandler.GetItemTypeGroup("Love_Jar-Empty"));
            Assert.Equal("LoveJars", NightmareLogicHandler.GetItemTypeGroup("Love_Jar-Crawlid"));
            Assert.Equal("Scarecrow", NightmareLogicHandler.GetItemTypeGroup("Scarecrow"));
            Assert.Equal("Lurien's_Telescope", NightmareLogicHandler.GetItemTypeGroup("Lurien's_Telescope"));

            string[] inPlaceYarcLocs =
            {
                "DreamOrb_Crossroads_1",
                "Vine-Root_Left",
                "Chain-Storerooms",
                "Hive_Platform-Lower_East",
                "Scarecrow",
                "Stalactite-Hornet_1",
                "Lurien's_Telescope"
            };
            var naCost = new NightmareAccessCost(new AccessCondition(AccessConditionType.Grubs, 2));

            foreach (string loc in inPlaceYarcLocs)
            {
                var ap = new AutoPlacement(loc) { Location = new ItemChanger.Locations.EmptyLocation { name = loc } };
                var map = new Dictionary<string, AbstractPlacement> { [loc] = ap };
                Assert.True(CostApplier.CanPlacementAcceptCost(loc, ap));
                CostApplier.ApplyCost(loc, ap, naCost, map);
                Assert.Same(ap, map[loc]);
            }

            foreach (string jarLoc in new[] { "Soul_Jar-Shade_Soul", "Love_Jar-Outer_Lower" })
            {
                var mp = new MutablePlacement(jarLoc)
                {
                    Location = new ObjectLocation { name = jarLoc, objectName = "Break Jar", sceneName = "Ruins2_11_b" }
                };
                var map = new Dictionary<string, AbstractPlacement> { [jarLoc] = mp };
                Assert.True(CostApplier.CanPlacementAcceptCost(jarLoc, mp));
                CostApplier.ApplyCost(jarLoc, mp, naCost, map);
                var yn = Assert.IsType<YNShinyPlacement>(map[jarLoc]);
                Assert.Same(naCost, NightmareAccessCost.Find(yn.Cost));
            }
        }

        [Fact]
        public void IntegrationRegistry_RoutesAccessRandomizerHooksAndUniqueKeysCleanly()
        {
            Assert.Equal("SimpleKeys", NightmareLogicHandler.GetItemTypeGroup("Graveyard_Key"));
            Assert.Equal("SimpleKeys", NightmareLogicHandler.GetItemTypeGroup("Waterways_Key"));
            Assert.Equal("SimpleKeys", NightmareLogicHandler.GetItemTypeGroup("Pleasure_Key"));
            Assert.Equal("SimpleKeys", NightmareLogicHandler.GetItemTypeGroup("Coffin_Key"));
            Assert.Equal(AccessConditionType.SimpleKeys, NightmareLogicHandler.GetItemResourceType("Graveyard_Key"));
            Assert.Equal("Tram_Pass", NightmareLogicHandler.GetItemTypeGroup("Upper_Tram_Pass"));
            Assert.Equal("Tram_Pass", NightmareLogicHandler.GetItemTypeGroup("Lower_Tram_Pass"));
            Assert.Equal("Elevator_Pass", NightmareLogicHandler.GetItemTypeGroup("Left_Elevator_Pass"));
            Assert.Equal("Elevator_Pass", NightmareLogicHandler.GetItemTypeGroup("Right_Elevator_Pass"));
            Assert.Equal("Shade_Gates", NightmareLogicHandler.GetItemTypeGroup("Shade_Gate-Fog_Canyon"));
            Assert.Equal("Mantis_Respect", NightmareLogicHandler.GetItemTypeGroup("Mantis_Respect"));
            Assert.Equal("Hollow_Knight_Chain", NightmareLogicHandler.GetItemTypeGroup("Hollow_Knight_Chain"));
            Assert.Equal("Trap_Bench", NightmareLogicHandler.GetItemTypeGroup("Trap_Bench"));
            Assert.Equal("Mapper_Key", NightmareLogicHandler.GetItemTypeGroup("Mapper_Key"));

            PlayerData pd = CreateFreshPlayerData();
            pd.simpleKeys = 1;
            AccessRandomizerInterop.UniqueKeyCountOverrideForTests = () => 0;
            Assert.False(new AccessCondition(AccessConditionType.SimpleKeys, 1).IsSatisfied(pd, out int zeroCount));
            Assert.Equal(0, zeroCount);

            AccessRandomizerInterop.UniqueKeyCountOverrideForTests = () => 3;
            Assert.True(new AccessCondition(AccessConditionType.SimpleKeys, 3).IsSatisfied(pd, out int threeCount));
            Assert.Equal(3, threeCount);
            AccessRandomizerInterop.UniqueKeyCountOverrideForTests = null;

            var naCost = new NightmareAccessCost(new AccessCondition(AccessConditionType.Grubs, 4));
            foreach (string loc in new[] { "Mantis_Respect", "Hollow_Knight_Chain-1", "Trap_Bench", "Shade_Gate-Fog_Canyon" })
            {
                var ap = new AutoPlacement(loc) { Location = new ItemChanger.Locations.EmptyLocation { name = loc } };
                var map = new Dictionary<string, AbstractPlacement> { [loc] = ap };
                Assert.True(CostApplier.CanPlacementAcceptCost(loc, ap));
                CostApplier.ApplyCost(loc, ap, naCost, map);
                Assert.Same(ap, map[loc]);
                Assert.Same(naCost, NightmareAccessCost.Find(ap.GetTag<CostTag>()?.Cost));
            }

            var elev = new MutablePlacement("Split_Elevator_Pass")
            {
                Location = new CoordinateLocation { name = "Split_Elevator_Pass", sceneName = "Ruins2_10b" },
                Cost = new GeoCost(150)
            };
            var elevMap = new Dictionary<string, AbstractPlacement> { ["Split_Elevator_Pass"] = elev };
            Assert.True(CostApplier.CanPlacementAcceptCost("Split_Elevator_Pass", elev));
            CostApplier.ApplyCost("Split_Elevator_Pass", elev, naCost, elevMap);
            var elevYn = Assert.IsType<YNShinyPlacement>(elevMap["Split_Elevator_Pass"]);
            var multi = Assert.IsType<MultiCost>(elevYn.Cost);
            Assert.Contains(multi, c => c is GeoCost gc && gc.amount == 150);
            Assert.Same(naCost, NightmareAccessCost.Find(elevYn.Cost));
        }

        [Fact]
        public void IntegrationRegistry_WaveAMods_GroupingExclusionsOverridesAndCostApplication()
        {
            var naCost = new NightmareAccessCost(new AccessCondition(AccessConditionType.Grubs, 3));

            // 1. AlphabetRando & RgbRando
            Assert.Equal("Alphabet", NightmareLogicHandler.GetItemTypeGroup("Alphabet-A"));
            Assert.Equal("Alphabet", NightmareLogicHandler.GetItemTypeGroup("Alphabet-7"));
            Assert.Equal("RGB", NightmareLogicHandler.GetItemTypeGroup("Color-Red"));
            Assert.Equal("RGB", NightmareLogicHandler.GetItemTypeGroup("Color-Blue"));

            // 2. CombatRandomizer
            Assert.Equal("Notches", NightmareLogicHandler.GetItemTypeGroup("Notch_Fragment"));
            Assert.Equal(AccessConditionType.Notches, NightmareLogicHandler.GetItemResourceType("Notch_Fragment"));
            Assert.True(NightmareLogicHandler.IsAlwaysConflictingObjectType("Notch_Fragment", AccessConditionType.Notches, false));
            Assert.Equal("Soul_Upgrades", NightmareLogicHandler.GetItemTypeGroup("Soul_Gain"));
            Assert.Equal("Soul_Upgrades", NightmareLogicHandler.GetItemTypeGroup("Soul_Plug"));

            // 3. DarknessRandomizer
            Assert.Equal("Lumafly_Lantern", NightmareLogicHandler.GetItemTypeGroup("Lantern_Shard"));
            Assert.Equal("Lumafly_Lantern", NightmareLogicHandler.GetItemTypeGroup("Final_No_Lantern_Shard"));
            Assert.True(NightmareLogicHandler.IsAlwaysConflictingObjectType("Lantern_Shard", AccessConditionType.LumaflyLantern, false));

            // 4. BenchRando
            Assert.Equal("Benches", NightmareLogicHandler.GetItemTypeGroup("Bench-Dirtmouth"));
            var benchLoc = new ExistingFsmContainerLocation { name = "Bench-Dirtmouth", containerType = "Bench", nonreplaceable = true };
            var benchPlacement = new ExistingContainerPlacement("Bench-Dirtmouth") { Location = benchLoc };
            Assert.True(CostApplier.CanPlacementAcceptCost("Bench-Dirtmouth", benchPlacement));
            var benchMap = new Dictionary<string, AbstractPlacement> { ["Bench-Dirtmouth"] = benchPlacement };
            CostApplier.ApplyCost("Bench-Dirtmouth", benchPlacement, naCost, benchMap);
            Assert.Same(benchPlacement, benchMap["Bench-Dirtmouth"]);

            // 5. Fishing
            var fishPlacement = new AutoPlacement("Fishing_Spot-Lake_of_Unn") { Location = new EmptyLocation { name = "Fishing_Spot-Lake_of_Unn" } };
            var fishMap = new Dictionary<string, AbstractPlacement> { ["Fishing_Spot-Lake_of_Unn"] = fishPlacement };
            Assert.True(CostApplier.CanPlacementAcceptCost("Fishing_Spot-Lake_of_Unn", fishPlacement));
            CostApplier.ApplyCost("Fishing_Spot-Lake_of_Unn", fishPlacement, naCost, fishMap);
            Assert.Same(naCost, NightmareAccessCost.Find(fishPlacement.GetTag<CostTag>()?.Cost));

            // 6. MoreLocations: *_Sale items must be Geo (NOT Relics), shops & chest excluded, Swim & Egg supported
            Assert.Equal("Geo", NightmareLogicHandler.GetItemTypeGroup("Wanderer's_Journal_Sale"));
            Assert.Equal("Geo", NightmareLogicHandler.GetItemTypeGroup("Arcane_Egg_Sale"));
            Assert.Equal(AccessConditionType.Geo, NightmareLogicHandler.GetItemResourceType("Hallownest_Seal_Sale"));
            Assert.Equal(AccessConditionType.Geo, NightmareLogicHandler.GetItemResourceType("King's_Idol_Sale"));
            Assert.True(NightmareLogicHandler.IsExcludedLocation("Lemm"));
            Assert.True(NightmareLogicHandler.IsExcludedLocation("Junk_Shop"));
            Assert.True(NightmareLogicHandler.IsExcludedLocation("Geo_Chest-Above_Baldur_Shell"));

            var swimDual = new DualPlacement("Swim")
            {
                falseLocation = new EmptyLocation(),
                trueLocation = new CoordinateLocation { name = "Swim", sceneName = "Crossroads_50" }
            };
            Assert.True(CostApplier.CanPlacementAcceptCost("Swim", swimDual));
            var swimMap = new Dictionary<string, AbstractPlacement> { ["Swim"] = swimDual };
            CostApplier.ApplyCost("Swim", swimDual, naCost, swimMap);
            Assert.Same(naCost, swimDual.Cost);

            // 7. CurseRandomizer: mimic grouping + CursedVessel monotonic in-game counter
            Assert.Equal("Mantis_Claw", NightmareLogicHandler.GetItemTypeGroup("Fool_Item-Mantis_Claw"));
            Assert.Equal("Curses", NightmareLogicHandler.GetItemTypeGroup("Fool_Item-Evil_Mask_Shard"));
            Assert.Equal("Curses", NightmareLogicHandler.GetItemTypeGroup("OMEN"));

            PlayerData pd = CreateFreshPlayerData();
            pd.ore = 2;
            pd.nailSmithUpgrades = 3; // normally +3 spent = 5
            RandoPlusInterop.NailUpgradesInSaveOverrideForTests = () => true;
            Assert.True(new AccessCondition(AccessConditionType.PaleOre, 2).IsSatisfied(pd, out int oreCount));
            Assert.False(new AccessCondition(AccessConditionType.PaleOre, 3).IsSatisfied(pd, out _));
            Assert.Equal(2, oreCount);
            RandoPlusInterop.NailUpgradesInSaveOverrideForTests = null;

            CurseRandomizerInterop.SavedCursedVesselOverrideForTests = () => 2; // started at SoulVessel = 0
            CurseRandomizerInterop.CurrentSoulVesselOverrideForTests = () => 1; // 1 tier restored = 3 frags
            pd.MPReserveMax = 0;
            pd.vesselFragments = 2; // 3 + 2 = 5
            Assert.True(new AccessCondition(AccessConditionType.VesselFragments, 5).IsSatisfied(pd, out int vfCount));
            Assert.False(new AccessCondition(AccessConditionType.VesselFragments, 6).IsSatisfied(pd, out _));
            Assert.Equal(5, vfCount);

            // 8. RandoPlus: Nailsmith_Upgrade_* AutoPlacement.Cost + NoLantern/NoTear + AreaBlitz + Ghost_Essence exclusion
            var nailsmith = new AutoPlacement("Nailsmith_Upgrade_2")
            {
                Location = new EmptyLocation { name = "Nailsmith_Upgrade_2" },
                Cost = new GeoCost(500)
            };
            var nailMap = new Dictionary<string, AbstractPlacement> { ["Nailsmith_Upgrade_2"] = nailsmith };
            Assert.True(CostApplier.CanPlacementAcceptCost("Nailsmith_Upgrade_2", nailsmith));
            CostApplier.ApplyCost("Nailsmith_Upgrade_2", nailsmith, naCost, nailMap);
            Assert.IsType<MultiCost>(nailsmith.Cost);
            Assert.Same(naCost, NightmareAccessCost.Find(nailsmith.Cost));

            Assert.False(NightmareLogicHandler.IsExcludedLocation("Ghost_Essence-Gravedigger"));
            Assert.Equal("Essence", NightmareLogicHandler.GetItemTypeGroup("Ghost_Essence"));
            Assert.Equal(AccessConditionType.Essence, NightmareLogicHandler.GetItemResourceType("Ghost_Essence"));
            Assert.True(NightmareLogicHandler.IsAlwaysConflictingObjectType("Ghost_Essence", AccessConditionType.Roots, false));
            Assert.Equal("Lumafly_Lantern", NightmareLogicHandler.GetItemTypeGroup("Not_Lumafly_Lantern"));
            Assert.True(NightmareLogicHandler.IsAlwaysConflictingObjectType("Not_Lumafly_Lantern", AccessConditionType.LumaflyLantern, false));

            RandoPlusInterop.PoolOverrideForTests = k => k is "NoLantern" or "NoTear" or "AreaBlitz";
            RandoPlusInterop.AreaBlitzInvalidLocationsOverrideForTests = () => new HashSet<string> { "Crossroads_01_Invalid" };
            var gs = new GenerationSettings();
            List<AccessConditionType> allowed = NightmareLogicHandler.GetAllowedTypes(new NightmareAccessSettings(), gs);
            Assert.DoesNotContain(AccessConditionType.LumaflyLantern, allowed);
            Assert.DoesNotContain(AccessConditionType.AcidArmour, allowed);
            Assert.True(NightmareLogicHandler.IsExcludedLocation("Crossroads_01_Invalid"));
        }

        [Fact]
        public void IntegrationRegistry_WaveBMods_GroupingExclusionsOverridesAndCostApplication()
        {
            var naCost = new NightmareAccessCost(new AccessCondition(AccessConditionType.Grubs, 3));

            // 1. BreakableWallRandomizer: wall grouping, non-resource guards, Myla_Shop exclusion
            Assert.True(NightmareLogicHandler.IsExcludedLocation("Myla_Shop"));
            Assert.Equal("Walls", NightmareLogicHandler.GetItemTypeGroup("Wall-420_Geo_Rock"));
            Assert.Equal("Walls", NightmareLogicHandler.GetItemTypeGroup("Wall_Group-Grimm_Walls"));
            Assert.Equal("Planks", NightmareLogicHandler.GetItemTypeGroup("Plank-Sly"));
            Assert.Equal("Planks", NightmareLogicHandler.GetItemTypeGroup("Plank_Group-Village_Walls"));
            Assert.Equal("Dive_Floors", NightmareLogicHandler.GetItemTypeGroup("Dive_Floor-Basin_Grub"));
            Assert.Equal("Dive_Floors", NightmareLogicHandler.GetItemTypeGroup("Dive_Group-420_Rock"));
            Assert.Equal("Collapsers", NightmareLogicHandler.GetItemTypeGroup("Collapser-Deepnest_Trap_Entrance"));
            Assert.Equal("Collapsers", NightmareLogicHandler.GetItemTypeGroup("Collapser_Group-Den_Walls"));
            Assert.Null(NightmareLogicHandler.GetItemResourceType("Wall-420_Geo_Rock"));
            Assert.Null(NightmareLogicHandler.GetItemResourceType("Dive_Floor-Basin_Grub"));

            // 2. GodhomeRandomizer: marks/lifeblood grouping (disjoint from ExtraRando Pantheon_Access-*), Godhome_Shop exclusion
            Assert.True(NightmareLogicHandler.IsExcludedLocation("Godhome_Shop"));
            Assert.Equal("Statue_Marks", NightmareLogicHandler.GetItemTypeGroup("Statue_Mark-Gruz_Mother"));
            Assert.Equal("Statue_Marks", NightmareLogicHandler.GetItemTypeGroup("Eternal_Ordeal"));
            Assert.Equal("Pantheon_Marks", NightmareLogicHandler.GetItemTypeGroup("Pantheon_Master-Completion"));
            Assert.Equal("Pantheon_Marks", NightmareLogicHandler.GetItemTypeGroup("Pantheon_Hallownest-Hitless"));
            Assert.Equal("Godhome_Lifeblood", NightmareLogicHandler.GetItemTypeGroup("Godhome_Lifeblood"));
            Assert.Null(NightmareLogicHandler.GetItemResourceType("Pantheon_Master-Charms_Binding"));

            // 3. LoreRandomizer: Precept_*, Read/Listen abilities, Lore non-resource guards, Elderbug_Shop & InspectLocation protection
            Assert.True(NightmareLogicHandler.IsExcludedLocation("Elderbug_Shop"));
            Assert.Equal("Zote_Precepts", NightmareLogicHandler.GetItemTypeGroup("Precept_1"));
            Assert.Equal("Zote_Precepts", NightmareLogicHandler.GetItemTypeGroup("Precept_57"));
            Assert.Equal("Read_Ability", NightmareLogicHandler.GetItemTypeGroup("Read_Ability"));
            Assert.Equal("Listen_Ability", NightmareLogicHandler.GetItemTypeGroup("Listen_Ability"));
            Assert.Equal("Lore", NightmareLogicHandler.GetItemTypeGroup("Inspect-Stag_Egg"));
            Assert.Equal("Lore", NightmareLogicHandler.GetItemTypeGroup("Dialogue-Bretta"));
            Assert.Equal("Lore", NightmareLogicHandler.GetItemTypeGroup("Dream_Dialogue-Isma"));
            Assert.Equal("Lore", NightmareLogicHandler.GetItemTypeGroup("Lore_Tablet-Ancient_Basin_Empowered"));
            Assert.Null(NightmareLogicHandler.GetItemResourceType("Inspect-Stag_Egg"));
            Assert.Null(NightmareLogicHandler.GetItemResourceType("Inspect-Grub_Seal"));

            var gorbPlacement = new AutoPlacement("Gorb_Grave")
            {
                Location = new EmptyLocation { name = "Gorb_Grave", sceneName = "Cliffs_02" }
            };
            var belaPlacement = new AutoPlacement("Lore_Tablet-Record_Bela")
            {
                Location = new EmptyLocation { name = "Lore_Tablet-Record_Bela", sceneName = "Ruins1_30" }
            };
            var brettaLore = new AutoPlacement("Bretta")
            {
                Location = new EmptyLocation { name = "Bretta", sceneName = "Fungus2_23" }
            };
            var shrineLore = new AutoPlacement("All_Grubs-Shrine")
            {
                Location = new EmptyLocation { name = "All_Grubs-Shrine", sceneName = "Dream_Room_Believer_Shrine" }
            };
            var vanillaTablet = new MutablePlacement("Lore_Tablet-City_Entrance")
            {
                Location = new CoordinateLocation { name = "Lore_Tablet-City_Entrance", sceneName = "Ruins1_01" }
            };
            foreach (var lorePlt in new AbstractPlacement[] { gorbPlacement, belaPlacement, brettaLore, shrineLore })
            {
                Assert.True(CostApplier.CanPlacementAcceptCost(lorePlt.Name, lorePlt));
                var map = new Dictionary<string, AbstractPlacement> { [lorePlt.Name] = lorePlt };
                CostApplier.ApplyCost(lorePlt.Name, lorePlt, naCost, map);
                Assert.Same(lorePlt, map[lorePlt.Name]);
                Assert.Same(naCost, NightmareAccessCost.Find(lorePlt.GetTag<CostTag>()?.Cost));
            }
            Assert.True(CostApplier.CanPlacementAcceptCost("Lore_Tablet-City_Entrance", vanillaTablet));

            // 4. ExtraRando: grouping, non-resource Dirtmouth_Stag_Key, SplitShadeCloak runtime override, SplitFireball logic, placements
            Assert.True(NightmareLogicHandler.IsExcludedLocation("Dirtmouth_Stag_Door"));
            Assert.Equal("SimpleKeys", NightmareLogicHandler.GetItemTypeGroup("Key_Ring"));
            Assert.Equal(AccessConditionType.SimpleKeys, NightmareLogicHandler.GetItemResourceType("Key_Ring"));
            Assert.Equal("Mothwing_Cloak", NightmareLogicHandler.GetItemTypeGroup("Progressive_Left_Cloak"));
            Assert.Equal("Mothwing_Cloak", NightmareLogicHandler.GetItemTypeGroup("Cloak"));
            Assert.Equal("Vengeful_Spirit", NightmareLogicHandler.GetItemTypeGroup("Left_Vengeful_Spirit"));
            Assert.Equal("Vengeful_Spirit", NightmareLogicHandler.GetItemTypeGroup("Fireball_Spell"));
            Assert.Equal("Desolate_Dive", NightmareLogicHandler.GetItemTypeGroup("Dive_Spell"));
            Assert.Equal("Howling_Wraiths", NightmareLogicHandler.GetItemTypeGroup("Scream_Spell"));
            Assert.Equal("Pantheon_Access", NightmareLogicHandler.GetItemTypeGroup("Pantheon_Access-Master"));
            Assert.Equal("Colo_Tickets", NightmareLogicHandler.GetItemTypeGroup("Colo_Ticket-Gold"));
            Assert.Equal("HintMarkers", NightmareLogicHandler.GetItemTypeGroup("Token_Marker_Hint"));
            Assert.Equal("Dirtmouth_Stag_Key", NightmareLogicHandler.GetItemTypeGroup("Dirtmouth_Stag_Key"));
            Assert.Null(NightmareLogicHandler.GetItemResourceType("Dirtmouth_Stag_Key"));

            Assert.True(NightmareLogicHandler.IsAlwaysConflictingObjectType("Progressive_Left_Cloak", AccessConditionType.LeftDash, false));
            Assert.False(NightmareLogicHandler.IsAlwaysConflictingObjectType("Progressive_Left_Cloak", AccessConditionType.RightDash, false));
            Assert.True(NightmareLogicHandler.IsAlwaysConflictingObjectType("Left_Shade_Soul", AccessConditionType.FireballLevel, false));
            Assert.True(NightmareLogicHandler.IsAlwaysConflictingObjectType("Key_Ring", AccessConditionType.SimpleKeys, false));

            // SplitShadeCloakModule sets pd.hasShadowDash = true on a single cloak direction; Dash/RightDash must stay false
            PlayerData pd = CreateFreshPlayerData();
            pd.hasShadowDash = true;
            ExtraRandoInterop.SplitDashStateOverrideForTests = () => (hasLeft: true, hasRight: false);
            Assert.True(new AccessCondition(AccessConditionType.LeftDash).IsSatisfied(pd, out _));
            Assert.False(new AccessCondition(AccessConditionType.RightDash).IsSatisfied(pd, out _));
            Assert.False(new AccessCondition(AccessConditionType.Dash).IsSatisfied(pd, out _));

            ExtraRandoInterop.SplitDashStateOverrideForTests = () => (hasLeft: true, hasRight: true);
            Assert.True(new AccessCondition(AccessConditionType.Dash).IsSatisfied(pd, out _));
            ExtraRandoInterop.SplitDashStateOverrideForTests = null;

            // SplitFireball overrides Fireball Lv2 logic expression
            var gs = new GenerationSettings();
            gs.PoolSettings.Skills = true;
            ExtraRandoInterop.SettingOverrideForTests = k => k is "Enabled" or "SplitFireball" or "SplitShadeCloak";
            Assert.Equal("(FIREBALLLEFT>1 | FIREBALLRIGHT>1)", new AccessCondition(AccessConditionType.FireballLevel, 2).ToLogicExpression(gs));
            Assert.Equal("FIREBALL", new AccessCondition(AccessConditionType.FireballLevel, 1).ToLogicExpression(gs));

            List<AccessConditionType> erAllowed = NightmareLogicHandler.GetAllowedTypes(new NightmareAccessSettings(), gs);
            Assert.Contains(AccessConditionType.LeftDash, erAllowed);
            Assert.Contains(AccessConditionType.RightDash, erAllowed);
            Assert.DoesNotContain(AccessConditionType.Dash, erAllowed);
            ExtraRandoInterop.SettingOverrideForTests = null;

            // Split_Shade_Soul & GPZ_10 convert to YNShinyPlacement; Bardoon_Butt gates in-place via ShouldBlockAutoGive
            foreach (string loc in new[] { "Split_Shade_Soul", "GPZ_10" })
            {
                var mp = new MutablePlacement(loc)
                {
                    Location = new CoordinateLocation { name = loc, sceneName = "Ruins1_31b" }
                };
                var map = new Dictionary<string, AbstractPlacement> { [loc] = mp };
                Assert.True(CostApplier.CanPlacementAcceptCost(loc, mp));
                CostApplier.ApplyCost(loc, mp, naCost, map);
                var yn = Assert.IsType<YNShinyPlacement>(map[loc]);
                Assert.Same(naCost, NightmareAccessCost.Find(yn.Cost));
            }

            var butt = new AutoPlacement("Bardoon_Butt") { Location = new EmptyLocation { name = "Bardoon_Butt" } };
            var buttMap = new Dictionary<string, AbstractPlacement> { ["Bardoon_Butt"] = butt };
            Assert.True(CostApplier.CanPlacementAcceptCost("Bardoon_Butt", butt));
            CostApplier.ApplyCost("Bardoon_Butt", butt, naCost, buttMap);
            Assert.Same(butt, buttMap["Bardoon_Butt"]);
            Assert.Same(naCost, NightmareAccessCost.Find(butt.GetTag<CostTag>()?.Cost));

            NightmareLogicHandler.ActiveScope = RequirementScope.PerLocation;
            NightmareLogicHandler.LocationConditions["Bardoon_Butt"] = new AccessCondition(AccessConditionType.Grubs, 3);
            var buttGive = new GiveInfo { Container = "Butt", FlingType = FlingType.DirectDeposit };
            PlayerData buttPd = CreateFreshPlayerData();
            buttPd.grubsCollected = 2;
            CornerPromptHelper.PlayerDataOverrideForTests = buttPd;
            Assert.True(IntegrationRegistry.ShouldBlockAutoGive(butt, buttGive));

            buttPd.grubsCollected = 3;
            Assert.False(IntegrationRegistry.ShouldBlockAutoGive(butt, buttGive));
            Assert.False(IntegrationRegistry.ShouldBlockAutoGive(
                butt, new GiveInfo { Container = Container.Shiny, FlingType = FlingType.DirectDeposit }));

            // Batch 4 ExtraRando AutoLocations: HotSprings, Split_Vengeful_Spirit, White_Defender_5, Pantheon-*
            string[] batch4AutoLocs =
            {
                "Crossroads-Hot_Spring",
                "Colosseum-Hot_Spring",
                "Deepnest-Hot_Spring",
                "Bathhouse-Hot_Spring",
                "Lower_Godhome-Hot_Spring",
                "Upper_Godhome-Hot_Spring",
                "Split_Vengeful_Spirit",
                "White_Defender_5",
                "Pantheon-Master",
                "Pantheon-Artist",
                "Pantheon-Sage",
                "Pantheon-Knight",
                "Pantheon-Hallownest"
            };
            foreach (string loc in batch4AutoLocs)
            {
                var ap = new AutoPlacement(loc) { Location = new EmptyLocation { name = loc } };
                var map = new Dictionary<string, AbstractPlacement> { [loc] = ap };
                Assert.True(CostApplier.CanPlacementAcceptCost(loc, ap));
                CostApplier.ApplyCost(loc, ap, naCost, map);
                Assert.Same(ap, map[loc]);
                Assert.Same(naCost, NightmareAccessCost.Find(ap.GetTag<CostTag>()?.Cost));
            }

            var panth = new AutoPlacement("Pantheon-Master") { Location = new EmptyLocation { name = "Pantheon-Master" } };
            NightmareLogicHandler.LocationConditions["Pantheon-Master"] = new AccessCondition(AccessConditionType.Grubs, 5);
            Assert.True(IntegrationRegistry.ShouldBlockAutoGive(
                panth, new GiveInfo { Container = Container.Unknown, FlingType = FlingType.DirectDeposit }));
            Assert.False(IntegrationRegistry.ShouldBlockAutoGive(
                panth, new GiveInfo { Container = Container.Shiny, FlingType = FlingType.DirectDeposit }));
        }

        [Fact]
        public void IntegrationRegistry_WaveC_RopeAndCorpse_GroupingAndCostApplication()
        {
            var naCost = new NightmareAccessCost(new AccessCondition(AccessConditionType.Grubs, 2));

            Assert.Equal("Chandelier-Watcher_Knights", NightmareLogicHandler.GetItemTypeGroup("Chandelier-Watcher_Knights"));
            Assert.Null(NightmareLogicHandler.GetItemResourceType("Chandelier-Watcher_Knights"));

            Assert.Equal("Corpse_Dreams", NightmareLogicHandler.GetItemTypeGroup("Corpse_Dream-Basin_Key"));
            Assert.Equal("Corpse_Dreams", NightmareLogicHandler.GetItemTypeGroup("Corpse_Dream-Grub_Room"));
            Assert.Null(NightmareLogicHandler.GetItemResourceType("Corpse_Dream-Basin_Key"));
            Assert.Null(NightmareLogicHandler.GetItemResourceType("Corpse_Dream-Grub_Room"));

            string[] waveCLocs =
            {
                "Chandelier-Watcher_Knights",
                "Corpse_Dream-Lighthouse",
                "Ghost_Essence-Gravedigger",
                "Wall-420_Geo_Rock",
                "Wall_Group-Peak_Walls",
                "Plank-Peak_Toll",
                "Plank_Group-Sanctum_Planks",
                "Dive_Floor-Basin_Grub",
                "Dive_Group-Sanctum_Dives",
                "Collapser-Deepnest_Entrance",
                "Collapser_Group-Sanctum_Collapsers"
            };
            foreach (string loc in waveCLocs)
            {
                var ap = new AutoPlacement(loc) { Location = new EmptyLocation { name = loc } };
                var map = new Dictionary<string, AbstractPlacement> { [loc] = ap };
                Assert.True(CostApplier.CanPlacementAcceptCost(loc, ap));
                CostApplier.ApplyCost(loc, ap, naCost, map);
                Assert.Same(ap, map[loc]);
                Assert.Same(naCost, NightmareAccessCost.Find(ap.GetTag<CostTag>()?.Cost));
            }
        }

        [Fact]
        public void IntegrationRegistry_WaveC_FlowerRandomizer_ExclusionsAndCostApplication()
        {
            var naCost = new NightmareAccessCost(new AccessCondition(AccessConditionType.Grubs, 3));

            string[] flowerLocations =
            {
                "Flower_Quest-Elderbug",
                "Flower_Quest-Oro",
                "Flower_Quest-Godseeker",
                "Flower_Quest-Emilitia",
                "Flower_Quest-White_Lady",
                "Flower_Quest-Mato",
                "Flower_Quest-Sheo",
                "Flower_Quest-Marissa",
                "Flower_Quest-Midwife",
                "Flower_Quest-Isma",
                "Flower_Quest-Radiance",
                "Flower_Quest-Pale_King",
                "Flower_Quest-Pain"
            };

            Assert.False(NightmareLogicHandler.IsConflictingCondition(AccessConditionType.PaleOre, "Flower_Quest-Oro"));
            Assert.False(NightmareLogicHandler.IsConflictingCondition(AccessConditionType.PaleOre, "Flower_Quest-Pale_King"));
            Assert.False(NightmareLogicHandler.IsConflictingCondition(AccessConditionType.KingsoulLevel, "Flower_Quest-White_Lady"));

            foreach (string loc in flowerLocations)
            {
                Assert.False(NightmareLogicHandler.IsExcludedLocation(loc));

                var ap = new AutoPlacement(loc) { Location = new EmptyLocation { name = loc } };
                var map = new Dictionary<string, AbstractPlacement> { [loc] = ap };
                Assert.True(CostApplier.CanPlacementAcceptCost(loc, ap));
                CostApplier.ApplyCost(loc, ap, naCost, map);
                Assert.Same(ap, map[loc]);
                Assert.Same(naCost, NightmareAccessCost.Find(ap.GetTag<CostTag>()?.Cost));
            }
        }

        [Fact]
        public void IntegrationRegistry_WaveC_GodhomeRandomizer_CostApplicationAndAutoGiveGating()
        {
            var naCost = new NightmareAccessCost(new AccessCondition(AccessConditionType.Grubs, 3));

            string[] godhomeLocs =
            {
                "Godhome_Lifeblood",
                "Eternal_Ordeal",
                "Empty_Mark-Gruz_Mother",
                "Bronze_Mark-Hornet_1",
                "Silver_Mark-Mantis_Lords",
                "Gold_Mark-Pure_Vessel",
                "Pantheon_Master-Completion",
                "Pantheon_Knight-All_Bindings",
                "Pantheon_Hallownest-Hitless"
            };

            foreach (string loc in godhomeLocs)
            {
                Assert.False(NightmareLogicHandler.IsExcludedLocation(loc));

                var ap = new AutoPlacement(loc) { Location = new EmptyLocation { name = loc } };
                var map = new Dictionary<string, AbstractPlacement> { [loc] = ap };
                Assert.True(CostApplier.CanPlacementAcceptCost(loc, ap));
                CostApplier.ApplyCost(loc, ap, naCost, map);
                Assert.Same(ap, map[loc]);
                Assert.Same(naCost, NightmareAccessCost.Find(ap.GetTag<CostTag>()?.Cost));
            }

            PlayerData pd = CreateFreshPlayerData();
            pd.grubsCollected = 1;
            CornerPromptHelper.PlayerDataOverrideForTests = pd;
            NightmareLogicHandler.ActiveScope = RequirementScope.PerLocation;
            NightmareLogicHandler.LocationConditions["Bronze_Mark-Hornet_1"] = new AccessCondition(AccessConditionType.Grubs, 3);
            NightmareLogicHandler.LocationConditions["Pantheon_Master-Completion"] = new AccessCondition(AccessConditionType.Grubs, 3);

            var bronze = new AutoPlacement("Bronze_Mark-Hornet_1") { Location = new EmptyLocation { name = "Bronze_Mark-Hornet_1" } };
            var panth = new AutoPlacement("Pantheon_Master-Completion") { Location = new EmptyLocation { name = "Pantheon_Master-Completion" } };
            var directGive = new GiveInfo { Container = Container.Unknown, FlingType = FlingType.DirectDeposit };

            Assert.True(IntegrationRegistry.ShouldBlockAutoGive(bronze, directGive));
            Assert.True(IntegrationRegistry.ShouldBlockAutoGive(panth, directGive));

            pd.grubsCollected = 3;
            Assert.False(IntegrationRegistry.ShouldBlockAutoGive(bronze, directGive));
            Assert.False(IntegrationRegistry.ShouldBlockAutoGive(panth, directGive));
        }
    }
}
