using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Modding;
using UnityEngine;

namespace NightmareAccess.Interop
{
    /// <summary>Reflection-based bridge to YetAnotherRandoConnection (soft dependency). Reflection data is cached.</summary>
    internal static class YarcInterop
    {
        private const string ModName = "YetAnotherRandoConnection";
        private const string MainType = "YetAnotherRandoConnection.YetAnotherRandoConnection, YetAnotherRandoConnection";
        private const string EggBombCoordsType = "YetAnotherRandoConnection.EggBombCoords, YetAnotherRandoConnection";
        private const string VineCoordsType = "YetAnotherRandoConnection.VineCoords, YetAnotherRandoConnection";
        private const string HivePlatCoordsType = "YetAnotherRandoConnection.HivePlatCoords, YetAnotherRandoConnection";
        private const string StalactiteCoordsType = "YetAnotherRandoConnection.StalactiteCoords, YetAnotherRandoConnection";

        private static bool _settingsResolved;
        private static PropertyInfo _settingsProp;
        private static readonly Dictionary<string, FieldInfo> PoolFields = new(StringComparer.Ordinal);

        private static bool _eggBombCoordsResolved;
        private static FieldInfo _eggBombCoordsField;

        private static bool _vineCoordsResolved;
        private static FieldInfo _vineCoordsField;

        private static bool _hivePlatCoordsResolved;
        private static FieldInfo _hivePlatCoordsField;

        private static bool _stalactiteCoordsResolved;
        private static FieldInfo _stalactiteCoordsField;

        private static bool _failureLogged;

        internal static Func<bool> IsLoadedOverrideForTests { get; set; }
        internal static Func<bool> DreamOrbsOverrideForTests { get; set; }
        internal static Func<string, bool?> PoolOverrideForTests { get; set; }

        public static bool IsLoaded
        {
            get
            {
                if (IsLoadedOverrideForTests != null) return IsLoadedOverrideForTests();
                if (DreamOrbsOverrideForTests != null || PoolOverrideForTests != null) return false;
                if (NightmareAccess.Instance == null) return false;
                return IsLoadedViaModHooks();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsLoadedViaModHooks() => ModHooks.GetMod(ModName) != null;

        public static bool IsDreamOrbsRandomized() => IsPoolEnabled("DreamOrbs");
        public static bool IsVinesRandomized() => IsPoolEnabled("Vines");
        public static bool IsSoulJarsRandomized() => IsPoolEnabled("SoulJars");
        public static bool IsHivePlatformsRandomized() => IsPoolEnabled("HivePlatforms");
        public static bool IsEggBombsRandomized() => IsPoolEnabled("JellyEggBombs");
        public static bool IsTelescopeRandomized() => IsPoolEnabled("Telescope");
        public static bool IsScarecrowRandomized() => IsPoolEnabled("Scarecrow");
        public static bool IsStalactitesRandomized() => IsPoolEnabled("Stalactites");
        public static bool IsLoveJarsRandomized() => IsPoolEnabled("LoveJars");

        private static bool IsPoolEnabled(string fieldName)
        {
            if (PoolOverrideForTests?.Invoke(fieldName) is bool overridden) return overridden;
            if (fieldName == "DreamOrbs" && DreamOrbsOverrideForTests != null) return DreamOrbsOverrideForTests();
            if (!IsLoaded) return false;

            try
            {
                if (!_settingsResolved)
                {
                    _settingsResolved = true;
                    _settingsProp = Type.GetType(MainType)?.GetProperty("Settings", BindingFlags.Public | BindingFlags.Static);
                }

                object settings = _settingsProp?.GetValue(null);
                if (settings == null) return false;

                if (!PoolFields.TryGetValue(fieldName, out FieldInfo field))
                {
                    field = settings.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
                    PoolFields[fieldName] = field;
                }

                return field?.GetValue(settings) is bool enabled && enabled;
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
                return false;
            }
        }

        public static bool TryGetEggBombPosition(string locationName, out string scene, out Vector2 position)
        {
            scene = null;
            position = Vector2.zero;

            try
            {
                if (!_eggBombCoordsResolved)
                {
                    _eggBombCoordsResolved = true;
                    _eggBombCoordsField = Type.GetType(EggBombCoordsType)?.GetField("placementToPosition", BindingFlags.Public | BindingFlags.Static);
                }

                if (_eggBombCoordsField?.GetValue(null) is Dictionary<string, (string, Vector2)> map
                    && map.TryGetValue(locationName, out var entry))
                {
                    scene = entry.Item1;
                    position = entry.Item2;
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
            }

            return false;
        }

        public static bool TryGetVinePlacementName(string sceneAndParentKey, out string placementName)
        {
            placementName = null;

            try
            {
                if (!_vineCoordsResolved)
                {
                    _vineCoordsResolved = true;
                    _vineCoordsField = Type.GetType(VineCoordsType)?.GetField("nameToPlacement", BindingFlags.Public | BindingFlags.Static);
                }

                if (_vineCoordsField?.GetValue(null) is Dictionary<string, string> map
                    && map.TryGetValue(sceneAndParentKey, out string found))
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

        public static bool TryGetHivePlatPlacementName(string sceneAndObjKey, out string placementName)
        {
            placementName = null;

            try
            {
                if (!_hivePlatCoordsResolved)
                {
                    _hivePlatCoordsResolved = true;
                    _hivePlatCoordsField = Type.GetType(HivePlatCoordsType)?.GetField("nameToPlacement", BindingFlags.Public | BindingFlags.Static);
                }

                if (_hivePlatCoordsField?.GetValue(null) is Dictionary<string, string> map
                    && map.TryGetValue(sceneAndObjKey, out string found))
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

        public static bool TryGetStalactitePlacementName(string sceneName, string objectName, out string placementName)
        {
            placementName = null;

            try
            {
                if (!_stalactiteCoordsResolved)
                {
                    _stalactiteCoordsResolved = true;
                    _stalactiteCoordsField = Type.GetType(StalactiteCoordsType)?.GetField("nameToPlacement", BindingFlags.Public | BindingFlags.Static);
                }

                if (_stalactiteCoordsField?.GetValue(null) is Dictionary<(string, string), string> map
                    && map.TryGetValue((sceneName, objectName), out string found))
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

        private static void LogFailureOnce(Exception ex)
        {
            if (_failureLogged) return;
            _failureLogged = true;
            ModLog.Warn($"YARC reflection failed (the API may have changed): {ex.Message}");
        }
    }
}