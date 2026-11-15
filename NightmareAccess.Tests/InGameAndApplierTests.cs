using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using ItemChanger;
using ItemChanger.Items;
using ItemChanger.Locations;
using ItemChanger.Placements;
using ItemChanger.Tags;
using ItemChanger.UIDefs;
using NightmareAccess.InGame;
using NightmareAccess.Integrations;
using NightmareAccess.Integrations.FlowerRandomizer;
using NightmareAccess.Integrations.YARC;
using NightmareAccess.Interop;
using NightmareAccess.Logic;
using NightmareAccess.Settings;
using RandomizerCore;
using RandomizerCore.Logic;
using RandomizerCore.LogicItems;
using RandomizerMod.RC;
using RandomizerMod.Settings;
using Xunit;
using Random = System.Random;

namespace NightmareAccess.Tests
{
    public sealed class InGameAndApplierTests : IDisposable
    {
        private readonly HashSet<string> _obtained = new(StringComparer.Ordinal);

        public InGameAndApplierTests()
        {
            ResetAll();
            ObtainedItems.OverrideForTests = name => _obtained.Contains(name);
        }

        public void Dispose()
        {
            ResetAll();
            ObtainedItems.OverrideForTests = null;
        }

        private static void ResetAll()
        {
            NightmareAccess.ApplySaveData(null);
            NightmareLogicHandler.LastVerificationReport = null;
            ColosseumPrompt.PlacementsOverrideForTests = null;
            CornerPromptHelper.PlacementsOverrideForTests = null;
            CornerPromptHelper.SendCornerMessageOverrideForTests = null;
            CornerPromptHelper.PlayerDataOverrideForTests = null;
            YarcInterop.DreamOrbsOverrideForTests = null;
            YarcInterop.PoolOverrideForTests = null;
            FlowerRandomizerInterop.EnabledOverrideForTests = null;
            FlowerRandomizerInterop.QuestEnabledOverrideForTests = null;
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

        private sealed class TestItem : AbstractItem
        {
            public bool ObtainedState;
            public TestItem(string itemName, string uiDisplayName = null)
            {
                name = itemName;
                if (uiDisplayName != null)
                {
                    UIDef = new MsgUIDef { name = new BoxedString(uiDisplayName) };
                }
            }

            public override void GiveImmediate(GiveInfo info) => ObtainedState = true;
            public override bool Redundant() => ObtainedState;
        }

        private sealed class TestMultiCostPlacement : AbstractPlacement, IMultiCostPlacement
        {
            public TestMultiCostPlacement(string name) : base(name) { }
            protected override void OnLoad() { }
            protected override void OnUnload() { }
        }

        private sealed class TestSingleCostPlacement : AbstractPlacement, ISingleCostPlacement
        {
            public Cost Cost { get; set; }
            public TestSingleCostPlacement(string name) : base(name) { }
            protected override void OnLoad() { }
            protected override void OnUnload() { }
        }

        private sealed class TestAutoLocation : AutoLocation
        {
            private readonly bool _supportsCost;
            public TestAutoLocation(string locName, bool supportsCost)
            {
                name = locName;
                _supportsCost = supportsCost;
            }
            public override bool SupportsCost => _supportsCost;
            protected override void OnLoad() { }
            protected override void OnUnload() { }
        }

        // ------------------------------------------------------------------
        // Save Data & Language Rewrite
        // ------------------------------------------------------------------

        [Fact]
        public void NightmareAccess_SaveDataLifecycle_InfersLegacyScopesAndResetsOnNull()
        {
            var legacyObjectSave = new NightmareAccessSaveData
            {
                ObjectConditions = { ["Mantis_Claw"] = new AccessCondition(AccessConditionType.Grubs, 2) }
            };
            NightmareAccess.ApplySaveData(legacyObjectSave);
            Assert.True(NightmareLogicHandler.IsSaveActive);
            Assert.Equal(RequirementScope.PerObject, NightmareLogicHandler.ActiveScope);
            Assert.Equal("Not enough.", NightmareAccess.RewriteGeoPrompt("NOT_ENOUGH_GEO", "Anything"));
            Assert.Equal("Not enough.", NightmareAccess.RewriteGeoPrompt("OTHER", "Not enough Geo."));
            Assert.Equal("Hello", NightmareAccess.RewriteGeoPrompt("OTHER", "Hello"));

            var legacyGroupSave = new NightmareAccessSaveData
            {
                ItemTypeConditions = { ["Geo"] = new AccessCondition(AccessConditionType.Geo, 50) }
            };
            NightmareAccess.ApplySaveData(legacyGroupSave);
            Assert.True(NightmareLogicHandler.IsSaveActive);
            Assert.Equal(RequirementScope.PerObjectGroup, NightmareLogicHandler.ActiveScope);

            var explicitSave = new NightmareAccessSaveData
            {
                Enabled = true,
                Scope = RequirementScope.PerObject,
                LocationConditions = { ["L1"] = new AccessCondition(AccessConditionType.Dash) }
            };
            NightmareAccess.ApplySaveData(explicitSave);
            NightmareAccessSaveData exported = NightmareAccess.ExportSaveData();
            Assert.True(exported.Enabled);
            Assert.Equal(RequirementScope.PerObject, exported.Scope);
            Assert.Single(exported.LocationConditions);

            NightmareAccess.ApplySaveData(null);
            Assert.False(NightmareLogicHandler.IsSaveActive);
            Assert.Equal(RequirementScope.PerLocation, NightmareLogicHandler.ActiveScope);
            Assert.Empty(NightmareLogicHandler.LocationConditions);
            Assert.Empty(NightmareLogicHandler.ObjectConditions);
            Assert.Empty(NightmareLogicHandler.ItemTypeConditions);
            Assert.Equal("Not enough Geo.", NightmareAccess.RewriteGeoPrompt("NOT_ENOUGH_GEO", "Not enough Geo."));
        }

        // ------------------------------------------------------------------
        // CostApplier End-to-End Across All Scopes & Placement Types
        // ------------------------------------------------------------------

        [Theory]
        [InlineData(RequirementScope.PerLocation)]
        [InlineData(RequirementScope.PerObjectGroup)]
        [InlineData(RequirementScope.PerObject)]
        public void CostApplier_ApplyToPlacements_HandlesAllScopesAndPlacementKinds(RequirementScope scope)
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
            settings.AllowGeo = true;
            settings.AllowGrubs = true;
            settings.MinGeo = 10;
            settings.MaxGeo = 100;
            var gs = new GenerationSettings { Seed = 777 };

            var lmb = new LogicManagerBuilder();
            lmb.GetOrAddTerm("GEO", TermType.SignedByte);
            lmb.GetOrAddTerm("Can_Replenish_Geo", TermType.SignedByte);
            lmb.GetOrAddTerm("GRUBS", TermType.SignedByte);
            lmb.AddWaypoint(new RawWaypointDef("Start_State", "TRUE", stateless: false));
            lmb.AddWaypoint(new RawWaypointDef("Can_Replenish_Geo", "TRUE", stateless: true));
            lmb.AddItem(new SingleItem("Grub", new TermValue(lmb.GetTerm("GRUBS"), 1)));
            lmb.AddItem(new EmptyItem("One_Geo"));
            lmb.AddItem(new EmptyItem("Lore_Tablet-City_Entrance"));

            string[] locs =
            {
                "Loc_Shiny", "Loc_Mutable", "Loc_Dual", "Loc_Existing", "Loc_Single",
                "Loc_AutoCost", "Loc_Multi", "Whispering_Root-Crossroads", "Sly"
            };
            foreach (string loc in locs)
            {
                lmb.AddLogicDef(new RawLogicDef(loc, "TRUE"));
            }

            var lm = new LogicManager(lmb);
            var ctx = new RandoModContext(lm) { GenerationSettings = gs };

            void AddCtx(string item, string loc) =>
                ctx.itemPlacements.Add(new ItemPlacement(
                    new RandoModItem { item = lm.GetItemStrict(item) },
                    new RandoModLocation { logic = lm.GetLogicDefStrict(loc) }));

            AddCtx("One_Geo", "Loc_Shiny");
            AddCtx("Grub", "Loc_Mutable");
            AddCtx("One_Geo", "Loc_Dual");
            AddCtx("Lore_Tablet-City_Entrance", "Loc_Existing");
            AddCtx("One_Geo", "Loc_Single");
            AddCtx("One_Geo", "Loc_AutoCost");
            AddCtx("One_Geo", "Loc_Multi");
            AddCtx("Grub", "Loc_Multi");
            AddCtx("One_Geo", "Whispering_Root-Crossroads");
            AddCtx("One_Geo", "Sly");

            var shiny = new YNShinyPlacement("Loc_Shiny") { Location = new CoordinateLocation { name = "Loc_Shiny" } };
            shiny.Add(new TestItem("One_Geo"));

            var mutable = new MutablePlacement("Loc_Mutable") { Location = new CoordinateLocation { name = "Loc_Mutable" } };
            mutable.Add(new TestItem("Grub"));

            var dual = new DualPlacement("Loc_Dual") { falseLocation = new EmptyLocation(), trueLocation = new EmptyLocation() };
            dual.Add(new TestItem("One_Geo"));

            var existing = new ExistingContainerPlacement("Loc_Existing")
            {
                Location = new ExistingFsmContainerLocation { name = "Loc_Existing", containerType = "Tablet", nonreplaceable = false }
            };
            existing.Add(new TestItem("Lore_Tablet-City_Entrance"));

            var single = new TestSingleCostPlacement("Loc_Single");
            single.Add(new TestItem("One_Geo"));

            var autoSupported = new AutoPlacement("Loc_AutoCost") { Location = new TestAutoLocation("Loc_AutoCost", supportsCost: true) };
            autoSupported.Add(new TestItem("One_Geo"));

            var multi = new TestMultiCostPlacement("Loc_Multi");
            var multiGeo = new TestItem("One_Geo");
            var multiGrub = new TestItem("Grub");
            multi.Add(multiGeo);
            multi.Add(multiGrub);

            var root = new AutoPlacement("Whispering_Root-Crossroads") { Location = new TestAutoLocation("Whispering_Root-Crossroads", supportsCost: false) };
            root.Add(new TestItem("One_Geo"));

            var shop = new TestMultiCostPlacement("Sly");
            shop.Add(new TestItem("One_Geo"));

            var placements = new Dictionary<string, AbstractPlacement>(StringComparer.Ordinal)
            {
                ["Loc_Shiny"] = shiny,
                ["Loc_Mutable"] = mutable,
                ["Loc_Dual"] = dual,
                ["Loc_Existing"] = existing,
                ["Loc_Single"] = single,
                ["Loc_AutoCost"] = autoSupported,
                ["Loc_Multi"] = multi,
                ["Whispering_Root-Crossroads"] = root,
                ["Sly"] = shop
            };

            if (scope == RequirementScope.PerLocation)
            {
                NightmareLogicHandler.LocationConditions["Loc_Shiny"] = new AccessCondition(AccessConditionType.Geo, 100);
                NightmareLogicHandler.LocationConditions["Loc_Mutable"] = new AccessCondition(AccessConditionType.Geo, 50);
                NightmareLogicHandler.LocationConditions["Loc_Dual"] = new AccessCondition(AccessConditionType.Geo, 75);
                NightmareLogicHandler.LocationConditions["Loc_Existing"] = new AccessCondition(AccessConditionType.Geo, 60);
                NightmareLogicHandler.LocationConditions["Loc_Single"] = new AccessCondition(AccessConditionType.Geo, 40);
                NightmareLogicHandler.LocationConditions["Loc_AutoCost"] = new AccessCondition(AccessConditionType.Geo, 30);
                NightmareLogicHandler.LocationConditions["Loc_Multi"] = new AccessCondition(AccessConditionType.Geo, 20);
                NightmareLogicHandler.LocationConditions["Whispering_Root-Crossroads"] = new AccessCondition(AccessConditionType.Geo, 10);
                NightmareLogicHandler.LocationConditions["Sly"] = new AccessCondition(AccessConditionType.Geo, 999);
                NightmareLogicHandler.LocationConditions["Ghost_Location_NotInSave"] = new AccessCondition(AccessConditionType.Geo, 10);
            }

            NightmareLogicHandler.IsSaveActive = true;
            NightmareLogicHandler.ActiveScope = scope;

            CostApplier.ApplyToPlacements(placements, ctx, settings, gs);

            Assert.NotNull(NightmareLogicHandler.LastVerificationReport);
            Assert.True(NightmareLogicHandler.LastVerificationReport.IsCompletable);

            // Shop must always remain untouched & pruned from LocationConditions
            Assert.False(NightmareLogicHandler.LocationConditions.ContainsKey("Sly"));
            Assert.False(NightmareLogicHandler.LocationConditions.ContainsKey("Ghost_Location_NotInSave"));
            Assert.Null(shop.Items[0].GetTag<CostTag>());

            if (scope == RequirementScope.PerLocation)
            {
                Assert.NotNull(NightmareAccessCost.Find(shiny.Cost));
                var convertedMutable = Assert.IsType<YNShinyPlacement>(placements["Loc_Mutable"]);
                Assert.NotNull(NightmareAccessCost.Find(convertedMutable.Cost));
                Assert.NotNull(NightmareAccessCost.Find(dual.Cost));
                Assert.NotNull(NightmareAccessCost.Find(existing.Cost));
                Assert.NotNull(NightmareAccessCost.Find(single.Cost));
                Assert.NotNull(NightmareAccessCost.Find(autoSupported.Cost));
                Assert.NotNull(NightmareAccessCost.Find(multiGeo.GetTag<CostTag>()?.Cost));
                Assert.NotNull(NightmareAccessCost.Find(multiGrub.GetTag<CostTag>()?.Cost));
                Assert.NotNull(NightmareAccessCost.Find(root.GetTag<CostTag>()?.Cost));
            }
            else if (scope == RequirementScope.PerObjectGroup)
            {
                Assert.True(NightmareLogicHandler.ItemTypeConditions.ContainsKey("Geo"));
                Assert.True(NightmareLogicHandler.ItemTypeConditions.ContainsKey("Grubs"));
                Assert.True(NightmareLogicHandler.ItemTypeConditions.ContainsKey("Lore"));
            }
            else
            {
                // PerObject: multi placement gets individual CostTags per item; others go in ObjectConditions
                Assert.True(NightmareLogicHandler.ObjectConditions.ContainsKey("Loc_Shiny"));
                Assert.NotNull(NightmareAccessCost.Find(multiGeo.GetTag<CostTag>()?.Cost));
                Assert.NotNull(NightmareAccessCost.Find(multiGrub.GetTag<CostTag>()?.Cost));
            }
        }

