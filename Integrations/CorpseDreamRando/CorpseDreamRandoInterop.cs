using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using ItemChanger.Internal;
using Modding;

namespace NightmareAccess.Interop
{
    /// <summary>Reflection-based bridge to CorpseDreamRando (soft dependency).</summary>
    internal static class CorpseDreamRandoInterop
    {
        private const string ModName = "CorpseDreamRando";
        private const string MainTypeName = "CorpseDreamRando.CorpseDreamRando, CorpseDreamRando";
        private const string CorpseCoordsTypeName = "CorpseDreamRando.CorpseCoords, CorpseDreamRando";

        private static bool _settingsResolved;
        private static PropertyInfo _settingsProp;
        private static FieldInfo _enabledField;

        private static bool _coordsResolved;
        private static FieldInfo _nameToPlacementField;

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

        public static bool IsEnabled()
        {
            if (EnabledOverrideForTests?.Invoke() is bool overridden) return overridden;
            if (!IsLoaded) return false;

            try
            {
                if (!_settingsResolved)
                {
                    _settingsResolved = true;
                    _settingsProp = Type.GetType(MainTypeName)?.GetProperty("Settings", BindingFlags.Public | BindingFlags.Static);
                }

                object settings = _settingsProp?.GetValue(null, null);
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

        public static bool TryGetPlacementName(string sceneName, string objectPath, out string placementName)
        {
            placementName = null;
            if (!IsLoaded || string.IsNullOrEmpty(sceneName) || string.IsNullOrEmpty(objectPath))
            {
                return false;
            }

            try
            {
                if (!_coordsResolved)
                {
                    _coordsResolved = true;
                    _nameToPlacementField = Type.GetType(CorpseCoordsTypeName)
                        ?.GetField("nameToPlacement", BindingFlags.Public | BindingFlags.Static);
                }

                if (_nameToPlacementField?.GetValue(null) is Dictionary<(string, string), string> map
                    && map.TryGetValue((sceneName, objectPath), out string found))
                {
                    placementName = found;
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool IsPlacementActiveInSave(string placementName)
        {
            if (NightmareAccess.Instance == null || string.IsNullOrEmpty(placementName)) return false;
            var placements = Ref.Settings?.Placements;
            return placements != null && placements.ContainsKey(placementName);
        }

        private static void LogFailureOnce(Exception ex)
        {
            if (_failureLogged) return;
            _failureLogged = true;
            ModLog.Warn($"CorpseDreamRando reflection failed (the API may have changed): {ex.Message}");
        }
    }
}