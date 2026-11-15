using System;
using System.Collections.Generic;
using ItemChanger;
using NightmareAccess.InGame;
using NightmareAccess.Interop;
using NightmareAccess.Logic;
using RandomizerCore.Logic;
using RandomizerMod.Settings;

namespace NightmareAccess.Integrations.ExtraRando
{
    internal sealed class ExtraRandoIntegration : ModIntegrationBase
    {
        public static ExtraRandoIntegration Instance { get; } = new();

        public const string ModId = "ExtraRando";
        public const string SplitVengefulSpiritLocation = "Split_Vengeful_Spirit";
        public const string SplitShadeSoulLocation = "Split_Shade_Soul";
        public const string Gpz10Location = "GPZ_10";
        public const string WhiteDefender5Location = "White_Defender_5";
        public const string BardoonButtLocation = "Bardoon_Butt";
        public const string DirtmouthStagDoorLocation = "Dirtmouth_Stag_Door";
        public const string KeyRingItem = "Key_Ring";
        public const string DirtmouthStagKeyItem = "Dirtmouth_Stag_Key";

        private static readonly string[] HotSpringLocations =
        {
            "Crossroads-Hot_Spring",
            "Colosseum-Hot_Spring",
            "Deepnest-Hot_Spring",
            "Bathhouse-Hot_Spring",
            "Lower_Godhome-Hot_Spring",
            "Upper_Godhome-Hot_Spring"
        };

        private static readonly string[] PantheonLocations =
        {
            "Pantheon-Master",
            "Pantheon-Artist",
            "Pantheon-Sage",
            "Pantheon-Knight",
            "Pantheon-Hallownest"
        };

        private ExtraRandoIntegration() { }

        public override string Id => ModId;

        public override void Initialize()
        {
            ExtraRandoInGameGating.Hook();
        }

        public override IEnumerable<string> GetAdditionalLogicLocations(LogicManagerBuilder lmb, GenerationSettings gs)
        {
            if (lmb?.LogicLookup == null) yield break;

            if (ExtraRandoInterop.IsSplitFireballEnabled() && gs?.PoolSettings?.Skills == true)
            {
                if (lmb.LogicLookup.ContainsKey(SplitVengefulSpiritLocation)) yield return SplitVengefulSpiritLocation;
                if (lmb.LogicLookup.ContainsKey(SplitShadeSoulLocation)) yield return SplitShadeSoulLocation;
            }

            if (ExtraRandoInterop.IsRandomizeAwfulLocationsEnabled())
            {
                if (lmb.LogicLookup.ContainsKey(Gpz10Location)) yield return Gpz10Location;
                if (lmb.LogicLookup.ContainsKey(WhiteDefender5Location)) yield return WhiteDefender5Location;
            }

            if (ExtraRandoInterop.IsRandomizeButtEnabled() && lmb.LogicLookup.ContainsKey(BardoonButtLocation))
            {
                yield return BardoonButtLocation;
            }

            if (ExtraRandoInterop.IsRandomizeHotSpringsEnabled())
            {
                foreach (string loc in HotSpringLocations)
                {
                    if (lmb.LogicLookup.ContainsKey(loc)) yield return loc;
                }
            }

            if (ExtraRandoInterop.IsRandomizePantheonAccessEnabled())
            {
                foreach (string loc in PantheonLocations)
                {
                    if (lmb.LogicLookup.ContainsKey(loc)) yield return loc;
                }
            }
        }

        public override bool IsExcludedLocation(string locationName)
        {
            return locationName == DirtmouthStagDoorLocation;
        }

        public override bool IsCloakSplit(GenerationSettings gs)
        {
            return gs?.PoolSettings?.Skills == true && ExtraRandoInterop.IsSplitShadeCloakEnabled();
        }

        public override bool TryOverrideLogicExpression(AccessCondition condition, GenerationSettings gs, out string expr)
        {
            // Under SplitFireball, Left_VS + Right_VS pushes FIREBALL to 2 while in-game fireballLevel stays 1
            if (condition?.Type == AccessConditionType.FireballLevel
                && condition.RequiredAmount >= 2
                && gs?.PoolSettings?.Skills == true
                && ExtraRandoInterop.IsSplitFireballEnabled())
            {
                expr = "(FIREBALLLEFT>1 | FIREBALLRIGHT>1)";
                return true;
            }

            expr = null;
            return false;
        }