        [Fact]
        public void CostValidator_DetectsConsistencyIssues_UnreachableWaypointsTransitions_AndRepairsMultiItem()
        {
            var gs = new GenerationSettings();
            var settings = new NightmareAccessSettings
            {
                Enabled = true,
                Scope = RequirementScope.PerObject,
                AllowGeo = true,
                MinGeo = 10,
                MaxGeo = 100
            };

            var lmb = new LogicManagerBuilder();
            lmb.GetOrAddTerm("LEFTCLAW", TermType.SignedByte);
            lmb.GetOrAddTerm("GRUBS", TermType.SignedByte);
            lmb.GetOrAddTerm("GEO", TermType.SignedByte);
            lmb.GetOrAddTerm("Can_Replenish_Geo", TermType.SignedByte);
            lmb.AddWaypoint(new RawWaypointDef("Start_State", "TRUE", stateless: false));
            lmb.AddWaypoint(new RawWaypointDef("Can_Replenish_Geo", "TRUE", stateless: true));
            lmb.AddWaypoint(new RawWaypointDef("WP_NeedsClaw", "LEFTCLAW", stateless: true));
            lmb.AddTransition(new RawLogicDef("Crossroads_01[right1]", "LEFTCLAW"));
            lmb.AddItem(new SingleItem("Mantis_Claw", new TermValue(lmb.GetTerm("LEFTCLAW"), 1)));
            lmb.AddItem(new SingleItem("Grub", new TermValue(lmb.GetTerm("GRUBS"), 1)));
            lmb.AddItem(new EmptyItem("One_Geo"));
            lmb.AddLogicDef(new RawLogicDef("Loc_Multi", "TRUE"));
            lmb.AddLogicDef(new RawLogicDef("Sly", "TRUE"));

            var lm = new LogicManager(lmb);
            var ctx = new RandoModContext(lm) { GenerationSettings = gs };
            ctx.itemPlacements.Add(new ItemPlacement(new RandoModItem { item = lm.GetItemStrict("Mantis_Claw") }, new RandoModLocation { logic = lm.GetLogicDefStrict("Loc_Multi") }));
            ctx.itemPlacements.Add(new ItemPlacement(new RandoModItem { item = lm.GetItemStrict("Grub") }, new RandoModLocation { logic = lm.GetLogicDefStrict("Loc_Multi") }));
            ctx.itemPlacements.Add(new ItemPlacement(new RandoModItem { item = lm.GetItemStrict("One_Geo") }, new RandoModLocation { logic = lm.GetLogicDefStrict("Sly") }));

            var multi = new TestMultiCostPlacement("Loc_Multi");
            multi.Add(new TestItem("Mantis_Claw"));
            multi.Add(new TestItem("Grub"));
            var placements = new Dictionary<string, AbstractPlacement> { ["Loc_Multi"] = multi };

            // 1. Multi-item self-deadlock on Mantis_Claw prevents WP_NeedsClaw and Crossroads_01[right1]
            var multiConds = new Dictionary<string, List<AccessCondition>>
            {
                ["Loc_Multi"] = new List<AccessCondition>
                {
                    new AccessCondition(AccessConditionType.LeftWallJump),
                    new AccessCondition(AccessConditionType.Grubs, 5)
                }
            };

            WorldVerificationReport before = CostValidator.Verify(
                ctx, RequirementScope.PerObject, gs, multiConds, isEligibleLocation: null, placements: placements);
            Assert.False(before.IsCompletable);
            Assert.Contains("WP_NeedsClaw", before.UnreachableWaypoints);
            Assert.Contains("Crossroads_01[right1]", before.UnreachableTransitions);

            // 2. Repair fixes multi-item conditions cleanly
            var allowed = new List<AccessConditionType> { AccessConditionType.Geo, AccessConditionType.Grubs };
            WorldVerificationReport after = CostValidator.ValidateAndRepair(
                ctx, new Random(5), allowed, settings, gs, multiConds, isEligibleLocation: null, placements: placements);
            Assert.True(after.IsCompletable, after.FormatSummary());
            Assert.Equal(1, after.VerifiedWaypointsCount - 2); // Start_State, Can_Replenish_Geo, WP_NeedsClaw -> all 3 reachable
            Assert.Equal(1, after.VerifiedTransitionsCount);

            // 3. Consistency checks catch non-positive thresholds and ineligible placements
            NightmareLogicHandler.LocationConditions["Loc_Multi"] = new AccessCondition(AccessConditionType.Geo, 0);
            NightmareLogicHandler.LocationConditions["Sly"] = new AccessCondition(AccessConditionType.Geo, 50);
            WorldVerificationReport badConsistency = CostValidator.Verify(ctx, RequirementScope.PerLocation, gs);
            Assert.False(badConsistency.IsCompletable);
            Assert.Contains(badConsistency.ConsistencyIssues, s => s.Contains("Non-positive threshold"));
        }

