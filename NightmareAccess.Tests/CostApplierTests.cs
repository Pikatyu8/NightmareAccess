using System.Collections.Generic;
using ItemChanger;
using ItemChanger.Locations;
using ItemChanger.Locations.SpecialLocations;
using ItemChanger.Placements;
using ItemChanger.Tags;
using NightmareAccess.Logic;
using Xunit;

namespace NightmareAccess.Tests
{
    public sealed class CostApplierTests
    {
        [Fact]
        public void CanPlacementAcceptCost_CorrectlyDistinguishesPlacementTypes()
        {
            var shiny = new YNShinyPlacement("Claw");
            var mutable = new MutablePlacement("Grub-Test") { Location = new CoordinateLocation { name = "Grub-Test", sceneName = "Crossroads_01" } };
            var dual = new DualPlacement("Defender's_Crest") { falseLocation = new CoordinateLocation(), trueLocation = new CoordinateLocation() };
            var autoUnsupported = new AutoPlacement("Custom_Auto") { Location = new DreamBossLocation { name = "Custom_Auto", sceneName = "Crossroads_10" } };
            var autoSupported = new AutoPlacement("Crossroads_Map") { Location = new CorniferLocation { name = "Crossroads_Map", sceneName = "Crossroads_33" } };
            var nonReplaceableUnknown = new ExistingContainerPlacement("Chest_Test")
            {
                Location = new ExistingFsmContainerLocation { containerType = "UnsupportedType", nonreplaceable = true }
            };
            var replaceable = new ExistingContainerPlacement("Tablet_Test")
            {
                Location = new ExistingFsmContainerLocation { containerType = "Tablet", nonreplaceable = false }
            };

            Assert.True(CostApplier.CanPlacementAcceptCost("Claw", shiny));
            Assert.True(CostApplier.CanPlacementAcceptCost("Grub-Test", mutable));
            Assert.True(CostApplier.CanPlacementAcceptCost("Defender's_Crest", dual));
            Assert.False(CostApplier.CanPlacementAcceptCost("Custom_Auto", autoUnsupported));
            Assert.True(CostApplier.CanPlacementAcceptCost("Crossroads_Map", autoSupported));
            Assert.False(CostApplier.CanPlacementAcceptCost("Chest_Test", nonReplaceableUnknown));
            Assert.True(CostApplier.CanPlacementAcceptCost("Tablet_Test", replaceable));
        }

        [Fact]
        public void ApplyCost_PreservesExistingCost_AndDoesNotDestroyDualPlacement()
        {
            var naCost = new NightmareAccessCost(new AccessCondition(AccessConditionType.SimpleKeys, 1));
            var placements = new Dictionary<string, AbstractPlacement>();

            // 1. MutablePlacement with existing GeoCost -> converted to YNShinyPlacement with MultiCost
            var mutable = new MutablePlacement("Vessel_Fragment-Basin")
            {
                Location = new CoordinateLocation { name = "Vessel_Fragment-Basin", sceneName = "Abyss_04" },
                Cost = new GeoCost(3000)
            };
            placements[mutable.Name] = mutable;
            CostApplier.ApplyCost(mutable.Name, mutable, naCost, placements);

            var converted = Assert.IsType<YNShinyPlacement>(placements[mutable.Name]);
            var multi = Assert.IsType<MultiCost>(converted.Cost);
            Assert.Equal(2, multi.Count);
            Assert.Contains(multi, c => c is GeoCost gc && gc.amount == 3000);
            Assert.Same(naCost, NightmareAccessCost.Find(converted.Cost));

            // 2. DualPlacement stays DualPlacement
            var dual = new DualPlacement("Simple_Key-Lurker")
            {
                falseLocation = new CoordinateLocation { name = "Simple_Key-Lurker", sceneName = "GG_Lurker" },
                trueLocation = new CoordinateLocation { name = "Simple_Key-Lurker", sceneName = "GG_Lurker" },
                Cost = new GeoCost(50)
            };
            placements[dual.Name] = dual;
            CostApplier.ApplyCost(dual.Name, dual, naCost, placements);

            var preservedDual = Assert.IsType<DualPlacement>(placements[dual.Name]);
            Assert.IsType<MultiCost>(preservedDual.Cost);
            Assert.Same(naCost, NightmareAccessCost.Find(preservedDual.Cost));

            // 3. Whispering_Root stays AutoPlacement and uses CostTag
            var root = new AutoPlacement("Whispering_Root-Crossroads")
            {
                Location = new WhisperingRootLocation { name = "Whispering_Root-Crossroads", sceneName = "Crossroads_07" }
            };
            placements[root.Name] = root;
            CostApplier.ApplyCost(root.Name, root, naCost, placements);

            Assert.IsType<AutoPlacement>(placements[root.Name]);
            Assert.Same(naCost, root.GetTag<CostTag>()?.Cost);
        }
    }
}
