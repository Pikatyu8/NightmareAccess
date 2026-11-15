using System;
using NightmareAccess.Interop;
using NightmareAccess.Logic;
using RandomizerCore.Logic;

namespace NightmareAccess.Integrations.CombatRandomizer
{
    internal sealed class CombatRandomizerIntegration : ModIntegrationBase
    {
        public static CombatRandomizerIntegration Instance { get; } = new();

        public const string ModId = "CombatRandomizer";
        public const string NailDamageItem = "Nail_Damage";
        public const string NotchFragmentItem = "Notch_Fragment";
        public const string SoulGainItem = "Soul_Gain";
        public const string SoulPlugItem = "Soul_Plug";

        private CombatRandomizerIntegration() { }

        public override string Id => ModId;

        public override string TryGetItemTypeGroup(string itemName)
        {
            return itemName switch
            {
                NotchFragmentItem => "Notches",
                SoulGainItem or SoulPlugItem => "Soul_Upgrades",
                NailDamageItem => NailDamageItem,
                _ => null
            };
        }

        public override AccessConditionType? TryGetItemResourceType(string itemName)
        {
            return itemName == NotchFragmentItem ? AccessConditionType.Notches : null;
        }

        public override void OnSimulationItemAdded(string itemName, VerificationSimulationContext sim)
        {
            if (itemName == NotchFragmentItem)
            {
                sim.NotchFragmentsObtained++;
            }
        }

        public override bool TryOverrideLogicNumericAmount(
            AccessConditionType type, ProgressionManager pm, VerificationSimulationContext sim, out int amount)
        {
            amount = 0;
            if (type != AccessConditionType.Notches || pm?.lm == null || sim.NotchFragmentsObtained <= 0)
            {
                return false;
            }

            if (!CombatRandomizerInterop.TryGetFragmentsPerNotch(out int fragmentsPerNotch) || fragmentsPerNotch <= 1)
            {
                return false;
            }

            Term notchesTerm = pm.lm.GetTerm("NOTCHES");
            int rawNotches = notchesTerm != null ? pm.Get(notchesTerm) - 3 : 0;
            // CombatRandomizer's logic increments NOTCHES by 1 per fragment, whereas in-game 1 slot requires FragmentsPerNotch fragments.
            int adjusted = rawNotches - sim.NotchFragmentsObtained + (sim.NotchFragmentsObtained / fragmentsPerNotch);
            amount = Math.Max(0, adjusted);
            return true;
        }
    }
}
