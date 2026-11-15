using System;
using System.Collections.Generic;
using System.Linq;
using NightmareAccess.InGame;
using NightmareAccess.Interop;
using NightmareAccess.Logic;
using NightmareAccess.Settings;
using RandomizerCore;
using RandomizerCore.Logic;
using RandomizerCore.LogicItems;
using RandomizerCore.StringParsing;
using RandomizerMod.RandomizerData;
using RandomizerMod.RC;
using RandomizerMod.Settings;
using Xunit;
using Random = System.Random;

namespace NightmareAccess.Tests
{
    public sealed class LogicTestFixture : IDisposable
    {
        public LogicTestFixture()
        {
            Data.Load();
            NightmareLogicHandler.Hook();
            RequestBuilder.OnSelectStart.Subscribe(-1000f, SelectStartWithoutMenu);
        }

        private static bool SelectStartWithoutMenu(Random rng, GenerationSettings gs, SettingsPM pm, out StartDef def)
        {
            string name = string.IsNullOrEmpty(gs.StartLocationSettings.StartLocation)
                ? "King's Pass"
                : gs.StartLocationSettings.StartLocation;
            def = Data.GetStartDef(name) ?? Data.GetStartDef("King's Pass");
            gs.StartLocationSettings.StartLocation = def.Name;
            return true;
        }

        public void Dispose()
        {
        }
    }

    public sealed class WorldVerifierAndSeedTests : IClassFixture<LogicTestFixture>, IDisposable
    {
        private readonly NightmareAccessSettings _savedSettings = new();

        public WorldVerifierAndSeedTests(LogicTestFixture _)
        {
            _savedSettings.CopyFrom(NightmareAccess.Settings);
            ResetOverrides();
        }

