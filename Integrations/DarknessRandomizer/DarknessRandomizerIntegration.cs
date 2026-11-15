using NightmareAccess.Logic;

namespace NightmareAccess.Integrations.DarknessRandomizer
{
    internal sealed class DarknessRandomizerIntegration : ModIntegrationBase
    {
        public static DarknessRandomizerIntegration Instance { get; } = new();

        public const string ModId = "DarknessRandomizer";

        private DarknessRandomizerIntegration() { }

        public override string Id => ModId;

        public override string TryGetItemTypeGroup(string itemName)
        {
            if (IsLanternShard(itemName))
            {
                return "Lumafly_Lantern";
            }
            return null;
        }

        public override bool IsItemSelfConflicting(string itemName, AccessConditionType conditionType, bool isGroupMode)
        {
            return conditionType == AccessConditionType.LumaflyLantern && IsLanternShard(itemName);
        }

        private static bool IsLanternShard(string itemName) => itemName is
            "Lantern_Shard" or "Final_Lantern_Shard" or "No_Lantern_Shard" or "Final_No_Lantern_Shard";
    }
}