        // ------------------------------------------------------------------
        // AccessCondition Full Matrix & NightmareAccessCost
        // ------------------------------------------------------------------

        [Fact]
        public void AccessCondition_CoversAllMovementSpellsDreamersAndCollectibles()
        {
            PlayerData pd = CreateFreshPlayerData();
            pd.hasDash = true;
            pd.hasWalljump = true;
            pd.hasSuperDash = true;
            pd.hasDoubleJump = true;
            pd.hasAcidArmour = true;
            pd.fireballLevel = 2;
            pd.quakeLevel = 1;
            pd.screamLevel = 2;
            pd.hasCyclone = true;
            pd.hasDashSlash = true;
            pd.hasUpwardSlash = true;
            pd.lurienDefeated = true;
            pd.monomonDefeated = true;
            pd.hasCityKey = true;
            pd.hasLantern = true;
            pd.gotCharm_2 = true;
            pd.charmsOwned = 5;
            pd.mapCrossroads = true;
            pd.hasJournal = true;
            pd.journalEntriesCompleted = 12;
            pd.stationsOpened = 3;
            pd.MPReserveMax = 66;
            pd.vesselFragments = 1; // 6 + 1 = 7

            Assert.True(new AccessCondition(AccessConditionType.Dash).IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.LeftDash).IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.RightDash).IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.WallJump).IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.LeftWallJump).IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.RightWallJump).IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.Superdash).IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.LeftSuperdash).IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.RightSuperdash).IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.DoubleJump).IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.AcidArmour).IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.FireballLevel, 2).IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.QuakeLevel, 1).IsSatisfied(pd, out _));
            Assert.False(new AccessCondition(AccessConditionType.QuakeLevel, 2).IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.ScreamLevel, 2).IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.NailArt, 1, 0, "Cyclone").IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.NailArt, 1, 0, "Dash").IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.NailArt, 1, 0, "Great").IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.Dreamer, 1, 0, "Lurien").IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.Dreamer, 1, 0, "Monomon").IsSatisfied(pd, out _));
            Assert.False(new AccessCondition(AccessConditionType.Dreamer, 1, 0, "Herrah").IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.CityCrest).IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.LumaflyLantern).IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.SpecificCharm, 1, 2).IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.CharmsCount, 5).IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.MapsCount, 1).IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.JournalCount, 12).IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.StagsCount, 3).IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.VesselFragments, 7).IsSatisfied(pd, out _));
            Assert.True(new AccessCondition(AccessConditionType.SpecificJournalEntry, 1, 0, ConditionCatalog.JournalItemKey).IsSatisfied(pd, out _));

            // Unknown enum value falls back to satisfied (never softlocks)
            Assert.True(new AccessCondition((AccessConditionType)999).IsSatisfied(pd, out _));

            // NightmareAccessCost text & Includes
            var nullCost = new NightmareAccessCost(null);
            Assert.Equal("Nightmare Seal", nullCost.GetCostText());
            Assert.False(nullCost.CanPay());

            var numCost = new NightmareAccessCost(new AccessCondition(AccessConditionType.Geo, 99999));
            Assert.Contains("(have:", numCost.GetCostText());
            Assert.True(numCost.Includes(nullCost));
            Assert.False(numCost.Includes(new GeoCost(10)));

            var progCost = new NightmareAccessCost(new AccessCondition(AccessConditionType.KingsoulLevel, 3));
            Assert.Contains("(have: Lv", progCost.GetCostText());
        }

        // ------------------------------------------------------------------
        // CornerPromptHelper, ColosseumPrompt, SpoilerLog
        // ------------------------------------------------------------------

        [Fact]
        public void CornerPromptHelper_EnforcesAllScopesAndFormatsMessages()
        {
            var messages = new List<string>();
            CornerPromptHelper.SendCornerMessageOverrideForTests = (msg, _) => messages.Add(msg);
            PlayerData pd = CreateFreshPlayerData();
            pd.grubsCollected = 2;
            pd.fireballLevel = 1;
            CornerPromptHelper.PlayerDataOverrideForTests = pd;

            var item = new TestItem("Mantis_Claw", "Mantis Claw");
            var placement = new AutoPlacement("Loc_Test") { Location = new TestAutoLocation("Loc_Test", true) };
            placement.Add(item);
            CornerPromptHelper.PlacementsOverrideForTests = new Dictionary<string, AbstractPlacement>
            {
                ["Loc_Test"] = placement
            };

            // Inactive save always allows
            NightmareLogicHandler.IsSaveActive = false;
            Assert.True(CornerPromptHelper.CheckOrShowLocked("Loc_Test", "Fallback"));

            NightmareLogicHandler.IsSaveActive = true;

            // PerLocation numeric locked -> message includes "(have: 2)"
            NightmareLogicHandler.ActiveScope = RequirementScope.PerLocation;
            NightmareLogicHandler.LocationConditions["Loc_Test"] = new AccessCondition(AccessConditionType.Grubs, 5);
            Assert.False(CornerPromptHelper.CheckOrShowLocked("Loc_Test", "Fallback"));
            Assert.Single(messages);
            Assert.Equal("Mantis Claw: have 5 grubs (have: 2)", messages[0]);

            // PerObjectGroup progressive locked -> message includes "(have: Lv1)"
            messages.Clear();
            NightmareLogicHandler.ActiveScope = RequirementScope.PerObjectGroup;
            NightmareLogicHandler.ItemTypeConditions["Mantis_Claw"] = new AccessCondition(AccessConditionType.FireballLevel, 2);
            Assert.False(CornerPromptHelper.CheckOrShowLocked("Loc_Test", "Fallback"));
            Assert.Equal("Mantis Claw: have Shade Soul (Lv2) (have: Lv1)", messages[0]);

            // PerObject boolean locked -> no "(have: ...)" suffix
            messages.Clear();
            NightmareLogicHandler.ActiveScope = RequirementScope.PerObject;
            NightmareLogicHandler.ObjectConditions["Loc_Test"] = new AccessCondition(AccessConditionType.DoubleJump);
            Assert.False(CornerPromptHelper.CheckOrShowLocked("Loc_Test", "Fallback"));
            Assert.Equal("Mantis Claw: have Monarch Wings", messages[0]);

            // Obtained placement always passes
            item.Give(placement, new GiveInfo
            {
                FlingType = FlingType.DirectDeposit,
                MessageType = MessageType.None,
                Container = Container.Unknown
            });
            Assert.True(CornerPromptHelper.CheckOrShowLocked("Loc_Test", "Fallback"));

            // Excluded location always passes
            Assert.True(CornerPromptHelper.CheckOrShowLocked("Start", "Start"));
        }

        [Fact]
        public void ColosseumPrompt_ResolvesPlacementsTagsScopesAndObtainedStatus()
        {
            NightmareLogicHandler.IsSaveActive = true;

            // 1. YNShinyPlacement on Trial 1
            var t1 = new YNShinyPlacement("Charm_Notch-Colosseum")
            {
                Cost = new NightmareAccessCost(new AccessCondition(AccessConditionType.Grubs, 99))
            };
            var t1Item = new TestItem("Charm_Notch");
            t1.Add(t1Item);

            // 2. ISingleCostPlacement on Trial 2 (Pale_Ore-Colosseum)
            var t2 = new TestSingleCostPlacement("Pale_Ore-Colosseum")
            {
                Cost = new NightmareAccessCost(new AccessCondition(AccessConditionType.Geo, 0))
            };
            t2.Add(new TestItem("Pale_Ore"));

            // 3. CostTag + PerObjectGroup on Trial 3
            var t3 = new AutoPlacement("Colosseum_Gold") { Location = new TestAutoLocation("Colosseum_Gold", true) };
            t3.Add(new TestItem("Mantis_Claw"));
            t3.AddTag<NightmareAccessTag>().Condition = new AccessCondition(AccessConditionType.SimpleKeys, 99);

            ColosseumPrompt.PlacementsOverrideForTests = new Dictionary<string, AbstractPlacement>
            {
                ["Charm_Notch-Colosseum"] = t1,
                ["Pale_Ore-Colosseum"] = t2,
                ["Colosseum_Gold"] = t3
            };

            string p1 = ColosseumPrompt.ProcessPrompt("TRIAL_BOARD_BRONZE", "Trial of the Warrior\nPlace a mark to begin?");
            Assert.Contains("Requirement: have 99 grubs", p1);
            // Already-annotated prompt is not duplicated
            Assert.Equal(p1, ColosseumPrompt.ProcessPrompt("TRIAL_BOARD_BRONZE", p1));

            // Trial 2 has Geo 0 -> Met!
            string p2 = ColosseumPrompt.ProcessPrompt("TRIAL_BOARD_SILVER", "Trial of the Conqueror\nPlace a mark to begin?");
            Assert.Contains("Requirement: have 0 geo (Met)", p2);

            // Trial 3 uses NightmareAccessTag
            string p3 = ColosseumPrompt.ProcessPrompt("TRIAL_BOARD_GOLD", "Trial of the Fool\nPlace a mark to begin?");
            Assert.Contains("Requirement: have 99 simple keys", p3);

            // Completed Trial 1 shows "Completed (Met)"
            t1Item.Give(t1, new GiveInfo
            {
                FlingType = FlingType.DirectDeposit,
                MessageType = MessageType.None,
                Container = Container.Unknown
            });
            string p1Done = ColosseumPrompt.ProcessPrompt("TRIAL_BOARD_BRONZE", "Trial of the Warrior\nbegin the Trial");
            Assert.Contains("Requirement: Completed (Met)", p1Done);

            // Free Trial (no placement & no condition) shows "None (Met)"
            ColosseumPrompt.PlacementsOverrideForTests = new Dictionary<string, AbstractPlacement>();
            string pFree = ColosseumPrompt.ProcessPrompt("TRIAL_BOARD_BRONZE", "Trial of the Warrior\nbegin the Trial");
            Assert.Contains("Requirement: None (Met)", pFree);
        }

        [Fact]
        public void NightmareSpoilerLog_FormatsAllScopesAndVerificationErrors()
        {
            var lmb = new LogicManagerBuilder();
            lmb.AddItem(new EmptyItem("One_Geo"));
            lmb.AddItem(new EmptyItem("Mantis_Claw"));
            lmb.AddLogicDef(new RawLogicDef("Loc_A", "TRUE"));
            var lm = new LogicManager(lmb);
            var ctx = new RandoModContext(lm);
            ctx.itemPlacements.Add(new ItemPlacement(new RandoModItem { item = lm.GetItemStrict("Mantis_Claw") }, new RandoModLocation { logic = lm.GetLogicDefStrict("Loc_A") }));
            ctx.itemPlacements.Add(new ItemPlacement(new RandoModItem { item = lm.GetItemStrict("One_Geo") }, new RandoModLocation { logic = lm.GetLogicDefStrict("Loc_A") }));

            var report = new WorldVerificationReport
            {
                Scope = RequirementScope.PerObject,
                BaselineLocationsCount = 2,
                VerifiedLocationsCount = 1
            };
            report.UnreachableLocations.Add("Loc_B [Grub]");
            report.UnreachableWaypoints.Add("WP_X");
            report.UnreachableTransitions.Add("Town[left1]");
            report.ConsistencyIssues.Add("Test issue");

            NightmareLogicHandler.ItemTypeConditions["Geo"] = new AccessCondition(AccessConditionType.Geo, 100);
            NightmareLogicHandler.ItemTypeConditions["Grubs"] = null;
            string groupText = NightmareSpoilerLog.BuildSpoilerText(ctx, RequirementScope.PerObjectGroup, 80, report);
            Assert.Contains("=== Object Group Conditions ===", groupText);
            Assert.Contains("Geo: have 100 geo", groupText);
            Assert.Contains("Grubs: Free", groupText);
            Assert.Contains("Unreachable location: Loc_B [Grub]", groupText);
            Assert.Contains("Unreachable waypoint: WP_X", groupText);
            Assert.Contains("Unreachable transition: Town[left1]", groupText);
            Assert.Contains("Consistency issue: Test issue", groupText);

            NightmareLogicHandler.ObjectConditions["Loc_A"] = new AccessCondition(AccessConditionType.DoubleJump);
            NightmareLogicHandler.ObjectConditions["Loc_Free"] = null;
            string objText = NightmareSpoilerLog.BuildSpoilerText(ctx, RequirementScope.PerObject, 100, null);
            Assert.Contains("=== Per-Object Conditions ===", objText);
            Assert.Contains("Loc_A [Mantis_Claw, One_Geo]: have Monarch Wings", objText);
            Assert.Contains("Loc_Free: Free", objText);
        }

        // ------------------------------------------------------------------
        // Integration Helpers & Routing Tables
        // ------------------------------------------------------------------

        [Fact]
        public void FlowerRandomizerInGameGating_ResolvesAll13RoutesAccurately()
        {
            Assert.True(FlowerRandomizerInGameGating.TryResolveGateRoute("Town", "Elderbug", "Conversation Control", out var elderbug));
            Assert.Equal("Flower_Quest-Elderbug", elderbug.PlacementName);
            Assert.Equal("Decline", elderbug.CustomBlockState);

            Assert.True(FlowerRandomizerInGameGating.TryResolveGateRoute("Room_nailmaster_03", "NM Oro NPC", "Conversation Control", out var oro));
            Assert.Equal("Decline Pause 2", oro.CustomBlockState);

            Assert.True(FlowerRandomizerInGameGating.TryResolveGateRoute("GG_Waterways", "Godseeker Awake", "Conversation Control", out var godseeker));
            Assert.Equal("Decline Pause", godseeker.CustomBlockState);

            Assert.True(FlowerRandomizerInGameGating.TryResolveGateRoute("Ruins_House_03", "Emilitia NPC", "Conversation Control", out _));
            Assert.True(FlowerRandomizerInGameGating.TryResolveGateRoute("Room_Queen", "Queen", "Conversation Control", out _));
            Assert.True(FlowerRandomizerInGameGating.TryResolveGateRoute("Room_nailmaster", "NM Mato NPC", "Conversation Control", out _));
            Assert.True(FlowerRandomizerInGameGating.TryResolveGateRoute("Room_nailmaster_02", "NM Sheo NPC", "Conversation Control", out var sheo1));
            Assert.True(FlowerRandomizerInGameGating.TryResolveGateRoute("Room_nailmaster_02", "NM Sheo NPC Modeller", "Conversation Control", out var sheo2));
            Assert.Equal(sheo1.PlacementName, sheo2.PlacementName);
            Assert.True(FlowerRandomizerInGameGating.TryResolveGateRoute("Ruins_Bathhouse", "Ghost NPC", "Conversation Control", out _));
            Assert.True(FlowerRandomizerInGameGating.TryResolveGateRoute("Deepnest_41", "NPC", "Conversation Control", out _));
            Assert.True(FlowerRandomizerInGameGating.TryResolveGateRoute("Waterways_13", "Dream Dialogue", "npc_dream_dialogue", out _));
            Assert.True(FlowerRandomizerInGameGating.TryResolveGateRoute("Mines_34", "Dream Dialogue", "npc_dream_dialogue", out _));
            Assert.True(FlowerRandomizerInGameGating.TryResolveGateRoute("White_Palace_09", "Dream Dialogue", "npc_dream_dialogue", out _));
            Assert.True(FlowerRandomizerInGameGating.TryResolveGateRoute("White_Palace_20", "End Scene", "Conversation Control", out var pain));
            Assert.Equal("Disappear Scne", pain.CustomBlockState);

            // Non-quest NPCs sharing generic names must NOT match
            Assert.False(FlowerRandomizerInGameGating.TryResolveGateRoute("Ruins_Bathhouse", "NPC", "Conversation Control", out _));
            Assert.False(FlowerRandomizerInGameGating.TryResolveGateRoute("Town", "Elderbug Grimm", "Conversation Control", out _));
            Assert.False(FlowerRandomizerInGameGating.TryResolveGateRoute("Town", "Dream Dialogue", "npc_dream_dialogue", out _));
        }

        [Fact]
        public void YarcIntegration_OrbNameResolution_AreaMaps_AndSimulation()
        {
            Assert.Equal("DreamOrb_Crossroads_1", YarcIntegration.TryBuildDreamOrbPlacementName("Crossroads_07", "Dream Plant Orb"));
            Assert.Equal("DreamOrb_FungalWastes_Canyon_8", YarcIntegration.TryBuildDreamOrbPlacementName("Fungus2_33", "Dream Plant Orb (7)"));
            Assert.Equal("DreamOrb_KingdomsEdge_2", YarcIntegration.TryBuildDreamOrbPlacementName("Deepnest_East_07", "Dream Plant Orb (1)"));
            Assert.Null(YarcIntegration.TryBuildDreamOrbPlacementName("Crossroads_07", "Unrelated Object"));

            Assert.Equal("Crossroads", ConditionCatalog.GetRootAreaFromScene("Crossroads_07"));
            Assert.Equal("Ancestral_Mound", ConditionCatalog.GetRootAreaFromScene("Crossroads_ShamanTemple"));
            Assert.Equal("Unknown_Scene", ConditionCatalog.GetRootAreaFromScene("Unknown_Scene"));

            var sim = new VerificationSimulationContext();
            YarcIntegration.Instance.OnSimulationItemAdded(YarcIntegration.EssenceOrbItem, sim);
            Assert.Equal(1, sim.ExtraEssence);
            Assert.True(sim.NeedsFullRecheck);
            Assert.Equal(1, YarcIntegration.Instance.GetAdditionalNumericAmount(AccessConditionType.Essence, null, sim));
            Assert.Equal(0, YarcIntegration.Instance.GetAdditionalNumericAmount(AccessConditionType.Grubs, null, sim));
            Assert.True(YarcIntegration.Instance.HasAdditionalProgressionEffect(new RandoModItem { item = new EmptyItem(YarcIntegration.EssenceOrbItem) }));
        }

        [Fact]
        public void IntegrationRegistry_HandlesReplacementAndNullSafely()
        {
            try
            {
                IntegrationRegistry.EnsureBuiltInRegistered();
                int baselineCount = IntegrationRegistry.All.Count;

                IntegrationRegistry.Register(null);
                Assert.Equal(baselineCount, IntegrationRegistry.All.Count);

                var custom = new DummyReplacementIntegration();
                IntegrationRegistry.Register(custom);
                Assert.Equal(baselineCount, IntegrationRegistry.All.Count);
                Assert.True(IntegrationRegistry.TryOverrideLogicExpression(
                    new AccessCondition(AccessConditionType.Geo, 10), new GenerationSettings(), out string expr));
                Assert.Equal("CUSTOM_EXPR", expr);
            }
            finally
            {
                IntegrationRegistry.ResetForTests();
                IntegrationRegistry.EnsureBuiltInRegistered();
            }
        }

        [Fact]
        public void CostApplier_PerObjectGroup_AppliesDistinctItemCostTagsForMultiItemFishingSpot()
        {
            var settings = new NightmareAccessSettings
            {
                Enabled = true,
                Scope = RequirementScope.PerObjectGroup,
                RequirementChance = 100
            };
            foreach (var prop in typeof(NightmareAccessSettings).GetProperties())
            {
                if (prop.PropertyType == typeof(bool) && prop.Name.StartsWith("Allow", StringComparison.Ordinal))
                {
                    prop.SetValue(settings, false, null);
                }
            }
            settings.AllowGeo = true;
            settings.MinGeo = 10;
            settings.MaxGeo = 100;

            var gs = new GenerationSettings { Seed = 424 };
            var lmb = new LogicManagerBuilder();
            lmb.GetOrAddTerm("GEO", TermType.SignedByte);
            lmb.GetOrAddTerm("Can_Replenish_Geo", TermType.SignedByte);
            lmb.GetOrAddTerm("GRUBS", TermType.SignedByte);
            lmb.AddWaypoint(new RawWaypointDef("Start_State", "TRUE", stateless: false));
            lmb.AddWaypoint(new RawWaypointDef("Can_Replenish_Geo", "TRUE", stateless: true));
            lmb.AddItem(new SingleItem("Grub", new TermValue(lmb.GetTerm("GRUBS"), 1)));
            lmb.AddItem(new EmptyItem("One_Geo"));
            lmb.AddLogicDef(new RawLogicDef("Fishing_Spot-Lake_of_Unn", "TRUE"));

            var lm = new LogicManager(lmb);
            var ctx = new RandoModContext(lm) { GenerationSettings = gs };
            ctx.itemPlacements.Add(new ItemPlacement(new RandoModItem { item = lm.GetItemStrict("One_Geo") }, new RandoModLocation { logic = lm.GetLogicDefStrict("Fishing_Spot-Lake_of_Unn") }));
            ctx.itemPlacements.Add(new ItemPlacement(new RandoModItem { item = lm.GetItemStrict("Grub") }, new RandoModLocation { logic = lm.GetLogicDefStrict("Fishing_Spot-Lake_of_Unn") }));

            var fishSpot = new AutoPlacement("Fishing_Spot-Lake_of_Unn")
            {
                Location = new TestAutoLocation("Fishing_Spot-Lake_of_Unn", supportsCost: false)
            };
            var fishGeo = new TestItem("One_Geo");
            var fishGrub = new TestItem("Grub");
            fishSpot.Add(fishGeo);
            fishSpot.Add(fishGrub);

            var placements = new Dictionary<string, AbstractPlacement>(StringComparer.Ordinal)
            {
                ["Fishing_Spot-Lake_of_Unn"] = fishSpot
            };

            NightmareLogicHandler.ItemTypeConditions["Geo"] = new AccessCondition(AccessConditionType.Geo, 25);
            NightmareLogicHandler.ItemTypeConditions["Grubs"] = new AccessCondition(AccessConditionType.Geo, 80);
            NightmareLogicHandler.IsSaveActive = true;
            NightmareLogicHandler.ActiveScope = RequirementScope.PerObjectGroup;

            CostApplier.ApplyToPlacements(placements, ctx, settings, gs);

            Assert.Null(fishSpot.GetTag<CostTag>());
            Assert.Equal(25, NightmareAccessCost.Find(fishGeo.GetTag<CostTag>()?.Cost)?.Condition.RequiredAmount);
            Assert.Equal(80, NightmareAccessCost.Find(fishGrub.GetTag<CostTag>()?.Cost)?.Condition.RequiredAmount);
            Assert.True(NightmareLogicHandler.LastVerificationReport.IsCompletable);
        }

        private sealed class DummyReplacementIntegration : ModIntegrationBase
        {
            public override string Id => "AlphabetRando";
            public override bool TryOverrideLogicExpression(AccessCondition condition, GenerationSettings gs, out string expr)
            {
                expr = "CUSTOM_EXPR";
                return true;
            }
        }
    }
}