        public void Dispose()
        {
            NightmareAccess.Settings.CopyFrom(_savedSettings);
            ResetOverrides();
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

        private static RandoModContext BuildSyntheticContext(
            IEnumerable<(string itemName, string locName, string logicExpr)> entries,
            GenerationSettings gs = null)
        {
            gs ??= new GenerationSettings();
            var lmb = new LogicManagerBuilder();
            lmb.GetOrAddTerm("LEFTDASH", TermType.SignedByte);
            lmb.GetOrAddTerm("RIGHTDASH", TermType.SignedByte);
            lmb.GetOrAddTerm("LEFTCLAW", TermType.SignedByte);
            lmb.GetOrAddTerm("RIGHTCLAW", TermType.SignedByte);
            lmb.GetOrAddTerm("GRUBS", TermType.SignedByte);
            lmb.GetOrAddTerm("SIMPLE", TermType.SignedByte);
            lmb.GetOrAddTerm("MASKSHARDS", TermType.SignedByte);
            lmb.GetOrAddTerm("NOTCHES", TermType.SignedByte);
            lmb.GetOrAddTerm("CHARMS", TermType.SignedByte);
            lmb.GetOrAddTerm("JOURNAL", TermType.SignedByte);
            lmb.GetOrAddTerm("GEO", TermType.SignedByte);
            lmb.GetOrAddTerm("Can_Replenish_Geo", TermType.SignedByte);
            lmb.AddWaypoint(new RawWaypointDef("Start_State", "TRUE", stateless: false));
            lmb.AddWaypoint(new RawWaypointDef("Can_Replenish_Geo", "TRUE", stateless: true));
            lmb.AddMacro(new KeyValuePair<string, string>("DASH", "LEFTDASH + RIGHTDASH"));
            lmb.AddMacro(new KeyValuePair<string, string>("CLAW", "LEFTCLAW"));

            lmb.AddItem(new SingleItem("Mantis_Claw", new TermValue(lmb.GetTerm("LEFTCLAW"), 1)));
            lmb.AddItem(new SingleItem("Grub", new TermValue(lmb.GetTerm("GRUBS"), 1)));
            lmb.AddItem(new SingleItem("Simple_Key", new TermValue(lmb.GetTerm("SIMPLE"), 1)));
            lmb.AddItem(new SingleItem("Mask_Shard", new TermValue(lmb.GetTerm("MASKSHARDS"), 1)));
            lmb.AddItem(new SingleItem("Hunter's_Journal", new TermValue(lmb.GetTerm("JOURNAL"), 1)));
            lmb.AddItem(new EmptyItem("Journal_Entry-Goam"));
            lmb.AddItem(new EmptyItem("One_Geo"));

            var list = entries.ToList();
            foreach (var (_, locName, logicExpr) in list)
            {
                lmb.AddLogicDef(new RawLogicDef(locName, logicExpr));
            }

            var lm = new LogicManager(lmb);
            var ctx = new RandoModContext(lm)
            {
                GenerationSettings = gs,
                InitialProgression = new ProgressionInitializer
                {
                    Setters =
                    {
                        new TermValue(lm.GetTermStrict("MASKSHARDS"), 20 - 4 * gs.CursedSettings.CursedMasks),
                        new TermValue(lm.GetTermStrict("NOTCHES"), 3 - gs.CursedSettings.CursedNotches)
                    }
                }
            };

            foreach (var (itemName, locName, _) in list)
            {
                ctx.itemPlacements.Add(new ItemPlacement(
                    new RandoModItem { item = lm.GetItemStrict(itemName) },
                    new RandoModLocation { logic = lm.GetLogicDefStrict(locName) }));
            }

            return ctx;
        }

        [Fact]
        public void Verifier_DetectsAndRepairs_PerLocationNumericDeadlocksAndJournalCycle()
        {
            var gs = new GenerationSettings();
            var settings = new NightmareAccessSettings
            {
                Enabled = true,
                Scope = RequirementScope.PerLocation,
                AllowGeo = true,
                MinGeo = 1,
                MaxGeo = 100
            };

            RandoModContext ctx = BuildSyntheticContext(new[]
            {
                ("Grub", "Loc_Grub1", "TRUE"),
                ("Hunter's_Journal", "Loc_Journal", "TRUE"),
                ("Mantis_Claw", "Loc_Start", "TRUE"),
                ("Journal_Entry-Goam", "Loc_Goam", "LEFTCLAW"),
                ("One_Geo", "Loc_Final", "LEFTCLAW")
            }, gs);

            NightmareLogicHandler.LocationConditions["Loc_Start"] = new AccessCondition(AccessConditionType.MaskShards, 4);
            NightmareLogicHandler.LocationConditions["Loc_Grub1"] = new AccessCondition(AccessConditionType.Grubs, 2);
            NightmareLogicHandler.LocationConditions["Loc_Goam"] = new AccessCondition(AccessConditionType.SpecificJournalEntry, 1, 0, "Goam");
            NightmareLogicHandler.LocationConditions["Loc_Final"] = new AccessCondition(AccessConditionType.SpecificJournalEntry, 1, 0, "Goam");

            WorldVerificationReport beforeRepair = CostValidator.Verify(ctx, RequirementScope.PerLocation, gs);
            Assert.False(beforeRepair.IsCompletable);
            Assert.Contains(beforeRepair.UnreachableLocations, s => s.Contains("Loc_Start"));
            Assert.Contains(beforeRepair.UnreachableLocations, s => s.Contains("Loc_Goam"));

            var allowed = new List<AccessConditionType> { AccessConditionType.Geo, AccessConditionType.Grubs };
            WorldVerificationReport afterRepair = CostValidator.ValidateAndRepair(
                ctx, new Random(123), allowed, settings, gs);

            Assert.True(afterRepair.IsCompletable, afterRepair.FormatSummary());
            Assert.True(afterRepair.RepairedCount > 0);
            Assert.Empty(afterRepair.UnreachableLocations);
            Assert.Equal(5, afterRepair.VerifiedLocationsCount);
        }

        [Fact]
        public void Verifier_DetectsAndRepairs_PerObjectGroupSelfCycle()
        {
            var gs = new GenerationSettings();
            var settings = new NightmareAccessSettings
            {
                Enabled = true,
                Scope = RequirementScope.PerObjectGroup,
                AllowGeo = true,
                MinGeo = 10,
                MaxGeo = 100
            };

            RandoModContext ctx = BuildSyntheticContext(new[]
            {
                ("Mantis_Claw", "Loc_Claw", "TRUE"),
                ("One_Geo", "Loc_End", "LEFTCLAW")
            }, gs);

            NightmareLogicHandler.ItemTypeConditions["Mantis_Claw"] = new AccessCondition(AccessConditionType.WallJump);

            WorldVerificationReport before = CostValidator.Verify(ctx, RequirementScope.PerObjectGroup, gs);
            Assert.False(before.IsCompletable);

            var allowed = new List<AccessConditionType> { AccessConditionType.Geo, AccessConditionType.WallJump };
            WorldVerificationReport after = CostValidator.ValidateAndRepair(
                ctx, new Random(7), allowed, settings, gs);

            Assert.True(after.IsCompletable, after.FormatSummary());
            Assert.Empty(after.UnreachableLocations);
        }

        [Theory]
        [InlineData(RequirementScope.PerLocation, 1001, false, false, false)]
        [InlineData(RequirementScope.PerLocation, 1002, true, true, false)]
        [InlineData(RequirementScope.PerObjectGroup, 2001, false, false, false)]
        [InlineData(RequirementScope.PerObjectGroup, 2002, true, false, true)]
        [InlineData(RequirementScope.PerObject, 3001, false, false, false)]
        [InlineData(RequirementScope.PerObject, 3002, false, true, false)]
        public void FullRandomizerSeedGeneration_IsAlwaysVerifiedCompletable(
            RequirementScope scope,
            int seed,
            bool splitAbilities,
            bool cursedSettings,
            bool mapAreaRando)
        {
            NightmareAccess.Settings.CopyFrom(new NightmareAccessSettings
            {
                Enabled = true,
                Scope = scope,
                RequirementChance = 100
            });

            var gs = new GenerationSettings { Seed = seed };
            if (splitAbilities)
            {
                gs.NoveltySettings.SplitCloak = true;
                gs.NoveltySettings.SplitClaw = true;
                gs.NoveltySettings.SplitSuperdash = true;
            }
            if (cursedSettings)
            {
                gs.CursedSettings.CursedMasks = 2;
                gs.CursedSettings.CursedNotches = 1;
                gs.CursedSettings.RemoveSpellUpgrades = true;
            }
            if (mapAreaRando)
            {
                gs.TransitionSettings.Mode = TransitionSettings.TransitionMode.MapAreaRandomizer;
            }
            gs.Clamp();

            var rc = new RandoController(gs, new SettingsPM(gs), new RandomizerCore.RandoMonitor());
            rc.Run();

            CostApplier.PrepareAndVerify(rc.ctx, NightmareAccess.Settings, rc.gs, placements: null);
            WorldVerificationReport report = NightmareLogicHandler.LastVerificationReport;

            Assert.NotNull(report);
            Assert.True(report.IsCompletable, report.FormatSummary());
            Assert.Equal(report.BaselineLocationsCount, report.VerifiedLocationsCount);
            Assert.Equal(report.BaselineWaypointsCount, report.VerifiedWaypointsCount);
            Assert.Equal(report.BaselineTransitionsCount, report.VerifiedTransitionsCount);
            Assert.Empty(report.ConsistencyIssues);
        }

        [Fact]
        public void Verifier_CombatRandomizerNotchFragments_AndCurseRandomizerWalletAndVessel()
        {
            // 1. CombatRandomizer: 3 fragments per notch; 6 fragments -> only 2 real notches (NOT 6!)
            CombatRandomizerInterop.FragmentsPerNotchOverrideForTests = () => 3;
            var gs = new GenerationSettings();
            var settings = new NightmareAccessSettings
            {
                Enabled = true,
                Scope = RequirementScope.PerLocation,
                AllowGeo = true,
                MinGeo = 10,
                MaxGeo = 100
            };

            var lmb = new LogicManagerBuilder();
            lmb.GetOrAddTerm("NOTCHES", TermType.SignedByte);
            lmb.GetOrAddTerm("VESSELFRAGMENTS", TermType.SignedByte);
            lmb.GetOrAddTerm("WALLET", TermType.SignedByte);
            lmb.GetOrAddTerm("GEO", TermType.SignedByte);
            lmb.GetOrAddTerm("Can_Replenish_Geo", TermType.SignedByte);
            lmb.AddWaypoint(new RawWaypointDef("Start_State", "TRUE", stateless: false));
            lmb.AddWaypoint(new RawWaypointDef("Can_Replenish_Geo", "TRUE", stateless: true));

            lmb.AddItem(new RandomizerCore.StringItems.StringItemTemplate("Notch_Fragment", "NOTCHES++"));
            lmb.AddItem(new RandomizerCore.StringItems.StringItemTemplate("Geo_Wallet", "WALLET++"));
            lmb.AddItem(new RandomizerCore.StringItems.StringItemTemplate("Vessel_Fragment", "VESSELFRAGMENTS++"));
            lmb.AddItem(new EmptyItem("One_Geo"));

            lmb.AddLogicDef(new RawLogicDef("Loc_F1", "TRUE"));
            lmb.AddLogicDef(new RawLogicDef("Loc_F2", "TRUE"));
            lmb.AddLogicDef(new RawLogicDef("Loc_F3", "TRUE"));
            lmb.AddLogicDef(new RawLogicDef("Loc_F4", "TRUE"));
            lmb.AddLogicDef(new RawLogicDef("Loc_F5", "TRUE"));
            lmb.AddLogicDef(new RawLogicDef("Loc_F6", "TRUE"));
            lmb.AddLogicDef(new RawLogicDef("Loc_Need3Notches", "TRUE"));
            lmb.AddLogicDef(new RawLogicDef("Loc_WalletGate", "TRUE"));
            lmb.AddLogicDef(new RawLogicDef("Loc_AfterWallet", "WALLET"));
            lmb.AddLogicDef(new RawLogicDef("Loc_Need1Vessel", "TRUE"));

            var lm = new LogicManager(lmb);

            CurseRandomizerInterop.WalletCapacitiesOverrideForTests = () => new[] { 500, 1000, 1500, 2000 };
            CurseRandomizerInterop.CursedVesselCountOverrideForTests = () => 1;
            NightmareLogicHandler.IntegrationInts["CurseRandomizer.CursedVessel"] = 1;

            var ctx = new RandoModContext(lm)
            {
                GenerationSettings = gs,
                InitialProgression = new ProgressionInitializer
                {
                    Setters =
                    {
                        new TermValue(lm.GetTermStrict("NOTCHES"), 3),
                        // CurseRandomizer initializes VESSELFRAGMENTS at +3 when CursedVessel == 1
                        new TermValue(lm.GetTermStrict("VESSELFRAGMENTS"), 3)
                    }
                }
            };

            for (int i = 1; i <= 6; i++)
            {
                ctx.itemPlacements.Add(new ItemPlacement(new RandoModItem { item = lm.GetItemStrict("Notch_Fragment") }, new RandoModLocation { logic = lm.GetLogicDefStrict($"Loc_F{i}") }));
            }
            ctx.itemPlacements.Add(new ItemPlacement(new RandoModItem { item = lm.GetItemStrict("One_Geo") }, new RandoModLocation { logic = lm.GetLogicDefStrict("Loc_Need3Notches") }));
            ctx.itemPlacements.Add(new ItemPlacement(new RandoModItem { item = lm.GetItemStrict("Geo_Wallet") }, new RandoModLocation { logic = lm.GetLogicDefStrict("Loc_WalletGate") }));
            ctx.itemPlacements.Add(new ItemPlacement(new RandoModItem { item = lm.GetItemStrict("Vessel_Fragment") }, new RandoModLocation { logic = lm.GetLogicDefStrict("Loc_AfterWallet") }));
            ctx.itemPlacements.Add(new ItemPlacement(new RandoModItem { item = lm.GetItemStrict("One_Geo") }, new RandoModLocation { logic = lm.GetLogicDefStrict("Loc_Need1Vessel") }));

            // Impossible under real mechanics:
            // - 6 Notch_Fragments @ 3/notch = 2 notches (< 3)
            // - Loc_WalletGate requires 800 Geo BEFORE any Geo_Wallet (cap is 500)
            // - Loc_Need1Vessel requires 1 Vessel Fragment BEFORE collecting the only Vessel_Fragment (despite PI=+3!)
            NightmareLogicHandler.LocationConditions["Loc_Need3Notches"] = new AccessCondition(AccessConditionType.Notches, 3);
            NightmareLogicHandler.LocationConditions["Loc_WalletGate"] = new AccessCondition(AccessConditionType.Geo, 800);
            NightmareLogicHandler.LocationConditions["Loc_AfterWallet"] = new AccessCondition(AccessConditionType.Geo, 800);
            NightmareLogicHandler.LocationConditions["Loc_Need1Vessel"] = new AccessCondition(AccessConditionType.VesselFragments, 2);

            WorldVerificationReport before = CostValidator.Verify(ctx, RequirementScope.PerLocation, gs);
            Assert.False(before.IsCompletable);
            Assert.Contains(before.UnreachableLocations, s => s.Contains("Loc_Need3Notches"));
            Assert.Contains(before.UnreachableLocations, s => s.Contains("Loc_WalletGate"));
            Assert.Contains(before.UnreachableLocations, s => s.Contains("Loc_AfterWallet"));
            Assert.Contains(before.UnreachableLocations, s => s.Contains("Loc_Need1Vessel"));

            // Now repair: Loc_WalletGate gets repaired, unlocking Geo_Wallet -> Loc_AfterWallet (800 Geo) becomes reachable!
            var allowed = new List<AccessConditionType> { AccessConditionType.Geo, AccessConditionType.Notches, AccessConditionType.VesselFragments };
            WorldVerificationReport after = CostValidator.ValidateAndRepair(ctx, new Random(13), allowed, settings, gs);
            Assert.True(after.IsCompletable, after.FormatSummary());
            Assert.Empty(after.UnreachableLocations);
            // Loc_AfterWallet (800 Geo) stayed valid once Geo_Wallet raised cap to 1000!
            Assert.Equal(800, NightmareLogicHandler.LocationConditions["Loc_AfterWallet"].RequiredAmount);
        }

        [Fact]
        public void BenchRando_WaypointsAreNotCorruptedInLogicLookup_WhileLocationsStillRollAndVerify()
        {
            BenchRandoInterop.RandomizedBenchesOverrideForTests = () => new[] { "Bench-Dirtmouth" };
            NightmareAccess.Settings.CopyFrom(new NightmareAccessSettings
            {
                Enabled = true,
                Scope = RequirementScope.PerLocation,
                RequirementChance = 100
            });

            var lmb = new LogicManagerBuilder();
            lmb.GetOrAddTerm("Bench_Item_Term-Dirtmouth", TermType.SignedByte);
            lmb.AddItem(new RandomizerCore.StringItems.StringItemTemplate("Bench-Dirtmouth", "Bench_Item_Term-Dirtmouth++"));
            lmb.AddWaypoint(new RawWaypointDef("Bench-Dirtmouth", "TRUE", stateless: false));
            lmb.AddLogicDef(new RawLogicDef("Town", "Bench-Dirtmouth"));

            var gs = new GenerationSettings { Seed = 888 };
            NightmareLogicHandler.ApplyNightmareLogic(gs, lmb, NightmareAccess.Settings);

            // Bench-Dirtmouth was rolled as a Nightmare Access location,
            // yet the Bench-Dirtmouth waypoint in LogicLookup remained untouched!
            Assert.True(NightmareLogicHandler.LocationConditions.ContainsKey("Bench-Dirtmouth"));
            Assert.True(lmb.LogicLookup["Bench-Dirtmouth"].Expr.IsEquivalentTo(
                new RandomizerCore.StringLogic.LogicClause("TRUE").Expr));
        }

        [Fact]
        public void FullRandomizerSeedGeneration_WithSimulatedYarcDreamOrbs_IsVerifiedCompletable()
        {
            YarcInterop.DreamOrbsOverrideForTests = () => true;

            void InjectYarcOrbsLogic(GenerationSettings _, LogicManagerBuilder lmb)
            {
                lmb.AddItem(new SingleItem("Essence_Orb", new TermValue(lmb.GetTerm("ESSENCE"), 1)));
                for (int i = 1; i <= 29; i++)
                {
                    lmb.AddLogicDef(new RawLogicDef($"DreamOrb_Crossroads_{i}", "*Whispering_Root-Crossroads"));
                }
            }

            void InjectYarcOrbsRequest(RequestBuilder rb)
            {
                for (int i = 1; i <= 29; i++)
                {
                    rb.AddLocationByName($"DreamOrb_Crossroads_{i}");
                    rb.AddItemByName("Essence_Orb");
                }
                rb.RemoveItemsWhere(item => item.StartsWith("Whispering_Root-", StringComparison.Ordinal));
                rb.RemoveLocationsWhere(loc => loc.StartsWith("Whispering_Root-", StringComparison.Ordinal));
            }

            RCData.RuntimeLogicOverride.Subscribe(50f, InjectYarcOrbsLogic);
            RequestBuilder.OnUpdate.Subscribe(-100f, InjectYarcOrbsRequest);
            try
            {
                NightmareAccess.Settings.CopyFrom(new NightmareAccessSettings
                {
                    Enabled = true,
                    Scope = RequirementScope.PerObject,
                    RequirementChance = 100
                });

                var gs = new GenerationSettings { Seed = 4242 };
                gs.PoolSettings.WhisperingRoots = false;
                gs.PoolSettings.BossEssence = false;
                gs.Clamp();

                var rc = new RandoController(gs, new SettingsPM(gs), new RandomizerCore.RandoMonitor());
                rc.Run();

                CostApplier.PrepareAndVerify(rc.ctx, NightmareAccess.Settings, rc.gs, placements: null);
                WorldVerificationReport report = NightmareLogicHandler.LastVerificationReport;

                Assert.NotNull(report);
                Assert.True(report.IsCompletable, report.FormatSummary());
                Assert.Contains(rc.ctx.itemPlacements, p => p.Location.Name == "DreamOrb_Crossroads_1");
            }
            finally
            {
                RCData.RuntimeLogicOverride.Unsubscribe(50f, InjectYarcOrbsLogic);
                RequestBuilder.OnUpdate.Unsubscribe(-100f, InjectYarcOrbsRequest);
            }
        }

        [Theory]
        [InlineData(RequirementScope.PerLocation, false)]
        [InlineData(RequirementScope.PerLocation, true)]
        [InlineData(RequirementScope.PerObject, false)]
        [InlineData(RequirementScope.PerObject, true)]
        public void FullSeed_WhenCollectibleSubTogglesDisabled_NeverFailsVerification(
            RequirementScope scope, bool allowGeoFallback)
        {
            var settings = new NightmareAccessSettings
            {
                Enabled = true,
                Scope = scope,
                RequirementChance = 100
            };
            foreach (var prop in typeof(NightmareAccessSettings).GetProperties())
            {
                if (prop.PropertyType == typeof(bool) && prop.Name.StartsWith("Allow", StringComparison.Ordinal))
                {
                    prop.SetValue(settings, false, null);
                }
            }
            settings.AllowCharms = true;
            settings.AllowMaps = true;
            settings.AllowJournal = true;
            settings.AllowStags = true;
            settings.AllowGeo = allowGeoFallback;
            NightmareAccess.Settings.CopyFrom(settings);

            var gs = new GenerationSettings { Seed = allowGeoFallback ? 5151 : 5150 };
            gs.PoolSettings.GeoChests = false;
            gs.PoolSettings.GeoRocks = false;
            gs.CursedSettings.ReplaceJunkWithOneGeo = false;
            gs.Clamp();

            var rc = new RandoController(gs, new SettingsPM(gs), new RandomizerCore.RandoMonitor());
            rc.Run();

            CostApplier.PrepareAndVerify(rc.ctx, NightmareAccess.Settings, rc.gs, placements: null);
            WorldVerificationReport report = NightmareLogicHandler.LastVerificationReport;

            Assert.NotNull(report);
            Assert.True(report.IsCompletable, report.FormatSummary());
            if (!allowGeoFallback)
            {
                Assert.Empty(NightmareLogicHandler.LocationConditions);
                Assert.Empty(NightmareLogicHandler.ObjectConditions);
            }
        }

        [Theory]
        [InlineData(RequirementScope.PerLocation, 6001)]
        [InlineData(RequirementScope.PerObjectGroup, 6002)]
        [InlineData(RequirementScope.PerObject, 6003)]
        public void FullRandomizerSeedGeneration_WithAllSimulatedYarcPools_IsVerifiedCompletable(
            RequirementScope scope, int seed)
        {
            YarcInterop.PoolOverrideForTests = _ => true;

            void InjectAllYarcLogic(GenerationSettings _, LogicManagerBuilder lmb)
            {
                foreach (string term in new[] { "Vine-Root_Left", "Vine-Root_Right", "Vine-Durandas", "Chain-Storerooms", "Scarecrow" })
                {
                    lmb.GetOrAddTerm(term, TermType.SignedByte);
                    lmb.AddItem(new SingleItem(term, new TermValue(lmb.GetTerm(term), 1)));
                }

                lmb.AddItem(new SingleItem("Essence_Orb", new TermValue(lmb.GetTerm("ESSENCE"), 1)));
                lmb.AddItem(new EmptyItem("Soul_Jar"));
                lmb.AddItem(new EmptyItem("Jelly_Egg_Bomb"));
                lmb.AddItem(new EmptyItem("Lurien's_Telescope"));
                lmb.AddItem(new EmptyItem("Love_Jar-Empty"));
                lmb.AddItem(new EmptyItem("Love_Jar-Crawlid"));

                for (int i = 1; i <= 29; i++)
                {
                    lmb.AddLogicDef(new RawLogicDef($"DreamOrb_Crossroads_{i}", "*Whispering_Root-Crossroads"));
                }
                lmb.AddLogicDef(new RawLogicDef("Vine-Root_Left", "Fungus1_13[left1] + SIDESLASH"));
                lmb.AddLogicDef(new RawLogicDef("Vine-Root_Right", "Fungus1_13[left1] + Vine-Root_Left + SIDESLASH"));
                lmb.AddLogicDef(new RawLogicDef("Vine-Durandas", "Fungus1_09[left1] + SIDESLASH"));
                lmb.AddLogicDef(new RawLogicDef("Chain-Storerooms", "Ruins1_28 + SIDESLASH"));
                lmb.AddLogicDef(new RawLogicDef("Soul_Jar-Shade_Soul", "Ruins1_31"));
                lmb.AddLogicDef(new RawLogicDef("Hive_Platform-Lower_East", "Hive_03_c"));
                lmb.AddLogicDef(new RawLogicDef("Egg_Bomb-Map_Right", "Fungus3_25b[left1] | Fungus3_25b[right1]"));
                lmb.AddLogicDef(new RawLogicDef("Lurien's_Telescope", "Ruins2_Watcher_Room[bot1]"));
                lmb.AddLogicDef(new RawLogicDef("Scarecrow", "Deepnest_East_16[left1] | Deepnest_East_16[bot1]"));
                lmb.AddLogicDef(new RawLogicDef("Stalactite-Hornet_1", "Deepnest_East_12[left1] | Deepnest_East_12[right1]"));
                lmb.AddLogicDef(new RawLogicDef("Love_Jar-Outer_Lower", "Ruins2_11_b[right1] | Ruins2_11_b[left1]"));

                if (lmb.LogicLookup.ContainsKey("Defeated_Any_Great_Hopper"))
                {
                    lmb.DoSubst(new("Defeated_Any_Great_Hopper", "Deepnest_East_16[bot1]", "Scarecrow"));
                    lmb.DoSubst(new("Defeated_Any_Great_Hopper", "Deepnest_East_16[left1]", "Scarecrow"));
                }
            }

            void InjectAllYarcRequests(RequestBuilder rb)
            {
                for (int i = 1; i <= 29; i++)
                {
                    rb.AddLocationByName($"DreamOrb_Crossroads_{i}");
                    rb.AddItemByName("Essence_Orb");
                }
                rb.RemoveItemsWhere(item => item.StartsWith("Whispering_Root-", StringComparison.Ordinal));
                rb.RemoveLocationsWhere(loc => loc.StartsWith("Whispering_Root-", StringComparison.Ordinal));

                foreach (string vine in new[] { "Vine-Root_Left", "Vine-Root_Right", "Vine-Durandas", "Chain-Storerooms" })
                {
                    rb.AddLocationByName(vine);
                    rb.AddItemByName(vine);
                }

                rb.AddLocationByName("Soul_Jar-Shade_Soul");
                rb.AddItemByName("Soul_Jar");
                rb.AddLocationByName("Egg_Bomb-Map_Right");
                rb.AddItemByName("Jelly_Egg_Bomb");
                rb.AddLocationByName("Hive_Platform-Lower_East");
                rb.AddItemByName("One_Geo");
                rb.AddLocationByName("Lurien's_Telescope");
                rb.AddItemByName("Lurien's_Telescope");
                rb.AddLocationByName("Scarecrow");
                rb.AddItemByName("Scarecrow");
                rb.AddLocationByName("Stalactite-Hornet_1");
                rb.AddItemByName("One_Geo");
                rb.AddLocationByName("Love_Jar-Outer_Lower");
                rb.AddItemByName("Love_Jar-Empty");
            }

            RCData.RuntimeLogicOverride.Subscribe(50f, InjectAllYarcLogic);
            RequestBuilder.OnUpdate.Subscribe(-100f, InjectAllYarcRequests);
            try
            {
                NightmareAccess.Settings.CopyFrom(new NightmareAccessSettings
                {
                    Enabled = true,
                    Scope = scope,
                    RequirementChance = 100
                });

                var gs = new GenerationSettings { Seed = seed };
                gs.PoolSettings.WhisperingRoots = false;
                gs.Clamp();

                var rc = new RandoController(gs, new SettingsPM(gs), new RandomizerCore.RandoMonitor());
                rc.Run();

                CostApplier.PrepareAndVerify(rc.ctx, NightmareAccess.Settings, rc.gs, placements: null);
                WorldVerificationReport report = NightmareLogicHandler.LastVerificationReport;

                Assert.NotNull(report);
                Assert.True(report.IsCompletable, report.FormatSummary());
                Assert.Equal(report.BaselineLocationsCount, report.VerifiedLocationsCount);
                Assert.Equal(report.BaselineWaypointsCount, report.VerifiedWaypointsCount);
                Assert.Equal(report.BaselineTransitionsCount, report.VerifiedTransitionsCount);
                Assert.Empty(report.ConsistencyIssues);

                if (scope == RequirementScope.PerLocation)
                {
                    Assert.Contains("Vine-Root_Left", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("Chain-Storerooms", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("Hive_Platform-Lower_East", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("Scarecrow", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("Stalactite-Hornet_1", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("Lurien's_Telescope", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("Soul_Jar-Shade_Soul", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("Love_Jar-Outer_Lower", NightmareLogicHandler.LocationConditions.Keys);
                }
            }
            finally
            {
                RCData.RuntimeLogicOverride.Unsubscribe(50f, InjectAllYarcLogic);
                RequestBuilder.OnUpdate.Unsubscribe(-100f, InjectAllYarcRequests);
            }
        }

        [Fact]
        public void Verifier_DetectsAndRepairs_UniqueKeysDuplicateSimpleKeyInflation()
        {
            var gs = new GenerationSettings();
            var settings = new NightmareAccessSettings
            {
                Enabled = true,
                Scope = RequirementScope.PerLocation,
                AllowGeo = true,
                MinGeo = 10,
                MaxGeo = 100
            };

            var lmb = new LogicManagerBuilder();
            lmb.GetOrAddTerm("SIMPLE", TermType.SignedByte);
            lmb.GetOrAddTerm("Graveyard_Key", TermType.SignedByte);
            lmb.GetOrAddTerm("Waterways_Key", TermType.SignedByte);
            lmb.GetOrAddTerm("Pleasure_Key", TermType.SignedByte);
            lmb.GetOrAddTerm("Coffin_Key", TermType.SignedByte);
            lmb.GetOrAddTerm("GEO", TermType.SignedByte);
            lmb.GetOrAddTerm("Can_Replenish_Geo", TermType.SignedByte);
            lmb.AddWaypoint(new RawWaypointDef("Start_State", "TRUE", stateless: false));
            lmb.AddWaypoint(new RawWaypointDef("Can_Replenish_Geo", "TRUE", stateless: true));

            lmb.AddItem(new RandomizerCore.StringItems.StringItemTemplate("Graveyard_Key", "SIMPLE++ >> Graveyard_Key++"));
            lmb.AddItem(new RandomizerCore.StringItems.StringItemTemplate("Waterways_Key", "SIMPLE++ >> Waterways_Key++"));
            lmb.AddItem(new EmptyItem("One_Geo"));

            lmb.AddLogicDef(new RawLogicDef("Loc_1", "TRUE"));
            lmb.AddLogicDef(new RawLogicDef("Loc_2", "TRUE"));
            lmb.AddLogicDef(new RawLogicDef("Loc_3", "TRUE"));
            lmb.AddLogicDef(new RawLogicDef("Loc_Need3Keys", "TRUE"));

            var lm = new LogicManager(lmb);
            var ctx = new RandoModContext(lm)
            {
                GenerationSettings = gs,
                InitialProgression = new ProgressionInitializer()
            };

            ctx.itemPlacements.Add(new ItemPlacement(new RandoModItem { item = lm.GetItemStrict("Graveyard_Key") }, new RandoModLocation { logic = lm.GetLogicDefStrict("Loc_1") }));
            ctx.itemPlacements.Add(new ItemPlacement(new RandoModItem { item = lm.GetItemStrict("Graveyard_Key") }, new RandoModLocation { logic = lm.GetLogicDefStrict("Loc_2") }));
            ctx.itemPlacements.Add(new ItemPlacement(new RandoModItem { item = lm.GetItemStrict("Waterways_Key") }, new RandoModLocation { logic = lm.GetLogicDefStrict("Loc_3") }));
            ctx.itemPlacements.Add(new ItemPlacement(new RandoModItem { item = lm.GetItemStrict("One_Geo") }, new RandoModLocation { logic = lm.GetLogicDefStrict("Loc_Need3Keys") }));

            NightmareLogicHandler.LocationConditions["Loc_Need3Keys"] = new AccessCondition(AccessConditionType.SimpleKeys, 3);

            WorldVerificationReport before = CostValidator.Verify(ctx, RequirementScope.PerLocation, gs);
            Assert.False(before.IsCompletable);
            Assert.Contains(before.UnreachableLocations, s => s.Contains("Loc_Need3Keys"));

            var allowed = new List<AccessConditionType> { AccessConditionType.Geo, AccessConditionType.SimpleKeys };
            WorldVerificationReport after = CostValidator.ValidateAndRepair(ctx, new Random(19), allowed, settings, gs);
            Assert.True(after.IsCompletable, after.FormatSummary());
            Assert.Empty(after.UnreachableLocations);
        }

        [Theory]
        [InlineData(RequirementScope.PerLocation, 7001)]
        [InlineData(RequirementScope.PerObjectGroup, 7002)]
        [InlineData(RequirementScope.PerObject, 7003)]
        public void FullRandomizerSeedGeneration_WithAllSimulatedAccessRandomizerPools_IsVerifiedCompletable(
            RequirementScope scope, int seed)
        {
            AccessRandomizerInterop.PoolOverrideForTests = _ => true;

            string[] uniqueKeys = { "Graveyard_Key", "Waterways_Key", "Pleasure_Key", "Coffin_Key" };
            string[] customKeys = { "Mapper_Key", "Sly_Key", "Bretta_Key", "Zote_Key", "Relic_Key", "Birthplace_Key" };
            string[] shadeGates =
            {
                "Birthplace_Dead_End", "Fog_Canyon", "Markoth", "Overgrown_Mound",
                "Sharp_Shadow", "Traitor_Lord", "Void_Heart", "Void_Tendrils"
            };

            void InjectAccessLogic(GenerationSettings _, LogicManagerBuilder lmb)
            {
                lmb.AddItem(new BoolItem("Mantis_Respect", lmb.GetOrAddTerm("Mantis_Respect", TermType.SignedByte)));
                lmb.AddLogicDef(new RawLogicDef("Mantis_Respect", "Defeated_Mantis_Lords"));
                lmb.DoSubst(new("Fungus2_15[left1]", "Defeated_Mantis_Lords", "Mantis_Respect"));
                lmb.DoSubst(new("Fungus2_25[right1]", "Defeated_Mantis_Lords", "Mantis_Respect"));
                lmb.DoSubst(new("Fungus2_31[left1]", "Defeated_Mantis_Lords", "Mantis_Respect"));
                lmb.DoSubst(new("Bench-Mantis_Village", "Defeated_Mantis_Lords", "Mantis_Respect"));
                lmb.DoSubst(new("Mark_of_Pride", "Defeated_Mantis_Lords", "Mantis_Respect"));
                lmb.DoSubst(new("Geo_Chest-Mantis_Lords", "Defeated_Mantis_Lords", "Mantis_Respect"));
                lmb.DoSubst(new("Hallownest_Seal-Mantis_Lords", "Defeated_Mantis_Lords", "Mantis_Respect"));

                lmb.GetOrAddTerm("CHAINS", TermType.Int);
                lmb.AddItem(new RandomizerCore.StringItems.StringItemTemplate("Hollow_Knight_Chain", "CHAINS++"));
                for (int i = 1; i <= 4; i++)
                {
                    lmb.AddLogicDef(new RawLogicDef($"Hollow_Knight_Chain-{i}", $"Opened_Black_Egg_Temple + CHAINS>{i - 1}"));
                }

                foreach (string key in uniqueKeys)
                {
                    lmb.GetOrAddTerm(key, TermType.SignedByte);
                    lmb.AddItem(new RandomizerCore.StringItems.StringItemTemplate(key, $"SIMPLE++ >> {key}++"));
                }
                lmb.DoMacroEdit(new("JIJIUNLOCK", "Graveyard_Key"));
                lmb.DoMacroEdit(new("WATERWAYSUNLOCK", "Waterways_Key"));
                lmb.DoMacroEdit(new("PLEASUREHOUSEUNLOCK", "Pleasure_Key"));
                lmb.DoMacroEdit(new("GODTUNERUNLOCK", "Coffin_Key"));

                foreach (string key in customKeys)
                {
                    lmb.GetOrAddTerm(key, TermType.SignedByte);
                    lmb.AddItem(new RandomizerCore.StringItems.StringItemTemplate(key, $"{key}++"));
                }
                lmb.AddLogicDef(new("Mapper_Key", "Crossroads_33"));
                lmb.DoLogicEdit(new("Town[door_mapper]", "Town[door_mapper] | Town + Mapper_Key"));
                lmb.AddLogicDef(new("Sly_Key", "Crossroads_04[door1] | Crossroads_04[door_Mender_House] | Crossroads_04[door_charmshop] | Crossroads_04[right1] | (Crossroads_04[left1] | Crossroads_04[top1]) + Defeated_Gruz_Mother"));
                lmb.DoLogicEdit(new("Crossroads_04[door1]", "Crossroads_04[door1] | ORIG + Sly_Key"));
                lmb.AddLogicDef(new("Bretta_Key", "Fungus2_23 + (FULLCLAW + FULLDASH | FULLCLAW + FULLSUPERDASH | LEFTCLAW + WINGS)"));
                lmb.DoSubst(new("Town[door_bretta]", "Rescued_Bretta", "Bretta_Key"));
                lmb.AddLogicDef(new("Zote_Key", "Deepnest_33[top1]"));
                lmb.DoLogicEdit(new("Boss_Essence-Grey_Prince_Zote", "Room_Bretta[right1] + DREAMNAIL + Zote_Key + Defeated_Grey_Prince_Zote"));
                lmb.DoLogicEdit(new("Defeated_Grey_Prince_Zote", "Room_Bretta[right1] + DREAMNAIL + Zote_Key + COMBAT[Grey_Prince_Zote]"));
                lmb.AddLogicDef(new("Relic_Key", "Waterways_01[top1] | Waterways_01[right1] | Waterways_01 + (ANYCLAW | WINGS | ENEMYPOGOS)"));
                lmb.DoLogicEdit(new("Can_Visit_Lemm", "Ruins1_05b[top1] + Relic_Key"));
                lmb.GetOrAddTerm("Glade_Key", TermType.SignedByte);
                lmb.AddItem(new RandomizerCore.StringItems.StringItemTemplate("Glade_Key", "Glade_Key++"));
                lmb.DoLogicEdit(new("Opened_Glade_Door", "RestingGrounds_05 + Glade_Key"));
                lmb.AddLogicDef(new("Birthplace_Key", "Abyss_06_Core"));
                lmb.DoSubst(new("Abyss_06_Core[bot1]", "KINGSOUL", "Birthplace_Key"));

                lmb.GetOrAddTerm("Upper_Tram_Pass", TermType.SignedByte);
                lmb.AddItem(new RandomizerCore.StringItems.StringItemTemplate("Upper_Tram_Pass", "Upper_Tram_Pass++"));
                lmb.GetOrAddTerm("Lower_Tram_Pass", TermType.SignedByte);
                lmb.AddItem(new RandomizerCore.StringItems.StringItemTemplate("Lower_Tram_Pass", "Lower_Tram_Pass++"));
                lmb.AddLogicDef(new("Split_Tram_Pass", "*Tram_Pass"));
                lmb.DoSubst(new("Upper_Tram", "TRAM", "Upper_Tram_Pass"));
                lmb.DoSubst(new("Lower_Tram", "TRAM", "Lower_Tram_Pass"));

                lmb.GetOrAddTerm("Left_Elevator_Pass", TermType.SignedByte);
                lmb.AddItem(new RandomizerCore.StringItems.StringItemTemplate("Left_Elevator_Pass", "Left_Elevator_Pass++"));
                lmb.GetOrAddTerm("Right_Elevator_Pass", TermType.SignedByte);
                lmb.AddItem(new RandomizerCore.StringItems.StringItemTemplate("Right_Elevator_Pass", "Right_Elevator_Pass++"));
                lmb.AddLogicDef(new("Split_Elevator_Pass", "(Ruins2_10b[left1] | Ruins2_10b[right1] | Ruins2_10b[right2] | Right_Elevator) + Can_Replenish_Geo"));
                lmb.DoSubst(new("Left_Elevator", "Elevator_Pass", "Left_Elevator_Pass"));
                lmb.DoSubst(new("Right_Elevator", "Elevator_Pass", "Right_Elevator_Pass"));

                lmb.GetOrAddTerm("Trap_Bench", TermType.SignedByte);
                lmb.AddItem(new RandomizerCore.StringItems.StringItemTemplate("Trap_Bench", "Trap_Bench++"));
                lmb.AddLogicDef(new("Trap_Bench", "Deepnest_Spider_Town[left1]"));
                lmb.DoSubst(new("Herrah", "Deepnest_Spider_Town[left1]", "Deepnest_Spider_Town[left1] + (Trap_Bench | WINGS)"));

                foreach (string gate in shadeGates)
                {
                    string name = $"Shade_Gate-{gate}";
                    lmb.GetOrAddTerm(name);
                    lmb.AddItem(new RandomizerCore.StringItems.StringItemTemplate(name, $"{name}++"));
                }
                lmb.AddLogicDef(new("Shade_Gate-Birthplace_Dead_End", "Abyss_15[top1] + (RIGHTSHADOWDASH | Shade_Gate-Birthplace_Dead_End + LEFTSHADOWDASH)"));
                lmb.AddLogicDef(new("Shade_Gate-Fog_Canyon", "Fungus3_25[left1] + (RIGHTSHADOWDASH | Shade_Gate-Fog_Canyon + LEFTSHADOWDASH) | Fungus3_25[right1] + (LEFTSHADOWDASH | Shade_Gate-Fog_Canyon + RIGHTSHADOWDASH)"));
                lmb.AddLogicDef(new("Shade_Gate-Markoth", "Deepnest_East_18[right2] + (LEFTSHADOWDASH | Shade_Gate-Markoth + RIGHTSHADOWDASH)"));
                lmb.AddLogicDef(new("Shade_Gate-Overgrown_Mound", "Fungus3_44 + (LEFTSHADOWDASH | Shade_Gate-Overgrown_Mound + RIGHTSHADOWDASH)"));
                lmb.AddLogicDef(new("Shade_Gate-Sharp_Shadow", "Deepnest_44[top1] + (RIGHTSHADOWDASH | Shade_Gate-Sharp_Shadow + LEFTSHADOWDASH)"));
                lmb.AddLogicDef(new("Shade_Gate-Traitor_Lord", "Fungus3_23[right1] + (LEFTSHADOWDASH | Shade_Gate-Traitor_Lord + RIGHTSHADOWDASH)"));
                lmb.AddLogicDef(new("Shade_Gate-Void_Heart", "Abyss_15[top1] + (RIGHTSHADOWDASH | Shade_Gate-Void_Heart + LEFTSHADOWDASH)"));
                lmb.AddLogicDef(new("Shade_Gate-Void_Tendrils", "Abyss_10[left2] + (RIGHTSHADOWDASH | Shade_Gate-Void_Tendrils + LEFTSHADOWDASH)"));
                lmb.DoSubst(new("Fungus3_25[left1]", "LEFTSHADOWDASH", "Shade_Gate-Fog_Canyon"));
                lmb.DoSubst(new("Fungus3_25[right1]", "RIGHTSHADOWDASH", "Shade_Gate-Fog_Canyon"));
                lmb.DoSubst(new("Fog_Canyon_Map", "RIGHTSHADOWDASH", "Shade_Gate-Fog_Canyon"));
            }

            void InjectAccessRequests(RequestBuilder rb)
            {
                void AddDefItem(string name, bool major = false)
                {
                    rb.AddItemByName(name);
                    rb.EditItemRequest(name, info => info.getItemDef = () => new() { Name = name, Pool = "Key", PriceCap = 500, MajorItem = major });
                }

                void AddDefLoc(string name)
                {
                    rb.AddLocationByName(name);
                    rb.EditLocationRequest(name, info => info.getLocationDef = () => new() { Name = name, SceneName = "Crossroads_01" });
                }

                AddDefItem("Mantis_Respect");
                AddDefLoc("Mantis_Respect");

                rb.AddItemByName("Hollow_Knight_Chain", 4);
                rb.EditItemRequest("Hollow_Knight_Chain", info => info.getItemDef = () => new() { Name = "Hollow_Knight_Chain", Pool = "Dreamers", PriceCap = 2000, MajorItem = true });
                for (int i = 1; i <= 4; i++) AddDefLoc($"Hollow_Knight_Chain-{i}");

                rb.RemoveItemByName("Simple_Key");
                rb.RemoveItemByName($"{PlaceholderItem.Prefix}Simple_Key");
                foreach (string k in uniqueKeys)
                {
                    AddDefItem(k);
                    rb.AddItemByName($"{PlaceholderItem.Prefix}{k}");
                }

                foreach (string k in customKeys)
                {
                    AddDefItem(k);
                    AddDefLoc(k);
                }
                AddDefItem("Glade_Key");

                rb.RemoveItemByName("Tram_Pass");
                rb.RemoveItemByName($"{PlaceholderItem.Prefix}Tram_Pass");
                AddDefItem("Upper_Tram_Pass");
                AddDefItem("Lower_Tram_Pass");
                AddDefLoc("Split_Tram_Pass");

                rb.RemoveItemByName("Elevator_Pass");
                rb.RemoveItemByName($"{PlaceholderItem.Prefix}Elevator_Pass");
                AddDefItem("Left_Elevator_Pass");
                AddDefItem("Right_Elevator_Pass");
                AddDefLoc("Split_Elevator_Pass");

                AddDefItem("Trap_Bench");
                AddDefLoc("Trap_Bench");

                foreach (string gate in shadeGates)
                {
                    string name = $"Shade_Gate-{gate}";
                    AddDefItem(name);
                    AddDefLoc(name);
                }
            }

            RCData.RuntimeLogicOverride.Subscribe(11f, InjectAccessLogic);
            RequestBuilder.OnUpdate.Subscribe(0f, InjectAccessRequests);
            try
            {
                NightmareAccess.Settings.CopyFrom(new NightmareAccessSettings
                {
                    Enabled = true,
                    Scope = scope,
                    RequirementChance = 100
                });

                var gs = new GenerationSettings { Seed = seed };
                gs.PoolSettings.Keys = true;
                gs.NoveltySettings.RandomizeElevatorPass = true;
                gs.DuplicateItemSettings.DuplicateUniqueKeys = true;
                gs.Clamp();

                var rc = new RandoController(gs, new SettingsPM(gs), new RandomizerCore.RandoMonitor());
                rc.Run();

                CostApplier.PrepareAndVerify(rc.ctx, NightmareAccess.Settings, rc.gs, placements: null);
                WorldVerificationReport report = NightmareLogicHandler.LastVerificationReport;

                Assert.NotNull(report);
                Assert.True(report.IsCompletable, report.FormatSummary());
                Assert.Equal(report.BaselineLocationsCount, report.VerifiedLocationsCount);
                Assert.Equal(report.BaselineWaypointsCount, report.VerifiedWaypointsCount);
                Assert.Equal(report.BaselineTransitionsCount, report.VerifiedTransitionsCount);
                Assert.Empty(report.ConsistencyIssues);

                if (scope == RequirementScope.PerLocation)
                {
                    Assert.Contains("Mantis_Respect", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("Hollow_Knight_Chain-1", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("Mapper_Key", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("Split_Tram_Pass", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("Split_Elevator_Pass", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("Trap_Bench", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("Shade_Gate-Fog_Canyon", NightmareLogicHandler.LocationConditions.Keys);
                }
            }
            finally
            {
                RCData.RuntimeLogicOverride.Unsubscribe(11f, InjectAccessLogic);
                RequestBuilder.OnUpdate.Unsubscribe(0f, InjectAccessRequests);
            }
        }

        [Theory]
        [InlineData(RequirementScope.PerLocation, 8001)]
        [InlineData(RequirementScope.PerObjectGroup, 8002)]
        [InlineData(RequirementScope.PerObject, 8003)]
        public void FullRandomizerSeedGeneration_WithSimulatedWaveAMods_IsVerifiedCompletable(
            RequirementScope scope, int seed)
        {
            TheGloryOfBeingAFoolInterop.EnabledOverrideForTests = () => true;
            FishingInterop.EnabledOverrideForTests = () => true;
            MoreStagsInterop.ActiveStagsOverrideForTests = () => new[] { "Stag-East_Crossroads" };
            BenchRandoInterop.RandomizedBenchesOverrideForTests = () => new[] { "Bench-Dirtmouth" };
            MoreLocationsInterop.MiscOverrideForTests = () => new[] { "Swim", "Stag_Nest_Egg" };
            RandoPlusInterop.PoolOverrideForTests = k => k is "MrMushroom" or "NailUpgrades";
            CombatRandomizerInterop.FragmentsPerNotchOverrideForTests = () => 3;
            CurseRandomizerInterop.WalletCapacitiesOverrideForTests = () => new[] { 500, 1000, 1500, 2000 };
            CurseRandomizerInterop.CursedVesselCountOverrideForTests = () => 1;

            void InjectWaveALogic(GenerationSettings _, LogicManagerBuilder lmb)
            {
                lmb.GetOrAddTerm("The_Glory_of_Being_a_Fool");
                lmb.AddItem(new SingleItem("The_Glory_of_Being_a_Fool", new TermValue(lmb.GetTerm("The_Glory_of_Being_a_Fool"), 1)));
                lmb.LogicLookup["The_Glory_of_Being_a_Fool-Colosseum"] = lmb.LogicLookup[ItemChanger.LocationNames.Pale_Ore_Colosseum];

                lmb.AddLogicDef(new RawLogicDef("Fishing_Spot-Lake_of_Unn", "Fungus1_26[door_SlugShrine]"));

                lmb.GetOrAddTerm("Stag-East_Crossroads");
                lmb.AddItem(new MultiItem("Stag-East_Crossroads", new[]
                {
                    new TermValue(lmb.GetTerm("Stag-East_Crossroads"), 1),
                    new TermValue(lmb.GetTerm("STAGS"), 1)
                }));
                lmb.AddLogicDef(new RawLogicDef("Stag-East_Crossroads", "Crossroads_31[right1]"));

                lmb.GetOrAddTerm("Bench_Item_Term-Dirtmouth");
                lmb.AddItem(new RandomizerCore.StringItems.StringItemTemplate("Bench-Dirtmouth", "Bench_Item_Term-Dirtmouth++"));
                lmb.AddWaypoint(new RawWaypointDef("Bench-Dirtmouth", "Town", stateless: false));

                lmb.AddLogicDef(new RawLogicDef("Swim", "Crossroads_50[right1] + Monomon"));
                lmb.AddLogicDef(new RawLogicDef("Stag_Nest_Egg", "Cliffs_03[right1] + (WINGS | LEFTSUPERDASH | FULLCLAW | LEFTDASH)"));

                lmb.GetOrAddTerm("MRMUSHROOMFOUND");
                lmb.AddItem(new SingleItem("Mr_Mushroom_Level_Up", new TermValue(lmb.GetTerm("MRMUSHROOMFOUND"), 1)));
                lmb.AddLogicDef(new RawLogicDef("Mr_Mushroom-Fungal_Wastes", "Fungus2_18[top1] + Spore_Shroom"));

                lmb.GetOrAddTerm("NAILUPGRADE");
                lmb.AddItem(new SingleItem("Nail_Upgrade", new TermValue(lmb.GetTerm("NAILUPGRADE"), 1)));
                lmb.AddLogicDef(new RawLogicDef("Nailsmith_Upgrade_1", "Room_nailsmith[left1]"));
                lmb.AddLogicDef(new RawLogicDef("Nailsmith_Upgrade_2", "Room_nailsmith[left1] + PALEORE > 0"));

                lmb.AddItem(new RandomizerCore.StringItems.StringItemTemplate("Notch_Fragment", "NOTCHES++"));

                lmb.GetOrAddTerm("WALLET");
                lmb.AddItem(new SingleItem("Geo_Wallet", new TermValue(lmb.GetTerm("WALLET"), 1)));
                lmb.AddItem(new EmptyItem("Fool_Item-Mantis_Claw"));
                lmb.AddItem(new EmptyItem("Fool_Item-Evil_Mask_Shard"));
                lmb.AddItem(new EmptyItem("Alphabet-A"));
                lmb.AddItem(new EmptyItem("Color-Red"));
                lmb.AddItem(new EmptyItem("Wanderer's_Journal_Sale"));
            }

            void InjectWaveARequests(RequestBuilder rb)
            {
                void AddDefItem(string name, string pool = "Misc")
                {
                    rb.AddItemByName(name);
                    rb.EditItemRequest(name, info => info.getItemDef = () => new() { Name = name, Pool = pool, PriceCap = 500 });
                }
                void AddDefLoc(string name)
                {
                    rb.AddLocationByName(name);
                    rb.EditLocationRequest(name, info => info.getLocationDef = () => new() { Name = name, SceneName = "Crossroads_01" });
                }

                AddDefItem("The_Glory_of_Being_a_Fool");
                AddDefLoc("The_Glory_of_Being_a_Fool-Colosseum");
                AddDefLoc("Fishing_Spot-Lake_of_Unn");
                AddDefItem("Stag-East_Crossroads", "Stag");
                AddDefLoc("Stag-East_Crossroads");
                AddDefItem("Bench-Dirtmouth", "Benches");
                AddDefLoc("Bench-Dirtmouth");
                AddDefLoc("Swim");
                AddDefLoc("Stag_Nest_Egg");
                AddDefItem("Mr_Mushroom_Level_Up");
                AddDefLoc("Mr_Mushroom-Fungal_Wastes");
                AddDefItem("Nail_Upgrade");
                AddDefLoc("Nailsmith_Upgrade_1");
                AddDefLoc("Nailsmith_Upgrade_2");

                rb.RemoveItemByName("Charm_Notch");
                rb.AddItemByName("Notch_Fragment", 12);
                rb.EditItemRequest("Notch_Fragment", info => info.getItemDef = () => new() { Name = "Notch_Fragment", Pool = "Charm Notches", PriceCap = 500 });

                rb.AddItemByName("Geo_Wallet", 4);
                rb.EditItemRequest("Geo_Wallet", info => info.getItemDef = () => new() { Name = "Geo_Wallet", Pool = "Misc", PriceCap = 500 });
                AddDefItem("Fool_Item-Mantis_Claw");
                AddDefItem("Fool_Item-Evil_Mask_Shard");
                AddDefItem("Alphabet-A");
                AddDefItem("Color-Red");
                AddDefItem("Wanderer's_Journal_Sale", "Geo");
            }

            RCData.RuntimeLogicOverride.Subscribe(50f, InjectWaveALogic);
            RequestBuilder.OnUpdate.Subscribe(0f, InjectWaveARequests);
            try
            {
                NightmareAccess.Settings.CopyFrom(new NightmareAccessSettings
                {
                    Enabled = true,
                    Scope = scope,
                    RequirementChance = 100
                });

                var gs = new GenerationSettings { Seed = seed };
                gs.Clamp();

                var rc = new RandoController(gs, new SettingsPM(gs), new RandomizerCore.RandoMonitor());
                rc.Run();

                CostApplier.PrepareAndVerify(rc.ctx, NightmareAccess.Settings, rc.gs, placements: null);
                WorldVerificationReport report = NightmareLogicHandler.LastVerificationReport;

                Assert.NotNull(report);
                Assert.True(report.IsCompletable, report.FormatSummary());
                Assert.Equal(report.BaselineLocationsCount, report.VerifiedLocationsCount);
                Assert.Equal(report.BaselineWaypointsCount, report.VerifiedWaypointsCount);
                Assert.Equal(report.BaselineTransitionsCount, report.VerifiedTransitionsCount);
                Assert.Empty(report.ConsistencyIssues);

                if (scope == RequirementScope.PerLocation)
                {
                    Assert.Contains("The_Glory_of_Being_a_Fool-Colosseum", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("Fishing_Spot-Lake_of_Unn", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("Stag-East_Crossroads", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("Bench-Dirtmouth", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("Swim", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("Stag_Nest_Egg", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("Mr_Mushroom-Fungal_Wastes", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("Nailsmith_Upgrade_1", NightmareLogicHandler.LocationConditions.Keys);
                }
            }
            finally
            {
                RCData.RuntimeLogicOverride.Unsubscribe(50f, InjectWaveALogic);
                RequestBuilder.OnUpdate.Unsubscribe(0f, InjectWaveARequests);
            }
        }

        [Fact]
        public void Verifier_DetectsAndRepairs_ExtraRandoSplitFireballLv2()
        {
            ExtraRandoInterop.SettingOverrideForTests = k => k is "Enabled" or "SplitFireball";

            var gs = new GenerationSettings();
            gs.PoolSettings.Skills = true;
            var settings = new NightmareAccessSettings
            {
                Enabled = true,
                Scope = RequirementScope.PerLocation,
                AllowGeo = true,
                MinGeo = 10,
                MaxGeo = 100
            };

            var lmb = new LogicManagerBuilder();
            lmb.GetOrAddTerm("FIREBALL", TermType.SignedByte);
            lmb.GetOrAddTerm("FIREBALLLEFT", TermType.SignedByte);
            lmb.GetOrAddTerm("FIREBALLRIGHT", TermType.SignedByte);
            lmb.GetOrAddTerm("SPELLS", TermType.SignedByte);
            lmb.GetOrAddTerm("GEO", TermType.SignedByte);
            lmb.GetOrAddTerm("Can_Replenish_Geo", TermType.SignedByte);
            lmb.AddWaypoint(new RawWaypointDef("Start_State", "TRUE", stateless: false));
            lmb.AddWaypoint(new RawWaypointDef("Can_Replenish_Geo", "TRUE", stateless: true));

            lmb.AddItem(new MultiItem("Left_Vengeful_Spirit", new[]
            {
                new TermValue(lmb.GetTerm("FIREBALL"), 1),
                new TermValue(lmb.GetTerm("FIREBALLLEFT"), 1),
                new TermValue(lmb.GetTerm("SPELLS"), 1)
            }));
            lmb.AddItem(new MultiItem("Right_Vengeful_Spirit", new[]
            {
                new TermValue(lmb.GetTerm("FIREBALL"), 1),
                new TermValue(lmb.GetTerm("FIREBALLRIGHT"), 1),
                new TermValue(lmb.GetTerm("SPELLS"), 1)
            }));
            lmb.AddItem(new MultiItem("Left_Shade_Soul", new[]
            {
                new TermValue(lmb.GetTerm("FIREBALL"), 1),
                new TermValue(lmb.GetTerm("FIREBALLLEFT"), 1),
                new TermValue(lmb.GetTerm("SPELLS"), 1)
            }));
            lmb.AddItem(new EmptyItem("One_Geo"));

            lmb.AddLogicDef(new RawLogicDef("Loc_LeftVS", "TRUE"));
            lmb.AddLogicDef(new RawLogicDef("Loc_RightVS", "TRUE"));
            lmb.AddLogicDef(new RawLogicDef("Loc_NeedLv2", "TRUE"));
            lmb.AddLogicDef(new RawLogicDef("Loc_AfterLv2", "TRUE"));

            var lm = new LogicManager(lmb);
            var ctx = new RandoModContext(lm)
            {
                GenerationSettings = gs,
                InitialProgression = new ProgressionInitializer()
            };

            ctx.itemPlacements.Add(new ItemPlacement(new RandoModItem { item = lm.GetItemStrict("Left_Vengeful_Spirit") }, new RandoModLocation { logic = lm.GetLogicDefStrict("Loc_LeftVS") }));
            ctx.itemPlacements.Add(new ItemPlacement(new RandoModItem { item = lm.GetItemStrict("Right_Vengeful_Spirit") }, new RandoModLocation { logic = lm.GetLogicDefStrict("Loc_RightVS") }));
            // Left_Shade_Soul is behind Loc_NeedLv2, which itself requires Fireball Lv2 -> impossible without repair!
            ctx.itemPlacements.Add(new ItemPlacement(new RandoModItem { item = lm.GetItemStrict("Left_Shade_Soul") }, new RandoModLocation { logic = lm.GetLogicDefStrict("Loc_NeedLv2") }));
            ctx.itemPlacements.Add(new ItemPlacement(new RandoModItem { item = lm.GetItemStrict("One_Geo") }, new RandoModLocation { logic = lm.GetLogicDefStrict("Loc_AfterLv2") }));

            NightmareLogicHandler.LocationConditions["Loc_NeedLv2"] = new AccessCondition(AccessConditionType.FireballLevel, 2);
            NightmareLogicHandler.LocationConditions["Loc_AfterLv2"] = new AccessCondition(AccessConditionType.FireballLevel, 2);

            WorldVerificationReport before = CostValidator.Verify(ctx, RequirementScope.PerLocation, gs);
            Assert.False(before.IsCompletable);
            Assert.Contains(before.UnreachableLocations, s => s.Contains("Loc_NeedLv2"));
            Assert.Contains(before.UnreachableLocations, s => s.Contains("Loc_AfterLv2"));

            var allowed = new List<AccessConditionType> { AccessConditionType.Geo, AccessConditionType.FireballLevel };
            WorldVerificationReport after = CostValidator.ValidateAndRepair(ctx, new Random(31), allowed, settings, gs);
            Assert.True(after.IsCompletable, after.FormatSummary());
            Assert.Empty(after.UnreachableLocations);
            // Once Loc_NeedLv2 is repaired and gives Left_Shade_Soul, Loc_AfterLv2 (Fireball Lv2) stays valid!
            Assert.Equal(AccessConditionType.FireballLevel, NightmareLogicHandler.LocationConditions["Loc_AfterLv2"].Type);
            Assert.Equal(2, NightmareLogicHandler.LocationConditions["Loc_AfterLv2"].RequiredAmount);
        }

        [Theory]
        [InlineData(RequirementScope.PerLocation, 9001)]
        [InlineData(RequirementScope.PerObjectGroup, 9002)]
        [InlineData(RequirementScope.PerObject, 9003)]
        public void FullRandomizerSeedGeneration_WithSimulatedWaveBMods_IsVerifiedCompletable(
            RequirementScope scope, int seed)
        {
            ExtraRandoInterop.SettingOverrideForTests = k => k is "Enabled"
                or "SplitShadeCloak"
                or "SplitFireball"
                or "UseKeyring"
                or "RandomizeButt"
                or "RandomizeAwfulLocations"
                or "BlockEarlyGameStags";

            void InjectWaveBLogic(GenerationSettings _, LogicManagerBuilder lmb)
            {
                lmb.AddItem(new SingleItem("Progressive_Left_Cloak", new TermValue(lmb.GetTerm("LEFTDASH"), 1)));
                lmb.AddItem(new SingleItem("Progressive_Right_Cloak", new TermValue(lmb.GetTerm("RIGHTDASH"), 1)));
                lmb.DoMacroEdit(new("LEFTSHADOWDASH", "LEFTDASH>1"));
                lmb.DoMacroEdit(new("RIGHTSHADOWDASH", "RIGHTDASH>1"));

                lmb.GetOrAddTerm("FIREBALLLEFT");
                lmb.GetOrAddTerm("FIREBALLRIGHT");
                foreach (string item in new[] { "Left_Vengeful_Spirit", "Left_Shade_Soul" })
                {
                    lmb.AddItem(new MultiItem(item, new[]
                    {
                        new TermValue(lmb.GetTerm("FIREBALL"), 1),
                        new TermValue(lmb.GetTerm("FIREBALLLEFT"), 1),
                        new TermValue(lmb.GetTerm("SPELLS"), 1)
                    }));
                }
                foreach (string item in new[] { "Right_Vengeful_Spirit", "Right_Shade_Soul" })
                {
                    lmb.AddItem(new MultiItem(item, new[]
                    {
                        new TermValue(lmb.GetTerm("FIREBALL"), 1),
                        new TermValue(lmb.GetTerm("FIREBALLRIGHT"), 1),
                        new TermValue(lmb.GetTerm("SPELLS"), 1)
                    }));
                }

                lmb.LogicLookup["Split_Shade_Soul"] = lmb.LogicLookup["Shade_Soul"];
                lmb.LogicLookup["GPZ_10"] = lmb.LogicLookup["Boss_Essence-Grey_Prince_Zote"];
                lmb.AddLogicDef(new RawLogicDef("Bardoon_Butt", "Deepnest_East_04 + (LEFTCLAW | RIGHTCLAW + WINGS)"));
                lmb.AddItem(new EmptyItem("Bardoon_Butt_Smack"));
                lmb.AddItem(new SingleItem("Key_Ring", new TermValue(lmb.GetTerm("SIMPLE"), 4)));
                lmb.AddItem(new EmptyItem("Pantheon_Access-Master"));
                lmb.AddItem(new EmptyItem("Colo_Ticket-Bronze"));
                lmb.AddItem(new EmptyItem("Token_Marker_Hint"));
                lmb.AddItem(new EmptyItem("Dirtmouth_Stag_Key"));
                lmb.AddLogicDef(new RawLogicDef("Dirtmouth_Stag_Door", "Town"));

                lmb.GetOrAddTerm("Total_Broken_Walls", TermType.Int);
                lmb.GetOrAddTerm("Broken_Walls");
                lmb.AddItem(new RandomizerCore.StringItems.StringItemTemplate("Wall-420_Geo_Rock", "Total_Broken_Walls++ >> Broken_Walls++"));
                lmb.AddItem(new RandomizerCore.StringItems.StringItemTemplate("Dive_Floor-Basin_Grub", "Total_Broken_Walls++"));
                lmb.AddLogicDef(new RawLogicDef("Myla_Shop", "Crossroads_45[left1]"));

                lmb.AddItem(new EmptyItem("Statue_Mark-Gruz_Mother"));
                lmb.AddItem(new EmptyItem("Pantheon_Master-Completion"));
                lmb.AddItem(new EmptyItem("Godhome_Lifeblood"));
                lmb.AddLogicDef(new RawLogicDef("Godhome_Shop", "Town"));

                lmb.AddItem(new EmptyItem("Precept_1"));
                lmb.AddItem(new EmptyItem("Inspect-Stag_Egg"));
                lmb.AddItem(new EmptyItem("Listen_Ability"));
                lmb.AddItem(new EmptyItem("Read_Ability"));
                lmb.AddLogicDef(new RawLogicDef("Elderbug_Shop", "Town"));
                lmb.AddLogicDef(new RawLogicDef("Gorb_Grave", "Cliffs_02"));
            }

            void InjectWaveBRequests(RequestBuilder rb)
            {
                void AddDefItem(string name, string pool = "Misc", bool major = false)
                {
                    rb.AddItemByName(name);
                    rb.EditItemRequest(name, info => info.getItemDef = () => new() { Name = name, Pool = pool, PriceCap = 500, MajorItem = major });
                }
                void AddDefLoc(string name, string sceneName = "Crossroads_01")
                {
                    rb.AddLocationByName(name);
                    rb.EditLocationRequest(name, info => info.getLocationDef = () => new() { Name = name, SceneName = sceneName });
                }

                rb.RemoveItemByName("Mothwing_Cloak");
                rb.RemoveItemByName("Shade_Cloak");
                rb.RemoveItemByName("Split_Shade_Cloak");
                rb.RemoveItemByName("Left_Mothwing_Cloak");
                rb.RemoveItemByName("Right_Mothwing_Cloak");
                rb.AddItemByName("Progressive_Left_Cloak", 2);
                rb.AddItemByName("Progressive_Right_Cloak", 2);
                rb.EditItemRequest("Progressive_Left_Cloak", info => info.getItemDef = () => new() { Name = "Progressive_Left_Cloak", Pool = "Skill", PriceCap = 500, MajorItem = true });
                rb.EditItemRequest("Progressive_Right_Cloak", info => info.getItemDef = () => new() { Name = "Progressive_Right_Cloak", Pool = "Skill", PriceCap = 500, MajorItem = true });

                rb.RemoveItemByName("Vengeful_Spirit");
                rb.RemoveItemByName("Shade_Soul");
                AddDefItem("Left_Vengeful_Spirit", "Skill", major: true);
                AddDefItem("Right_Vengeful_Spirit", "Skill", major: true);
                AddDefItem("Left_Shade_Soul", "Skill", major: true);
                AddDefItem("Right_Shade_Soul", "Skill", major: true);

                rb.RemoveItemByName("Simple_Key");
                AddDefItem("Key_Ring", "Key", major: true);

                AddDefLoc("Split_Shade_Soul", "Ruins1_31b");
                AddDefLoc("GPZ_10", "Room_Bretta_Basement");
                AddDefLoc("Bardoon_Butt", "Deepnest_East_04");
                AddDefItem("Bardoon_Butt_Smack", "Key");
                AddDefItem("Pantheon_Access-Master", "Key");
                AddDefItem("Colo_Ticket-Bronze", "Key");
                AddDefItem("Token_Marker_Hint", "Map");
                rb.AddToVanilla("Dirtmouth_Stag_Key", "Dirtmouth_Stag_Door");

                AddDefItem("Wall-420_Geo_Rock", "Walls");
                AddDefItem("Dive_Floor-Basin_Grub", "Dive_Floors");
                AddDefItem("Statue_Mark-Gruz_Mother", "Statue Marks");
                AddDefItem("Pantheon_Master-Completion", "Pantheon Marks", major: true);
                AddDefItem("Godhome_Lifeblood", "Lifeblood");
                AddDefItem("Precept_1", "Lore");
                AddDefItem("Inspect-Stag_Egg", "Lore");
                AddDefItem("Listen_Ability", "Skill", major: true);
                AddDefItem("Read_Ability", "Skill", major: true);
                AddDefLoc("Gorb_Grave", "Cliffs_02");
            }

            RCData.RuntimeLogicOverride.Subscribe(1050f, InjectWaveBLogic);
            RequestBuilder.OnUpdate.Subscribe(0f, InjectWaveBRequests);
            try
            {
                NightmareAccess.Settings.CopyFrom(new NightmareAccessSettings
                {
                    Enabled = true,
                    Scope = scope,
                    RequirementChance = 100
                });

                var gs = new GenerationSettings { Seed = seed };
                gs.Clamp();

                var rc = new RandoController(gs, new SettingsPM(gs), new RandomizerCore.RandoMonitor());
                rc.Run();

                CostApplier.PrepareAndVerify(rc.ctx, NightmareAccess.Settings, rc.gs, placements: null);
                WorldVerificationReport report = NightmareLogicHandler.LastVerificationReport;

                Assert.NotNull(report);
                Assert.True(report.IsCompletable, report.FormatSummary());
                Assert.Equal(report.BaselineLocationsCount, report.VerifiedLocationsCount);
                Assert.Equal(report.BaselineWaypointsCount, report.VerifiedWaypointsCount);
                Assert.Equal(report.BaselineTransitionsCount, report.VerifiedTransitionsCount);
                Assert.Empty(report.ConsistencyIssues);

                Assert.False(NightmareLogicHandler.LocationConditions.ContainsKey("Myla_Shop"));
                Assert.False(NightmareLogicHandler.LocationConditions.ContainsKey("Godhome_Shop"));
                Assert.False(NightmareLogicHandler.LocationConditions.ContainsKey("Elderbug_Shop"));
                Assert.False(NightmareLogicHandler.LocationConditions.ContainsKey("Dirtmouth_Stag_Door"));
                Assert.False(NightmareLogicHandler.LocationConditions.ContainsKey("Gorb_Grave"));

                if (scope == RequirementScope.PerLocation)
                {
                    Assert.Contains("Split_Shade_Soul", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("GPZ_10", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("Bardoon_Butt", NightmareLogicHandler.LocationConditions.Keys);
                }
            }
            finally
            {
                RCData.RuntimeLogicOverride.Unsubscribe(1050f, InjectWaveBLogic);
                RequestBuilder.OnUpdate.Unsubscribe(0f, InjectWaveBRequests);
            }
        }

        [Theory]
        [InlineData(RequirementScope.PerLocation, 9801)]
        [InlineData(RequirementScope.PerObjectGroup, 9802)]
        [InlineData(RequirementScope.PerObject, 9803)]
        public void FullRandomizerSeedGeneration_WithSimulatedExtraRandoBatch4_IsVerifiedCompletable(
            RequirementScope scope, int seed)
        {
            ExtraRandoInterop.SettingOverrideForTests = k => k is "Enabled"
                or "SplitFireball"
                or "RandomizeHotSprings"
                or "RandomizePantheonAccess"
                or "RandomizeAwfulLocations";

            string[] hotSprings =
            {
                "Crossroads-Hot_Spring", "Colosseum-Hot_Spring", "Deepnest-Hot_Spring",
                "Bathhouse-Hot_Spring", "Lower_Godhome-Hot_Spring", "Upper_Godhome-Hot_Spring"
            };
            string[] pantheons =
            {
                "Pantheon-Master", "Pantheon-Artist", "Pantheon-Sage",
                "Pantheon-Knight", "Pantheon-Hallownest"
            };

            void InjectBatch4Logic(GenerationSettings _, LogicManagerBuilder lmb)
            {
                lmb.GetOrAddTerm("FIREBALLLEFT");
                lmb.GetOrAddTerm("FIREBALLRIGHT");
                foreach (string item in new[] { "Left_Vengeful_Spirit", "Left_Shade_Soul" })
                {
                    lmb.AddItem(new MultiItem(item, new[]
                    {
                        new TermValue(lmb.GetTerm("FIREBALL"), 1),
                        new TermValue(lmb.GetTerm("FIREBALLLEFT"), 1),
                        new TermValue(lmb.GetTerm("SPELLS"), 1)
                    }));
                }
                foreach (string item in new[] { "Right_Vengeful_Spirit", "Right_Shade_Soul" })
                {
                    lmb.AddItem(new MultiItem(item, new[]
                    {
                        new TermValue(lmb.GetTerm("FIREBALL"), 1),
                        new TermValue(lmb.GetTerm("FIREBALLRIGHT"), 1),
                        new TermValue(lmb.GetTerm("SPELLS"), 1)
                    }));
                }

                lmb.LogicLookup["Split_Vengeful_Spirit"] = lmb.LogicLookup["Vengeful_Spirit"];
                lmb.LogicLookup["Split_Shade_Soul"] = lmb.LogicLookup["Shade_Soul"];
                lmb.LogicLookup["GPZ_10"] = lmb.LogicLookup["Boss_Essence-Grey_Prince_Zote"];
                lmb.LogicLookup["White_Defender_5"] = lmb.LogicLookup["Boss_Essence-White_Defender"];

                foreach (string hs in hotSprings)
                {
                    lmb.AddLogicDef(new RawLogicDef(hs, "Town"));
                }
                foreach (string p in pantheons)
                {
                    lmb.AddLogicDef(new RawLogicDef(p, "GG_Atrium"));
                }
                lmb.AddItem(new EmptyItem("Hot_Spring_Water"));
                lmb.AddItem(new EmptyItem("Pantheon_Access-Master"));
            }

            void InjectBatch4Requests(RequestBuilder rb)
            {
                rb.RemoveItemByName("Vengeful_Spirit");
                rb.RemoveItemByName("Shade_Soul");
                foreach (string item in new[] { "Left_Vengeful_Spirit", "Right_Vengeful_Spirit", "Left_Shade_Soul", "Right_Shade_Soul" })
                {
                    rb.AddItemByName(item);
                    rb.EditItemRequest(item, info => info.getItemDef = () => new() { Name = item, Pool = "Skill", PriceCap = 500, MajorItem = true });
                }

                void AddLoc(string name)
                {
                    rb.AddLocationByName(name);
                    rb.EditLocationRequest(name, info => info.getLocationDef = () => new() { Name = name, SceneName = "Town" });
                }

                AddLoc("Split_Vengeful_Spirit");
                AddLoc("Split_Shade_Soul");
                AddLoc("GPZ_10");
                AddLoc("White_Defender_5");
                foreach (string hs in hotSprings) AddLoc(hs);
                foreach (string p in pantheons) AddLoc(p);
                rb.AddItemByName("Hot_Spring_Water", 6);
                rb.AddItemByName("Pantheon_Access-Master");
            }

            RCData.RuntimeLogicOverride.Subscribe(1050f, InjectBatch4Logic);
            RequestBuilder.OnUpdate.Subscribe(0f, InjectBatch4Requests);
            try
            {
                NightmareAccess.Settings.CopyFrom(new NightmareAccessSettings
                {
                    Enabled = true,
                    Scope = scope,
                    RequirementChance = 100
                });

                var gs = new GenerationSettings { Seed = seed };
                gs.Clamp();

                var rc = new RandoController(gs, new SettingsPM(gs), new RandomizerCore.RandoMonitor());
                rc.Run();

                CostApplier.PrepareAndVerify(rc.ctx, NightmareAccess.Settings, rc.gs, placements: null);
                WorldVerificationReport report = NightmareLogicHandler.LastVerificationReport;

                Assert.NotNull(report);
                Assert.True(report.IsCompletable, report.FormatSummary());
                Assert.Equal(report.BaselineLocationsCount, report.VerifiedLocationsCount);
                Assert.Equal(report.BaselineWaypointsCount, report.VerifiedWaypointsCount);
                Assert.Equal(report.BaselineTransitionsCount, report.VerifiedTransitionsCount);
                Assert.Empty(report.ConsistencyIssues);

                if (scope == RequirementScope.PerLocation)
                {
                    Assert.Contains("Split_Vengeful_Spirit", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("White_Defender_5", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("Crossroads-Hot_Spring", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("Pantheon-Master", NightmareLogicHandler.LocationConditions.Keys);
                }
            }
            finally
            {
                RCData.RuntimeLogicOverride.Unsubscribe(1050f, InjectBatch4Logic);
                RequestBuilder.OnUpdate.Unsubscribe(0f, InjectBatch4Requests);
            }
        }

        [Theory]
        [InlineData(RequirementScope.PerLocation, 9501)]
        [InlineData(RequirementScope.PerObjectGroup, 9502)]
        [InlineData(RequirementScope.PerObject, 9503)]
        public void FullRandomizerSeedGeneration_WithSimulatedWaveC_RopeAndCorpse_IsVerifiedCompletable(
            RequirementScope scope, int seed)
        {
            RopeRandoInterop.EnabledOverrideForTests = () => true;
            CorpseDreamRandoInterop.EnabledOverrideForTests = () => true;

            void InjectWaveCLogic(GenerationSettings _, LogicManagerBuilder lmb)
            {
                lmb.AddItem(new EmptyItem("Chandelier-Watcher_Knights"));
                lmb.AddLogicDef(new RawLogicDef("Chandelier-Watcher_Knights", "Ruins2_03[bot1] + CLAW"));

                lmb.AddItem(new EmptyItem("Corpse_Dream-Lighthouse"));
                lmb.AddLogicDef(new RawLogicDef("Corpse_Dream-Lighthouse", "Abyss_Lighthouse_room[left1] + DREAMNAIL"));
            }

            void InjectWaveCRequests(RequestBuilder rb)
            {
                rb.AddItemByName("Chandelier-Watcher_Knights");
                rb.AddLocationByName("Chandelier-Watcher_Knights");
                rb.EditLocationRequest("Chandelier-Watcher_Knights", info => info.getLocationDef = () => new()
                {
                    Name = "Chandelier-Watcher_Knights",
                    SceneName = "Ruins2_03"
                });

                rb.AddItemByName("Corpse_Dream-Lighthouse");
                rb.AddLocationByName("Corpse_Dream-Lighthouse");
                rb.EditLocationRequest("Corpse_Dream-Lighthouse", info => info.getLocationDef = () => new()
                {
                    Name = "Corpse_Dream-Lighthouse",
                    SceneName = "Abyss_Lighthouse_room"
                });
            }

            RCData.RuntimeLogicOverride.Subscribe(100f, InjectWaveCLogic);
            RequestBuilder.OnUpdate.Subscribe(0f, InjectWaveCRequests);
            try
            {
                NightmareAccess.Settings.CopyFrom(new NightmareAccessSettings
                {
                    Enabled = true,
                    Scope = scope,
                    RequirementChance = 100
                });

                var gs = new GenerationSettings { Seed = seed };
                gs.Clamp();

                var rc = new RandoController(gs, new SettingsPM(gs), new RandomizerCore.RandoMonitor());
                rc.Run();

                CostApplier.PrepareAndVerify(rc.ctx, NightmareAccess.Settings, rc.gs, placements: null);
                WorldVerificationReport report = NightmareLogicHandler.LastVerificationReport;

                Assert.NotNull(report);
                Assert.True(report.IsCompletable, report.FormatSummary());
                Assert.Equal(report.BaselineLocationsCount, report.VerifiedLocationsCount);
                Assert.Equal(report.BaselineWaypointsCount, report.VerifiedWaypointsCount);
                Assert.Equal(report.BaselineTransitionsCount, report.VerifiedTransitionsCount);
                Assert.Empty(report.ConsistencyIssues);

                if (scope == RequirementScope.PerLocation)
                {
                    Assert.Contains("Chandelier-Watcher_Knights", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("Corpse_Dream-Lighthouse", NightmareLogicHandler.LocationConditions.Keys);
                }
            }
            finally
            {
                RCData.RuntimeLogicOverride.Unsubscribe(100f, InjectWaveCLogic);
                RequestBuilder.OnUpdate.Unsubscribe(0f, InjectWaveCRequests);
            }
        }

        [Theory]
        [InlineData(RequirementScope.PerLocation, 9601)]
        [InlineData(RequirementScope.PerObjectGroup, 9602)]
        [InlineData(RequirementScope.PerObject, 9603)]
        public void FullRandomizerSeedGeneration_WithSimulatedWaveC_GhostsAndBreakableWalls_IsVerifiedCompletable(
            RequirementScope scope, int seed)
        {
            RandoPlusInterop.PoolOverrideForTests = k => k == "GhostEssence";
            BreakableWallRandomizerInterop.WallSettingOverrideForTests = k => k is "Enabled"
                or "RockWalls"
                or "WoodenPlanks"
                or "DiveFloors"
                or "Collapsers"
                or "GroupWalls";

            void InjectWaveC2Logic(GenerationSettings _, LogicManagerBuilder lmb)
            {
                lmb.AddItem(new SingleItem("Ghost_Essence", new TermValue(lmb.GetTerm("ESSENCE"), 1)));
                lmb.AddLogicDef(new RawLogicDef("Ghost_Essence-Gravedigger", "Town + DREAMNAIL"));
                lmb.AddLogicDef(new RawLogicDef("Ghost_Essence-Kcin", "RestingGrounds_08[left1] + DREAMNAIL"));

                lmb.GetOrAddTerm("Total_Broken_Walls", TermType.Int);
                lmb.GetOrAddTerm("Broken_Walls");
                lmb.GetOrAddTerm("Broken_Planks");
                lmb.GetOrAddTerm("Broken_Dive_Floors");
                lmb.GetOrAddTerm("Broken_Collapsers");

                lmb.AddItem(new RandomizerCore.StringItems.StringItemTemplate("Wall-Mawlek", "Total_Broken_Walls++ >> Broken_Walls++"));
                lmb.AddLogicDef(new RawLogicDef("Wall-Mawlek", "Crossroads_09[left1]"));

                lmb.AddItem(new RandomizerCore.StringItems.StringItemTemplate("Wall_Group-Peak_Walls", "Total_Broken_Walls++ >> Broken_Walls++"));
                lmb.AddLogicDef(new RawLogicDef("Wall_Group-Peak_Walls", "Mines_05[left1]"));

                lmb.AddItem(new RandomizerCore.StringItems.StringItemTemplate("Plank-Peak_Toll", "Total_Broken_Walls++ >> Broken_Planks++"));
                lmb.AddLogicDef(new RawLogicDef("Plank-Peak_Toll", "Mines_33[left1]"));

                lmb.AddItem(new RandomizerCore.StringItems.StringItemTemplate("Dive_Floor-Basin_Grub", "Total_Broken_Walls++ >> Broken_Dive_Floors++"));
                lmb.AddLogicDef(new RawLogicDef("Dive_Floor-Basin_Grub", "Abyss_04[top1] + QUAKE"));

                lmb.AddItem(new RandomizerCore.StringItems.StringItemTemplate("Collapser-Deepnest_Entrance", "Total_Broken_Walls++ >> Broken_Collapsers++"));
                lmb.AddLogicDef(new RawLogicDef("Collapser-Deepnest_Entrance", "Fungus2_21[right1]"));

                lmb.AddItem(new RandomizerCore.StringItems.StringItemTemplate("Plank-King's_Pass", "Total_Broken_Walls++ >> Broken_Planks++"));
                lmb.AddLogicDef(new RawLogicDef("Plank-King's_Pass", "Tutorial_01"));
                lmb.AddLogicDef(new RawLogicDef("Myla_Shop", "Crossroads_45[left1]"));
            }

            void InjectWaveC2Requests(RequestBuilder rb)
            {
                void AddDef(string name, string pool, string scene)
                {
                    rb.AddItemByName(name);
                    rb.EditItemRequest(name, info => info.getItemDef = () => new() { Name = name, Pool = pool, PriceCap = 500 });
                    rb.AddLocationByName(name);
                    rb.EditLocationRequest(name, info => info.getLocationDef = () => new() { Name = name, SceneName = scene });
                }

                rb.AddItemByName("Ghost_Essence", 2);
                rb.EditItemRequest("Ghost_Essence", info => info.getItemDef = () => new() { Name = "Ghost_Essence", Pool = "Ghost Essence", PriceCap = 10 });
                foreach (var (loc, scene) in new[] { ("Ghost_Essence-Gravedigger", "Town"), ("Ghost_Essence-Kcin", "RestingGrounds_08") })
                {
                    rb.AddLocationByName(loc);
                    rb.EditLocationRequest(loc, info => info.getLocationDef = () => new() { Name = loc, SceneName = scene });
                }

                AddDef("Wall-Mawlek", "Walls", "Crossroads_09");
                AddDef("Wall_Group-Peak_Walls", "Wall Groups", "Mines_05");
                AddDef("Plank-Peak_Toll", "Planks", "Mines_33");
                AddDef("Dive_Floor-Basin_Grub", "Dive Floors", "Abyss_04");
                AddDef("Collapser-Deepnest_Entrance", "Collapsers", "Deepnest_01");
                AddDef("Plank-King's_Pass", "Planks", "Tutorial_01");
            }

            RCData.RuntimeLogicOverride.Subscribe(50f, InjectWaveC2Logic);
            RequestBuilder.OnUpdate.Subscribe(1f, InjectWaveC2Requests);
            try
            {
                NightmareAccess.Settings.CopyFrom(new NightmareAccessSettings
                {
                    Enabled = true,
                    Scope = scope,
                    RequirementChance = 100
                });

                var gs = new GenerationSettings { Seed = seed };
                gs.Clamp();

                var rc = new RandoController(gs, new SettingsPM(gs), new RandomizerCore.RandoMonitor());
                rc.Run();

                CostApplier.PrepareAndVerify(rc.ctx, NightmareAccess.Settings, rc.gs, placements: null);
                WorldVerificationReport report = NightmareLogicHandler.LastVerificationReport;

                Assert.NotNull(report);
                Assert.True(report.IsCompletable, report.FormatSummary());
                Assert.Equal(report.BaselineLocationsCount, report.VerifiedLocationsCount);
                Assert.Equal(report.BaselineWaypointsCount, report.VerifiedWaypointsCount);
                Assert.Equal(report.BaselineTransitionsCount, report.VerifiedTransitionsCount);
                Assert.Empty(report.ConsistencyIssues);

                Assert.False(NightmareLogicHandler.LocationConditions.ContainsKey("Myla_Shop"));
                Assert.False(NightmareLogicHandler.LocationConditions.ContainsKey("Plank-King's_Pass"));

                if (scope == RequirementScope.PerLocation)
                {
                    Assert.Contains("Ghost_Essence-Gravedigger", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("Ghost_Essence-Kcin", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("Wall-Mawlek", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("Wall_Group-Peak_Walls", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("Plank-Peak_Toll", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("Dive_Floor-Basin_Grub", NightmareLogicHandler.LocationConditions.Keys);
                    Assert.Contains("Collapser-Deepnest_Entrance", NightmareLogicHandler.LocationConditions.Keys);
                }
            }
            finally
            {
                RCData.RuntimeLogicOverride.Unsubscribe(50f, InjectWaveC2Logic);
                RequestBuilder.OnUpdate.Unsubscribe(1f, InjectWaveC2Requests);
            }
        }

        [Theory]
        [InlineData(RequirementScope.PerLocation, 9701)]
        [InlineData(RequirementScope.PerObjectGroup, 9702)]
        [InlineData(RequirementScope.PerObject, 9703)]
        public void FullRandomizerSeedGeneration_WithSimulatedFlowerRandomizer_IsVerifiedCompletable(
            RequirementScope scope, int seed)
        {
            FlowerRandomizerInterop.EnabledOverrideForTests = () => true;
            FlowerRandomizerInterop.QuestEnabledOverrideForTests = _ => true;

            (string name, string logic)[] flowerLocations =
            {
                ("Flower_Quest-Elderbug", "Town + LISTEN?TRUE + NOFLOWER=FALSE"),
                ("Flower_Quest-Emilitia", "(Ruins_House_03[left1] | (ANYCLAW + Ruins_House_03[left2])) + LISTEN?TRUE + NOFLOWER=FALSE"),
                ("Flower_Quest-Godseeker", "GG_Waterways + GODTUNERUNLOCK + LISTEN?TRUE + Defeated_Pantheon_1 + Defeated_Pantheon_2 + Defeated_Pantheon_3 + NOFLOWER=FALSE"),
                ("Flower_Quest-Isma", "Waterways_13 + DREAMNAIL + NOFLOWER=FALSE"),
                ("Flower_Quest-Marissa", "(Ruins_Bathhouse[door1] | Ruins_Bathhouse[right1] + Opened_Pleasure_House_Wall) + DREAMNAIL + NOFLOWER=FALSE"),
                ("Flower_Quest-Mato", "Room_nailmaster[left1] + LISTEN?TRUE + NOFLOWER=FALSE"),
                ("Flower_Quest-Midwife", "Deepnest_41[left2] + LISTEN?TRUE + Wall-Midwife?TRUE + (LANTERN | NOLANTERN?FALSE | DARKROOMS) + NOFLOWER=FALSE"),
                ("Flower_Quest-Oro", "Room_nailmaster_03[left1] + Can_Visit_Lemm + LISTEN?TRUE + NOFLOWER=FALSE"),
                ("Flower_Quest-Pain", "White_Palace_20[bot1] + Completed_Path_of_Pain + NOFLOWER=FALSE"),
                ("Flower_Quest-Pale_King", "*King_Fragment + NOFLOWER=FALSE"),
                ("Flower_Quest-Radiance", "*Pale_Ore-Crystal_Peak + DREAMNAIL + NOFLOWER=FALSE"),
                ("Flower_Quest-Sheo", "Room_nailmaster_02[left1] + LISTEN?TRUE + NOFLOWER=FALSE"),
                ("Flower_Quest-White_Lady", "Room_Queen[left1] + LISTEN?TRUE + NOFLOWER=FALSE")
            };

            void InjectFlowerLogic(GenerationSettings _, LogicManagerBuilder lmb)
            {
                lmb.AddWaypoint(new RawWaypointDef(
                    "Opened_Pantheon_1",
                    "GG_Atrium + ((Defeated_Gruz_Mother + Defeated_False_Knight + Defeated_Massive_Moss_Charger + Defeated_Hornet_1 + Defeated_Gorb + Defeated_Dung_Defender + (Defeated_Sanctum_Warrior | Defeated_Elegant_Warrior | Defeated_Colosseum_2 + (COLO_KEY_3 ? TRUE)) + Defeated_Brooding_Mawlek) | (PANTHEON_KEY_1 ? FALSE))",
                    stateless: true));
                lmb.AddWaypoint(new RawWaypointDef(
                    "Defeated_Pantheon_1",
                    "GG_Atrium + Opened_Pantheon_1 + (FOCUS | SPICYCOMBATSKIPS)",
                    stateless: true));
                lmb.AddWaypoint(new RawWaypointDef(
                    "Opened_Pantheon_2",
                    "GG_Atrium + ((Defeated_Xero + Defeated_Crystal_Guardian + Defeated_Soul_Master + Defeated_Colosseum_2 + Defeated_Mantis_Lords + Defeated_Marmu + Defeated_Nosk + Defeated_Flukemarm + Defeated_Broken_Vessel) | (PANTHEON_KEY_2 ? FALSE))",
                    stateless: true));
                lmb.AddWaypoint(new RawWaypointDef(
                    "Defeated_Pantheon_2",
                    "GG_Atrium + Opened_Pantheon_2 + (FOCUS | SPICYCOMBATSKIPS)",
                    stateless: true));
                lmb.AddWaypoint(new RawWaypointDef(
                    "Opened_Pantheon_3",
                    "GG_Atrium + ((Defeated_Hive_Knight + Defeated_Elder_Hu + Defeated_Collector + Defeated_Colosseum_2 + Defeated_Grimm + Defeated_Galien + Defeated_Uumuu + Defeated_Hornet_2) | (PANTHEON_KEY_3 ? FALSE))",
                    stateless: true));
                lmb.AddWaypoint(new RawWaypointDef(
                    "Defeated_Pantheon_3",
                    "GG_Atrium + Opened_Pantheon_3 + (FOCUS | SPICYCOMBATSKIPS)",
                    stateless: true));
                lmb.AddWaypoint(new RawWaypointDef(
                    "Defeated_Massive_Moss_Charger",
                    "*Boss_Geo-Massive_Moss_Charger",
                    stateless: true));

                foreach (var (name, logic) in flowerLocations)
                {
                    lmb.AddLogicDef(new RawLogicDef(name, logic));
                }
            }

            void InjectFlowerRequests(RequestBuilder rb)
            {
                foreach (var (name, _) in flowerLocations)
                {
                    rb.AddLocationByName(name);
                    rb.EditLocationRequest(name, info => info.getLocationDef = () => new()
                    {
                        Name = name,
                        SceneName = "Town"
                    });
                }
            }

            RCData.RuntimeLogicOverride.Subscribe(0f, InjectFlowerLogic);
            RequestBuilder.OnUpdate.Subscribe(0.4f, InjectFlowerRequests);
            try
            {
                NightmareAccess.Settings.CopyFrom(new NightmareAccessSettings
                {
                    Enabled = true,
                    Scope = scope,
                    RequirementChance = 100
                });

                var gs = new GenerationSettings { Seed = seed };
                gs.Clamp();

                var rc = new RandoController(gs, new SettingsPM(gs), new RandomizerCore.RandoMonitor());
                rc.Run();

                CostApplier.PrepareAndVerify(rc.ctx, NightmareAccess.Settings, rc.gs, placements: null);
                WorldVerificationReport report = NightmareLogicHandler.LastVerificationReport;

                Assert.NotNull(report);
                Assert.True(report.IsCompletable, report.FormatSummary());
                Assert.Equal(report.BaselineLocationsCount, report.VerifiedLocationsCount);
                Assert.Equal(report.BaselineWaypointsCount, report.VerifiedWaypointsCount);
                Assert.Equal(report.BaselineTransitionsCount, report.VerifiedTransitionsCount);
                Assert.Empty(report.ConsistencyIssues);

                if (scope == RequirementScope.PerLocation)
                {
                    foreach (var (name, _) in flowerLocations)
                    {
                        Assert.Contains(name, NightmareLogicHandler.LocationConditions.Keys);
                    }
                }
            }
            finally
            {
                RCData.RuntimeLogicOverride.Unsubscribe(0f, InjectFlowerLogic);
                RequestBuilder.OnUpdate.Unsubscribe(0.4f, InjectFlowerRequests);
            }
        }

        [Theory]
        [InlineData(RequirementScope.PerLocation, 9901)]
        [InlineData(RequirementScope.PerObjectGroup, 9902)]
        [InlineData(RequirementScope.PerObject, 9903)]
        public void FullRandomizerSeedGeneration_WithSimulatedGodhomeRandomizer_IsVerifiedCompletable(
            RequirementScope scope, int seed)
        {
            GodhomeRandomizerInterop.ConfigOverrideForTests = () => new GodhomeRandomizerInterop.GodhomeConfig
            {
                Enabled = true,
                RandomizeTiers = 3,
                IncludeUnlockLocations = true,
                RandomizeOrdeal = true,
                PantheonsIncluded = 2,
                Lifeblood = true,
                Completion = true,
                Nail = true
            };

            string[] godhomeLocs =
            {
                "Empty_Mark-Gruz_Mother",
                "Bronze_Mark-Gruz_Mother",
                "Silver_Mark-Gruz_Mother",
                "Gold_Mark-Gruz_Mother",
                "Eternal_Ordeal",
                "Godhome_Lifeblood",
                "Pantheon_Master-Completion",
                "Pantheon_Master-Nail_Binding",
                "Pantheon_Artist-Completion",
                "Pantheon_Artist-Nail_Binding"
            };

            void InjectGodhomeLogic(GenerationSettings _, LogicManagerBuilder lmb)
            {
                lmb.AddTransition(new RawLogicDef("GG_Workshop[left1]", "GG_Atrium"));
                lmb.AddWaypoint(new RawWaypointDef("GG_Blue_Room", "GG_Atrium", stateless: false));

                lmb.GetOrAddTerm("GG_Gruz_Mother", TermType.Int);
                lmb.AddItem(new RandomizerCore.StringItems.StringItemTemplate("Statue_Mark-Gruz_Mother", "GG_Gruz_Mother++"));
                lmb.AddLogicDef(new RawLogicDef("Empty_Mark-Gruz_Mother", "GG_Workshop[left1] + GG_Gruz_Mother>0"));
                lmb.AddLogicDef(new RawLogicDef("Bronze_Mark-Gruz_Mother", "GG_Workshop[left1] + GG_Gruz_Mother>0"));
                lmb.AddLogicDef(new RawLogicDef("Silver_Mark-Gruz_Mother", "GG_Workshop[left1] + GG_Gruz_Mother>0"));
                lmb.AddLogicDef(new RawLogicDef("Gold_Mark-Gruz_Mother", "GG_Workshop[left1] + GG_Gruz_Mother>2"));

                lmb.AddItem(new EmptyItem("Eternal_Ordeal"));
                lmb.AddLogicDef(new RawLogicDef("Eternal_Ordeal", "GG_Workshop"));

                lmb.AddItem(new EmptyItem("Godhome_Lifeblood"));
                lmb.AddLogicDef(new RawLogicDef("Godhome_Lifeblood", "GG_Blue_Room + DREAMNAIL"));

                lmb.AddItem(new EmptyItem("Pantheon_Master-Completion"));
                lmb.AddItem(new EmptyItem("Pantheon_Master-Nail_Binding"));
                lmb.AddItem(new EmptyItem("Pantheon_Artist-Completion"));
                lmb.AddItem(new EmptyItem("Pantheon_Artist-Nail_Binding"));
                lmb.AddLogicDef(new RawLogicDef("Pantheon_Master-Completion", "GG_Atrium"));
                lmb.AddLogicDef(new RawLogicDef("Pantheon_Master-Nail_Binding", "GG_Atrium"));
                lmb.AddLogicDef(new RawLogicDef("Pantheon_Artist-Completion", "GG_Atrium"));
                lmb.AddLogicDef(new RawLogicDef("Pantheon_Artist-Nail_Binding", "GG_Atrium"));
                lmb.AddLogicDef(new RawLogicDef("Godhome_Shop", "GG_Workshop"));
            }

            void InjectGodhomeRequests(RequestBuilder rb)
            {
                rb.AddItemByName("Statue_Mark-Gruz_Mother", 4);
                rb.AddItemByName("Eternal_Ordeal");
                rb.AddItemByName("Godhome_Lifeblood");
                rb.AddItemByName("Pantheon_Master-Completion");
                rb.AddItemByName("Pantheon_Master-Nail_Binding");
                rb.AddItemByName("Pantheon_Artist-Completion");
                rb.AddItemByName("Pantheon_Artist-Nail_Binding");

                foreach (string loc in godhomeLocs)
                {
                    rb.AddLocationByName(loc);
                    rb.EditLocationRequest(loc, info => info.getLocationDef = () => new() { Name = loc, SceneName = "GG_Workshop" });
                }
            }

            RCData.RuntimeLogicOverride.Subscribe(2048f, InjectGodhomeLogic);
            RequestBuilder.OnUpdate.Subscribe(10f, InjectGodhomeRequests);
            try
            {
                NightmareAccess.Settings.CopyFrom(new NightmareAccessSettings
                {
                    Enabled = true,
                    Scope = scope,
                    RequirementChance = 100
                });

                var gs = new GenerationSettings { Seed = seed };
                gs.Clamp();

                var rc = new RandoController(gs, new SettingsPM(gs), new RandomizerCore.RandoMonitor());
                rc.Run();

                CostApplier.PrepareAndVerify(rc.ctx, NightmareAccess.Settings, rc.gs, placements: null);
                WorldVerificationReport report = NightmareLogicHandler.LastVerificationReport;

                Assert.NotNull(report);
                Assert.True(report.IsCompletable, report.FormatSummary());
                Assert.Equal(report.BaselineLocationsCount, report.VerifiedLocationsCount);
                Assert.Equal(report.BaselineWaypointsCount, report.VerifiedWaypointsCount);
                Assert.Equal(report.BaselineTransitionsCount, report.VerifiedTransitionsCount);
                Assert.Empty(report.ConsistencyIssues);

                Assert.False(NightmareLogicHandler.LocationConditions.ContainsKey("Godhome_Shop"));
                if (scope == RequirementScope.PerLocation)
                {
                    foreach (string loc in godhomeLocs)
                    {
                        Assert.Contains(loc, NightmareLogicHandler.LocationConditions.Keys);
                    }
                }
            }
            finally
            {
                RCData.RuntimeLogicOverride.Unsubscribe(2048f, InjectGodhomeLogic);
                RequestBuilder.OnUpdate.Unsubscribe(10f, InjectGodhomeRequests);
            }
        }

        [Theory]
        [InlineData(RequirementScope.PerLocation, 9951)]
        [InlineData(RequirementScope.PerObjectGroup, 9952)]
        [InlineData(RequirementScope.PerObject, 9953)]
        public void FullRandomizerSeedGeneration_WithSimulatedLoreRandomizer_IsVerifiedCompletable(
            RequirementScope scope, int seed)
        {
            LoreRandomizerInterop.ConfigOverrideForTests = () => new LoreRandomizerInterop.LoreConfig
            {
                Enabled = true,
                RandomizeNpc = true,
                RandomizeDreamNailDialogue = true,
                RandomizePointOfInterest = true,
                RandomizeTravellerDialogues = true,
                RandomizeShrineOfBelievers = true
            };

            string[] loreLocs =
            {
                "Mask_Maker",
                "Isma_Dream",
                "Gorb_Grave",
                "Stag_Nest",
                "Quirrel_Crossroads",
                "Geo-Shrine"
            };

            void InjectLoreLogic(GenerationSettings _, LogicManagerBuilder lmb)
            {
                lmb.GetOrAddTerm("LORE", TermType.Int);
                lmb.AddItem(new SingleItem("Dialogue-Mask_Maker", new TermValue(lmb.GetTerm("LORE"), 1)));
                lmb.AddItem(new SingleItem("Dream_Dialogue-Isma", new TermValue(lmb.GetTerm("LORE"), 1)));
                lmb.AddItem(new SingleItem("Inspect-Gorb", new TermValue(lmb.GetTerm("LORE"), 1)));
                lmb.AddItem(new SingleItem("Inspect-Stag_Egg", new TermValue(lmb.GetTerm("LORE"), 1)));
                lmb.AddItem(new SingleItem("Dialogue-Quirrel_Crossroads", new TermValue(lmb.GetTerm("LORE"), 1)));
                lmb.AddItem(new EmptyItem("Precept_1"));

                lmb.AddLogicDef(new RawLogicDef("Mask_Maker", "Fungus2_23"));
                lmb.AddLogicDef(new RawLogicDef("Isma_Dream", "Waterways_13 + DREAMNAIL"));
                lmb.AddLogicDef(new RawLogicDef("Gorb_Grave", "Cliffs_02"));
                lmb.AddLogicDef(new RawLogicDef("Stag_Nest", "Cliffs_03[right1]"));
                lmb.AddLogicDef(new RawLogicDef("Quirrel_Crossroads", "Room_temple[left1]"));
                lmb.AddLogicDef(new RawLogicDef("Geo-Shrine", "Town"));
                lmb.AddLogicDef(new RawLogicDef("Elderbug_Shop", "Town"));
            }

            void InjectLoreRequests(RequestBuilder rb)
            {
                rb.AddItemByName("Dialogue-Mask_Maker");
                rb.AddItemByName("Dream_Dialogue-Isma");
                rb.AddItemByName("Inspect-Gorb");
                rb.AddItemByName("Inspect-Stag_Egg");
                rb.AddItemByName("Dialogue-Quirrel_Crossroads");
                rb.AddItemByName("Precept_1");

                foreach (string loc in loreLocs)
                {
                    rb.AddLocationByName(loc);
                    rb.EditLocationRequest(loc, info => info.getLocationDef = () => new() { Name = loc, SceneName = "Town" });
                }
            }

            RCData.RuntimeLogicOverride.Subscribe(9999f, InjectLoreLogic);
            RequestBuilder.OnUpdate.Subscribe(30f, InjectLoreRequests);
            try
            {
                NightmareAccess.Settings.CopyFrom(new NightmareAccessSettings
                {
                    Enabled = true,
                    Scope = scope,
                    RequirementChance = 100
                });

                var gs = new GenerationSettings { Seed = seed };
                gs.Clamp();

                var rc = new RandoController(gs, new SettingsPM(gs), new RandomizerCore.RandoMonitor());
                rc.Run();

                CostApplier.PrepareAndVerify(rc.ctx, NightmareAccess.Settings, rc.gs, placements: null);
                WorldVerificationReport report = NightmareLogicHandler.LastVerificationReport;

                Assert.NotNull(report);
                Assert.True(report.IsCompletable, report.FormatSummary());
                Assert.Equal(report.BaselineLocationsCount, report.VerifiedLocationsCount);
                Assert.Equal(report.BaselineWaypointsCount, report.VerifiedWaypointsCount);
                Assert.Equal(report.BaselineTransitionsCount, report.VerifiedTransitionsCount);
                Assert.Empty(report.ConsistencyIssues);

                Assert.False(NightmareLogicHandler.LocationConditions.ContainsKey("Elderbug_Shop"));
                if (scope == RequirementScope.PerLocation)
                {
                    foreach (string loc in loreLocs)
                    {
                        Assert.Contains(loc, NightmareLogicHandler.LocationConditions.Keys);
                    }
                }
            }
            finally
            {
                RCData.RuntimeLogicOverride.Unsubscribe(9999f, InjectLoreLogic);
                RequestBuilder.OnUpdate.Unsubscribe(30f, InjectLoreRequests);
            }
        }
    }
}