        public override string TryGetItemTypeGroup(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return null;

            if (itemName == KeyRingItem) return "SimpleKeys";

            if (itemName is "Cloak" or "Left_Cloak" or "Right_Cloak"
                or "Progressive_Left_Cloak" or "Progressive_Right_Cloak")
            {
                return "Mothwing_Cloak";
            }

            if (itemName is "Fireball_Spell" or "Left_Vengeful_Spirit" or "Right_Vengeful_Spirit"
                or "Left_Shade_Soul" or "Right_Shade_Soul" or "Left_Fireball" or "Right_Fireball")
            {
                return "Vengeful_Spirit";
            }

            if (itemName == "Dive_Spell") return "Desolate_Dive";
            if (itemName == "Scream_Spell") return "Howling_Wraiths";
            if (itemName == "Hot_Spring_Water") return "Soul";

            if (itemName is "Scarab_Marker_Hint" or "Shell_Marker_Hint"
                or "Gleaming_Marker_Hint" or "Token_Marker_Hint")
            {
                return "HintMarkers";
            }

            if (itemName is "Colo_Ticket-Bronze" or "Colo_Ticket-Silver" or "Colo_Ticket-Gold")
            {
                return "Colo_Tickets";
            }

            if (itemName.StartsWith("Pantheon_Access-", StringComparison.Ordinal))
            {
                return "Pantheon_Access";
            }

            if (itemName == "Bardoon_Butt_Smack") return "Bardoon_Butt_Smack";
            if (itemName == DirtmouthStagKeyItem) return DirtmouthStagKeyItem;

            return null;
        }

        public override AccessConditionType? TryGetItemResourceType(string itemName)
        {
            return itemName == KeyRingItem ? AccessConditionType.SimpleKeys : null;
        }

        public override bool IsNonResourceItem(string itemName)
        {
            return itemName != KeyRingItem && TryGetItemTypeGroup(itemName) != null;
        }

        public override bool IsItemSelfConflicting(string itemName, AccessConditionType conditionType, bool isGroupMode)
        {
            return itemName switch
            {
                "Cloak" => conditionType is AccessConditionType.Dash
                    or AccessConditionType.LeftDash
                    or AccessConditionType.RightDash,

                "Left_Cloak" or "Progressive_Left_Cloak" => conditionType is AccessConditionType.Dash
                    or AccessConditionType.LeftDash,

                "Right_Cloak" or "Progressive_Right_Cloak" => conditionType is AccessConditionType.Dash
                    or AccessConditionType.RightDash,

                "Fireball_Spell" or "Left_Vengeful_Spirit" or "Right_Vengeful_Spirit"
                    or "Left_Shade_Soul" or "Right_Shade_Soul"
                    or "Left_Fireball" or "Right_Fireball" => conditionType == AccessConditionType.FireballLevel,

                "Dive_Spell" => conditionType == AccessConditionType.QuakeLevel,
                "Scream_Spell" => conditionType == AccessConditionType.ScreamLevel,
                _ => false
            };
        }

        public override bool? CanPlacementAcceptCost(string locationName, AbstractPlacement placement)
        {
            return IsInPlaceAutoLocation(locationName) ? true : null;
        }

        public override bool TryApplyCost(
            string locationName,
            AbstractPlacement placement,
            NightmareAccessCost cost,
            IDictionary<string, AbstractPlacement> placements)
        {
            if (!IsInPlaceAutoLocation(locationName)) return false;

            if (placement != null && cost != null)
            {
                CostTag tag = placement.GetOrAddTag<CostTag>();
                tag.Cost = CostApplier.CombineCost(tag.Cost, cost);
            }

            return true;
        }

        public override bool TryGetNumericAmountOverride(AccessConditionType type, PlayerData pd, out int amount)
        {
            // Ignore pd.hasShadowDash when SplitShadeCloakModule is active because it sets hasShadowDash on a single side
            if (type is AccessConditionType.Dash or AccessConditionType.LeftDash or AccessConditionType.RightDash
                && pd != null
                && ExtraRandoInterop.TryGetSplitDashState(out bool modLeft, out bool modRight))
            {
                bool hasLeft = modLeft || pd.hasDash;
                bool hasRight = modRight || pd.hasDash;
                amount = type switch
                {
                    AccessConditionType.Dash => (hasLeft && hasRight) ? 1 : 0,
                    AccessConditionType.LeftDash => hasLeft ? 1 : 0,
                    AccessConditionType.RightDash => hasRight ? 1 : 0,
                    _ => 0
                };
                return true;
            }

            amount = 0;
            return false;
        }

        public override bool ShouldBlockAutoGive(AbstractPlacement placement, GiveInfo info)
        {
            if (placement?.Name == BardoonButtLocation && info?.Container == "Butt")
            {
                return !CornerPromptHelper.CheckOrShowLocked(placement.Name, "Bardoon's Butt");
            }

            if (placement != null
                && IsPantheonLocation(placement.Name)
                && info?.Container != Container.Shiny
                && info?.FlingType == FlingType.DirectDeposit)
            {
                return !CornerPromptHelper.CheckOrShowLocked(
                    placement.Name, FsmGateHelper.FormatPlacementName(placement.Name));
            }

            return false;
        }

        private static bool IsInPlaceAutoLocation(string locationName)
        {
            if (string.IsNullOrEmpty(locationName)) return false;

            return locationName is BardoonButtLocation
                       or SplitVengefulSpiritLocation
                       or WhiteDefender5Location
                   || IsHotSpringLocation(locationName)
                   || IsPantheonLocation(locationName);
        }

        private static bool IsHotSpringLocation(string locationName) =>
            Array.IndexOf(HotSpringLocations, locationName) >= 0;

        private static bool IsPantheonLocation(string locationName) =>
            Array.IndexOf(PantheonLocations, locationName) >= 0;
    }
}