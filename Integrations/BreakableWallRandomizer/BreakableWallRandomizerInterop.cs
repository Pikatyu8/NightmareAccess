using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using ItemChanger;
using ItemChanger.Internal;
using ItemChanger.Placements;
using Modding;
using UnityEngine;

namespace NightmareAccess.Interop
{
    /// <summary>Reflection-based bridge to Breakable Wall Randomizer (soft dependency).</summary>
    internal static class BreakableWallRandomizerInterop
    {
        private const string ModName = "BreakableWallRandomizer";
        private const string WallLocationTypeFullName = "BreakableWallRandomizer.IC.BreakableWallLocation";

        private sealed class WallTarget
        {
            public string NormalizedName;
            public bool HasSlash;
            public string PlacementName;
        }

        private static bool _gsPropResolved;
        private static PropertyInfo _gsProp;
        private static readonly Dictionary<string, PropertyInfo> SettingsProps = new(StringComparer.Ordinal);
        private static PropertyInfo _wallEnabledProp;

        private static bool _wallFieldsResolved;
        private static FieldInfo _objectNameField;
        private static FieldInfo _fsmTypeField;
        private static FieldInfo _groupWallsField;
        private static FieldInfo _condWallNameField;
        private static FieldInfo _condWallSceneField;
        private static FieldInfo _condWallGameObjectField;
        private static FieldInfo _condWallFsmTypeField;

        private static object _cachedSaveSettings;
        private static readonly Dictionary<(string SceneName, string FsmName), List<WallTarget>> CachedWallTargets = new();

        private static bool _failureLogged;

        internal static Func<bool> IsLoadedOverrideForTests { get; set; }
        internal static Func<string, bool?> WallSettingOverrideForTests { get; set; }

