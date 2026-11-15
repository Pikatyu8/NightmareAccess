using System;

namespace NightmareAccess.Integrations.AlphabetRando
{
    internal sealed class AlphabetRandoIntegration : ModIntegrationBase
    {
        public static AlphabetRandoIntegration Instance { get; } = new();

        public const string ModId = "AlphabetRando";
        public const string ItemPrefix = "Alphabet-";

        private AlphabetRandoIntegration() { }

        public override string Id => ModId;

        public override string TryGetItemTypeGroup(string itemName)
        {
            if (!string.IsNullOrEmpty(itemName) && itemName.StartsWith(ItemPrefix, StringComparison.Ordinal))
            {
                return "Alphabet";
            }
            return null;
        }
    }
}
