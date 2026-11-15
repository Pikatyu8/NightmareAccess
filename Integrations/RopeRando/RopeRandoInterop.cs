using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Modding;

namespace NightmareAccess.Interop
{
    /// <summary>Reflection-based bridge to RopeRando (soft dependency).</summary>
    internal static class RopeRandoInterop
    {
        private const string ModName = "RopeRando";

        private static bool _resolved;
        private static FieldInfo _settingsField;
        private static FieldInfo _enabledField;
        private static bool _failureLogged;

        internal static Func<bool> IsLoadedOverrideForTests { get; set; }
        internal static Func<bool?> EnabledOverrideForTests { get; set; }

        public static bool IsLoaded
        {
            get
            {
                if (IsLoadedOverrideForTests != null) return IsLoadedOverrideForTests();
                if (EnabledOverrideForTests != null) return false;
                if (NightmareAccess.Instance == null) return false;
                return IsLoadedViaModHooks();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsLoadedViaModHooks() => ModHooks.GetMod(ModName) != null;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object GetModInstance() => ModHooks.GetMod(ModName);

        public static bool IsEnabled()
        {
            if (EnabledOverrideForTests?.Invoke() is bool overridden) return overridden;
            if (!IsLoaded) return false;

            try
            {
                object mod = GetModInstance();
                if (mod == null) return false;

                if (!_resolved)
                {
                    _resolved = true;
                    _settingsField = mod.GetType().GetField("settings", BindingFlags.Public | BindingFlags.Instance);
                }

                object settings = _settingsField?.GetValue(mod);
                if (settings == null) return false;

                _enabledField ??= settings.GetType().GetField("Enabled", BindingFlags.Public | BindingFlags.Instance);
                return _enabledField?.GetValue(settings) is bool enabled && enabled;
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
                return false;
            }
        }

        private static void LogFailureOnce(Exception ex)
        {
            if (_failureLogged) return;
            _failureLogged = true;
            ModLog.Warn($"RopeRando reflection failed (the API may have changed): {ex.Message}");
        }
    }
}