        public static bool IsLoaded
        {
            get
            {
                if (IsLoadedOverrideForTests != null) return IsLoadedOverrideForTests();
                if (WallSettingOverrideForTests != null) return false;
                if (NightmareAccess.Instance == null) return false;
                return IsLoadedViaModHooks();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsLoadedViaModHooks() => ModHooks.GetMod(ModName) != null;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object GetModInstance() => ModHooks.GetMod(ModName);

        public static bool IsEnabled() => GetGlobalSettingBool("Enabled");
        public static bool IsGroupWallsEnabled() => GetGlobalSettingBool("GroupWalls");
        public static bool IsRockWallsEnabled() => GetWallCategoryEnabled("RockWalls");
        public static bool IsWoodenPlanksEnabled() => GetWallCategoryEnabled("WoodenPlanks");
        public static bool IsDiveFloorsEnabled() => GetWallCategoryEnabled("DiveFloors");
        public static bool IsCollapsersEnabled() => GetWallCategoryEnabled("Collapsers");

        public static bool TryGetPlacementName(PlayMakerFSM fsm, out string placementName)
        {
            placementName = null;
            if (fsm == null || NightmareAccess.Instance == null || !IsLoaded) return false;

            return TryGetPlacementNameFromSave(fsm, out placementName);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryGetPlacementNameFromSave(PlayMakerFSM fsm, out string placementName)
        {
            placementName = null;
            var saveSettings = Ref.Settings;
            if (saveSettings?.Placements == null) return false;

            if (!ReferenceEquals(_cachedSaveSettings, saveSettings))
            {
                RebuildWallTargets(saveSettings);
                _cachedSaveSettings = saveSettings;
            }

            if (!CachedWallTargets.TryGetValue((fsm.gameObject.scene.name, fsm.FsmName), out List<WallTarget> targets)
                || targets.Count == 0)
            {
                return false;
            }

            string goName = fsm.gameObject.name;
            string hierarchyPath = null;

            foreach (WallTarget target in targets)
            {
                if (!target.HasSlash)
                {
                    if (string.Equals(goName, target.NormalizedName, StringComparison.Ordinal))
                    {
                        placementName = target.PlacementName;
                        return true;
                    }
                }
                else
                {
                    hierarchyPath ??= GetHierarchyPath(fsm.gameObject);
                    if (string.Equals(hierarchyPath, target.NormalizedName, StringComparison.Ordinal)
                        || hierarchyPath.EndsWith("/" + target.NormalizedName, StringComparison.Ordinal))
                    {
                        placementName = target.PlacementName;
                        return true;
                    }
                }
            }

            return false;
        }

        private static void RebuildWallTargets(ItemChanger.Settings saveSettings)
        {
            CachedWallTargets.Clear();

            foreach (AbstractPlacement placement in saveSettings.Placements.Values)
            {
                if (placement is not IPrimaryLocationPlacement iplp || iplp.Location == null) continue;

                Type locType = iplp.Location.GetType();
                if (!string.Equals(locType.FullName, WallLocationTypeFullName, StringComparison.Ordinal)) continue;

                EnsureWallFieldsResolved(locType);

                if (_groupWallsField?.GetValue(iplp.Location) is IList groupWalls && groupWalls.Count > 0)
                {
                    foreach (object condWall in groupWalls)
                    {
                        if (condWall == null) continue;
                        EnsureCondensedFieldsResolved(condWall.GetType());

                        string wName = _condWallNameField?.GetValue(condWall) as string;
                        string wScene = _condWallSceneField?.GetValue(condWall) as string;
                        string wObj = _condWallGameObjectField?.GetValue(condWall) as string;
                        string wFsm = _condWallFsmTypeField?.GetValue(condWall) as string;

                        RegisterWallTarget(wName, wScene, wFsm, wObj, placement.Name);
                    }
                }
                else
                {
                    string objName = _objectNameField?.GetValue(iplp.Location) as string;
                    string fsmType = _fsmTypeField?.GetValue(iplp.Location) as string;
                    RegisterWallTarget(iplp.Location.name, iplp.Location.sceneName, fsmType, objName, placement.Name);
                }
            }
        }

        private static void RegisterWallTarget(
            string wallName,
            string sceneName,
            string fsmName,
            string rawObjectName,
            string placementName)
        {
            if (string.IsNullOrEmpty(sceneName)
                || string.IsNullOrEmpty(fsmName)
                || string.IsNullOrEmpty(rawObjectName)
                || string.IsNullOrEmpty(placementName))
            {
                return;
            }

            AddTargetEntry(sceneName, fsmName, rawObjectName, placementName);

            if (wallName == "Wall-Deepnest_Fungal")
            {
                AddTargetEntry(SceneNames.Fungus2_20, fsmName, "/Breakable Wall Waterways", placementName);
            }
            else if (wallName == "Plank-King's_Pass")
            {
                AddTargetEntry(sceneName, "collapse tute", "Collapser Tute 01", placementName);
            }
        }

        private static void AddTargetEntry(string sceneName, string fsmName, string rawObjectName, string placementName)
        {
            string normalized = rawObjectName.TrimStart('/');
            if (string.IsNullOrEmpty(normalized)) return;

            var key = (sceneName, fsmName);
            if (!CachedWallTargets.TryGetValue(key, out List<WallTarget> list))
            {
                list = new List<WallTarget>();
                CachedWallTargets[key] = list;
            }

            list.Add(new WallTarget
            {
                NormalizedName = normalized,
                HasSlash = normalized.IndexOf('/') >= 0,
                PlacementName = placementName
            });
        }

        private static void EnsureWallFieldsResolved(Type locType)
        {
            if (_wallFieldsResolved) return;
            _wallFieldsResolved = true;

            _objectNameField = locType.GetField("objectName", BindingFlags.Public | BindingFlags.Instance);
            _fsmTypeField = locType.GetField("fsmType", BindingFlags.Public | BindingFlags.Instance);
            _groupWallsField = locType.GetField("groupWalls", BindingFlags.Public | BindingFlags.Instance);
        }

        private static void EnsureCondensedFieldsResolved(Type condType)
        {
            if (_condWallNameField != null) return;

            _condWallNameField = condType.GetField("name", BindingFlags.Public | BindingFlags.Instance);
            _condWallSceneField = condType.GetField("sceneName", BindingFlags.Public | BindingFlags.Instance);
            _condWallGameObjectField = condType.GetField("gameObject", BindingFlags.Public | BindingFlags.Instance);
            _condWallFsmTypeField = condType.GetField("fsmType", BindingFlags.Public | BindingFlags.Instance);
        }

        private static string GetHierarchyPath(GameObject obj)
        {
            string path = obj.name;
            Transform current = obj.transform.parent;
            while (current != null)
            {
                path = $"{current.name}/{path}";
                current = current.parent;
            }
            return path;
        }

        private static bool GetGlobalSettingBool(string propName)
        {
            if (WallSettingOverrideForTests?.Invoke(propName) is bool overridden) return overridden;
            if (!IsLoaded) return false;

            try
            {
                object gs = GetGlobalSettings();
                if (gs == null) return false;

                if (!GetCachedBoolProperty(gs, "Enabled")) return false;
                if (propName == "Enabled") return true;

                return GetCachedBoolProperty(gs, propName);
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
                return false;
            }
        }

        private static bool GetWallCategoryEnabled(string categoryPropName)
        {
            if (WallSettingOverrideForTests?.Invoke(categoryPropName) is bool overridden) return overridden;
            if (!IsLoaded) return false;

            try
            {
                object gs = GetGlobalSettings();
                if (gs == null || !GetCachedBoolProperty(gs, "Enabled")) return false;

                object wallSettings = GetCachedPropertyValue(gs, "Settings", categoryPropName);
                if (wallSettings == null) return false;

                _wallEnabledProp ??= wallSettings.GetType().GetProperty("Enabled", BindingFlags.Public | BindingFlags.Instance);
                return _wallEnabledProp?.GetValue(wallSettings, null) is bool enabled && enabled;
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
                return false;
            }
        }

        private static object GetGlobalSettings()
        {
            object mod = GetModInstance();
            if (mod == null) return null;

            if (!_gsPropResolved)
            {
                _gsPropResolved = true;
                _gsProp = mod.GetType().GetProperty("GS", BindingFlags.Public | BindingFlags.Instance);
            }

            return _gsProp?.GetValue(mod, null);
        }

        private static object GetCachedPropertyValue(object instance, string _, string propName)
        {
            if (!SettingsProps.TryGetValue(propName, out PropertyInfo prop))
            {
                prop = instance.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                SettingsProps[propName] = prop;
            }
            return prop?.GetValue(instance, null);
        }

        private static bool GetCachedBoolProperty(object instance, string propName) =>
            GetCachedPropertyValue(instance, null, propName) is bool b && b;

        private static void LogFailureOnce(Exception ex)
        {
            if (_failureLogged) return;
            _failureLogged = true;
            ModLog.Warn($"BreakableWallRandomizer reflection failed (the API may have changed): {ex.Message}");
        }
    }
}