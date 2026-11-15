using System.Runtime.CompilerServices;
using Modding;
using NightmareAccess.Settings;
using RandoSettingsManager;
using RandoSettingsManager.SettingsManagement;
using RandoSettingsManager.SettingsManagement.Versioning;

namespace NightmareAccess.Interop
{
    internal static class RSM_Interop
    {
        public static void Hook()
        {
            if (ModHooks.GetMod("RandoSettingsManager") != null)
            {
                RegisterProxy();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RegisterProxy()
        {
            RandoSettingsManagerMod.Instance.RegisterConnection(new NightmareAccessRSMProxy());
        }
    }

    internal class NightmareAccessRSMProxy : RandoSettingsProxy<NightmareAccessSettings, string>
    {
        public override string ModKey => "Nightmare Access";
        public override VersioningPolicy<string> VersioningPolicy { get; }
            = new EqualityVersioningPolicy<string>(NightmareAccess.Instance.GetVersion());

        public override void ReceiveSettings(NightmareAccessSettings settings)
        {
            // Copy into the existing instance: the connection menu holds references to it
            NightmareAccess.Settings.CopyFrom(settings ?? new NightmareAccessSettings());
            ConnectionMenu.RefreshMenu();
        }

        public override bool TryProvideSettings(out NightmareAccessSettings settings)
        {
            settings = NightmareAccess.Settings;
            return settings.Enabled;
        }
    }
}