namespace NightmareAccess.Integrations.RgbRando
{
    internal sealed class RgbRandoIntegration : ModIntegrationBase
    {
        public static RgbRandoIntegration Instance { get; } = new();

        public const string ModId = "RgbRando";

        private RgbRandoIntegration() { }

        public override string Id => ModId;

        public override string TryGetItemTypeGroup(string itemName)
        {
            if (itemName is "Color-Red" or "Color-Green" or "Color-Blue")
            {
                return "RGB";
            }
            return null;
        }
    }
}
