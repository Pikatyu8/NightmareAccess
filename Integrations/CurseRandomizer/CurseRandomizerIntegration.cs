using System;
using System.Collections.Generic;
using NightmareAccess.Interop;
using NightmareAccess.Logic;
using RandomizerCore.Logic;
using RandomizerMod.Settings;

namespace NightmareAccess.Integrations.CurseRandomizer
{
    internal sealed class CurseRandomizerIntegration : ModIntegrationBase
    {
        public static CurseRandomizerIntegration Instance { get; } = new();

        public const string ModId = "CurseRandomizer";
        public const string CursedVesselSaveKey = "CurseRandomizer.CursedVessel";
        public const string CursePrefix = "Fool_Item-";
        public const string EvilPrefix = "Fool_Item-Evil_";
        public const string GeoWalletItem = "Geo_Wallet";
        public const string DreamnailFragmentItem = "Dreamnail_Fragment";

        private CurseRandomizerIntegration() { }

        public override string Id => ModId;

        public override void RecordSaveState(GenerationSettings gs, IDictionary<string, int> saveInts)
        {
            if (saveInts == null) return;

            int cursedVessel = CurseRandomizerInterop.GetCursedVesselCount();
            if (cursedVessel > 0)
            {
                saveInts[CursedVesselSaveKey] = cursedVessel;
            }
            else
            {
                saveInts.Remove(CursedVesselSaveKey);
            }
        }

        public override string TryGetItemTypeGroup(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return null;

            if (itemName == GeoWalletItem) return GeoWalletItem;
            if (itemName == DreamnailFragmentItem) return DreamnailFragmentItem;
            if (itemName == "OMEN") return "Curses";

            if (itemName.StartsWith(EvilPrefix, StringComparison.Ordinal))
            {
                return "Curses";
            }

            if (itemName.StartsWith(CursePrefix, StringComparison.Ordinal))
            {
                string inner = itemName.Substring(CursePrefix.Length);
                if (!string.IsNullOrEmpty(inner) && !inner.StartsWith(CursePrefix, StringComparison.Ordinal))
                {
                    return NightmareLogicHandler.GetItemTypeGroup(inner);
                }
                return "Curses";
            }

            return null;
        }

        public override bool TryGetNumericAmountOverride(AccessConditionType type, PlayerData pd, out int amount)
        {
            if (type == AccessConditionType.VesselFragments
                && CurseRandomizerInterop.TryGetCollectedVesselFragments(pd, out amount))
            {
                return true;
            }

            amount = 0;
            return false;
        }

        public override bool TryOverrideLogicNumericAmount(
            AccessConditionType type, ProgressionManager pm, VerificationSimulationContext sim, out int amount)
        {
            amount = 0;
            if (pm?.lm == null) return false;

            if (type == AccessConditionType.Geo
                && pm.lm.GetTerm("WALLET") is Term walletTerm
                && CurseRandomizerInterop.TryGetWalletCapacities(out int[] capacities)
                && capacities.Length > 0)
            {
                int wallets = Math.Max(0, pm.Get(walletTerm));
                Term canReplenish = pm.lm.GetTerm("Can_Replenish_Geo");
                Term geoTerm = pm.lm.GetTerm("GEO");
                int rawGeo = (canReplenish != null && pm.Has(canReplenish))
                    ? int.MaxValue
                    : Math.Max(0, geoTerm != null ? pm.Get(geoTerm) : 0);

                amount = wallets < capacities.Length ? Math.Min(rawGeo, capacities[wallets]) : rawGeo;
                return true;
            }

            if (type == AccessConditionType.VesselFragments)
            {
                int cursedVessel = NightmareLogicHandler.IntegrationInts.TryGetValue(CursedVesselSaveKey, out int cv)
                    ? cv
                    : CurseRandomizerInterop.GetCursedVesselCount();

                if (cursedVessel > 0 && pm.lm.GetTerm("VESSELFRAGMENTS") is Term vfTerm)
                {
                    // CurseRandomizer initializes VESSELFRAGMENTS at +(6 - 3 * CursedVessel) instead of 0.
                    amount = Math.Max(0, pm.Get(vfTerm) - (6 - cursedVessel * 3));
                    return true;
                }
            }

            return false;
        }

        public override IEnumerable<Term> GetAdditionalNumericTerms(AccessConditionType type, LogicManager lm)
        {
            if (type == AccessConditionType.Geo && lm?.GetTerm("WALLET") is Term walletTerm)
            {
                yield return walletTerm;
            }
        }
    }
}
