using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Modding;

namespace NightmareAccess.Interop
{
    /// <summary>Reflection-based bridge to CombatRandomizer (soft dependency). Reflection data is cached.</summary>
    internal static class CombatRandomizerInterop
    {
        private const string ModName = "CombatRandomizer";

        private static bool _gsPropResolved;
        private static PropertyInfo _gsProp;
        private static PropertyInfo _enabledProp;
        private static PropertyInfo _notchSettingsProp;
        private static FieldInfo _notchEnabledField;
        private static FieldInfo _notchFragSettingsField;
        private static FieldInfo _fragmentsPerNotchField;
        private static bool _failureLogged;

        internal static Func<bool> IsLoadedOverrideForTests { get; set; }
        internal static Func<int?> FragmentsPerNotchOverrideForTests { get; set; }

        public static bool IsLoaded
        {
            get
            {
                if (IsLoadedOverrideForTests != null) return IsLoadedOverrideForTests();
                if (FragmentsPerNotchOverrideForTests != null) return false;
                if (NightmareAccess.Instance == null) return false;
                return IsLoadedViaModHooks();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsLoadedViaModHooks() => ModHooks.GetMod(ModName) != null;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object GetModInstance() => ModHooks.GetMod(ModName);

        public static bool TryGetFragmentsPerNotch(out int fragmentsPerNotch)
        {
            fragmentsPerNotch = 0;

            if (FragmentsPerNotchOverrideForTests != null)
            {
                int? overridden = FragmentsPerNotchOverrideForTests();
                if (overridden.HasValue && overridden.Value > 0)
                {
                    fragmentsPerNotch = overridden.Value;
                    return true;
                }
                return false;
            }

            if (!IsLoaded) return false;

            try
            {
                object mod = GetModInstance();
                if (mod == null) return false;

                if (!_gsPropResolved)
                {
                    _gsPropResolved = true;
                    _gsProp = mod.GetType().GetProperty("GS", BindingFlags.Public | BindingFlags.Instance);
                }

                object gs = _gsProp?.GetValue(mod, null);
                if (gs == null) return false;

                _enabledProp ??= gs.GetType().GetProperty("Enabled", BindingFlags.Public | BindingFlags.Instance);
                if (_enabledProp?.GetValue(gs, null) is not bool enabled || !enabled) return false;

                _notchSettingsProp ??= gs.GetType().GetProperty("NotchSettings", BindingFlags.Public | BindingFlags.Instance);
                object notchSettings = _notchSettingsProp?.GetValue(gs, null);
                if (notchSettings == null) return false;

                _notchEnabledField ??= notchSettings.GetType().GetField("Enabled", BindingFlags.Public | BindingFlags.Instance);
                if (_notchEnabledField?.GetValue(notchSettings) is not bool notchEnabled || !notchEnabled) return false;

                _notchFragSettingsField ??= notchSettings.GetType().GetField("NotchFragmentSettings", BindingFlags.Public | BindingFlags.Instance);
                object fragSettings = _notchFragSettingsField?.GetValue(notchSettings);
                if (fragSettings == null) return false;

                _fragmentsPerNotchField ??= fragSettings.GetType().GetField("FragmentsPerNotch", BindingFlags.Public | BindingFlags.Instance);
                if (_fragmentsPerNotchField?.GetValue(fragSettings) is int count && count > 0)
                {
                    fragmentsPerNotch = count;
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
            }

            return false;
        }

        private static void LogFailureOnce(Exception ex)
        {
            if (_failureLogged) return;
            _failureLogged = true;
            ModLog.Warn($"CombatRandomizer reflection failed (the API may have changed): {ex.Message}");
        }
    }
}
