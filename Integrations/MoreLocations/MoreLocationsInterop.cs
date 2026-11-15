using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Modding;

namespace NightmareAccess.Interop
{
    /// <summary>Reflection-based bridge to MoreLocations (soft dependency).</summary>
    internal static class MoreLocationsInterop
    {
        private const string ModName = "MoreLocations";

        private static bool _resolved;
        private static MethodInfo _onSaveGlobalMethod;
        private static PropertyInfo _rsProp;
        private static PropertyInfo _enabledProp;
        private static PropertyInfo _miscProp;
        private static FieldInfo _swimField;
        private static FieldInfo _stagNestEggField;
        private static bool _failureLogged;

        internal static Func<bool> IsLoadedOverrideForTests { get; set; }
        internal static Func<IEnumerable<string>> MiscOverrideForTests { get; set; }

        public static bool IsLoaded
        {
            get
            {
                if (IsLoadedOverrideForTests != null) return IsLoadedOverrideForTests();
                if (MiscOverrideForTests != null) return false;
                if (NightmareAccess.Instance == null) return false;
                return IsLoadedViaModHooks();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsLoadedViaModHooks() => ModHooks.GetMod(ModName) != null;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object GetModInstance() => ModHooks.GetMod(ModName);

        public static IEnumerable<string> GetEnabledMiscLocations()
        {
            if (MiscOverrideForTests != null)
            {
                foreach (string name in MiscOverrideForTests())
                {
                    if (!string.IsNullOrEmpty(name)) yield return name;
                }
                yield break;
            }

            if (!IsLoaded) yield break;

            List<string> results = new();
            try
            {
                object mod = GetModInstance();
                if (mod == null) yield break;

                if (!_resolved)
                {
                    _resolved = true;
                    _onSaveGlobalMethod = mod.GetType().GetMethod("OnSaveGlobal", BindingFlags.Public | BindingFlags.Instance);
                }

                object gs = _onSaveGlobalMethod?.Invoke(mod, null);
                if (gs == null) yield break;

                _rsProp ??= gs.GetType().GetProperty("RS", BindingFlags.Public | BindingFlags.Instance);
                object rs = _rsProp?.GetValue(gs, null);
                if (rs == null) yield break;

                _enabledProp ??= rs.GetType().GetProperty("Enabled", BindingFlags.Public | BindingFlags.Instance);
                if (_enabledProp?.GetValue(rs, null) is not bool enabled || !enabled) yield break;

                _miscProp ??= rs.GetType().GetProperty("MiscLocationSettings", BindingFlags.Public | BindingFlags.Instance);
                object misc = _miscProp?.GetValue(rs, null);
                if (misc == null) yield break;

                Type miscType = misc.GetType();
                _swimField ??= miscType.GetField("Swim", BindingFlags.Public | BindingFlags.Instance);
                _stagNestEggField ??= miscType.GetField("StagNestEgg", BindingFlags.Public | BindingFlags.Instance);

                if (_swimField?.GetValue(misc) is bool swim && swim) results.Add("Swim");
                if (_stagNestEggField?.GetValue(misc) is bool egg && egg) results.Add("Stag_Nest_Egg");
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
                yield break;
            }

            foreach (string loc in results)
            {
                yield return loc;
            }
        }

        private static void LogFailureOnce(Exception ex)
        {
            if (_failureLogged) return;
            _failureLogged = true;
            ModLog.Warn($"MoreLocations reflection failed: {ex.Message}");
        }
    }
}
