using System;
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
    /// <summary>Reflection-based bridge to LoreRandomizer and LoreCore (soft dependencies). Reflection data is cached.</summary>
    internal static class LoreRandomizerInterop
    {
        private const string ModName = "LoreRandomizer";

        internal enum LoreTargetKind
        {
            Inspect,
            DreamNail,
            Dialogue,
            StagEgg
        }

        internal sealed class LoreTarget
        {
            public LoreTargetKind Kind;
            public string NormalizedObjectName;
            public bool HasSlash;
            public string PlacementName;
        }

        internal sealed class LoreConfig
        {
            public bool Enabled;
            public bool RandomizeNpc;
            public bool RandomizeDreamNailDialogue;
            public bool RandomizePointOfInterest;
            public bool RandomizeTravellerDialogues;
            public bool RandomizeShrineOfBelievers;
        }

        private static bool _settingsPropResolved;
        private static PropertyInfo _settingsProp;
        private static readonly Dictionary<string, PropertyInfo> SettingsProps = new(StringComparer.Ordinal);

        private static object _cachedSaveSettings;
        private static readonly Dictionary<(string SceneName, string FsmName), List<LoreTarget>> CachedTargets = new();
        private static readonly Dictionary<Type, PropertyInfo> ObjectNameProps = new();
        private static readonly Dictionary<Type, PropertyInfo> GameObjectNameProps = new();
        private static readonly Dictionary<Type, PropertyInfo> FsmNameProps = new();

        private static bool _failureLogged;

        internal static Func<bool> IsLoadedOverrideForTests { get; set; }
        internal static Func<LoreConfig> ConfigOverrideForTests { get; set; }

        public static bool IsLoaded
        {
            get
            {
                if (IsLoadedOverrideForTests != null) return IsLoadedOverrideForTests();
                if (ConfigOverrideForTests != null) return false;
                if (NightmareAccess.Instance == null) return false;
                return IsLoadedViaModHooks();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsLoadedViaModHooks() => ModHooks.GetMod(ModName) != null;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object GetModInstance() => ModHooks.GetMod(ModName);

        public static LoreConfig GetConfig()
        {
            if (ConfigOverrideForTests != null) return ConfigOverrideForTests();
            if (!IsLoaded) return null;

            try
            {
                object mod = GetModInstance();
                if (mod == null) return null;

                if (!_settingsPropResolved)
                {
                    _settingsPropResolved = true;
                    _settingsProp = mod.GetType().GetProperty("Settings", BindingFlags.Public | BindingFlags.Instance);
                }

                object rs = _settingsProp?.GetValue(mod, null);
                if (rs == null || !GetBool(rs, "Enabled")) return null;

                return new LoreConfig
                {
                    Enabled = true,
                    RandomizeNpc = GetBool(rs, "RandomizeNpc"),
                    RandomizeDreamNailDialogue = GetBool(rs, "RandomizeDreamNailDialogue"),
                    RandomizePointOfInterest = GetBool(rs, "RandomizePointOfInterest"),
                    RandomizeTravellerDialogues = GetBool(rs, "RandomizeTravellerDialogues"),
                    RandomizeShrineOfBelievers = GetBool(rs, "RandomizeShrineOfBelievers")
                };
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
                return null;
            }
        }

        public static bool TryGetTarget(PlayMakerFSM fsm, out LoreTarget matched)
        {
            matched = null;
            if (fsm == null || NightmareAccess.Instance == null) return false;

            return TryGetTargetFromSave(fsm, out matched);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryGetTargetFromSave(PlayMakerFSM fsm, out LoreTarget matched)
        {
            matched = null;
            var saveSettings = Ref.Settings;
            if (saveSettings?.Placements == null) return false;

            if (!ReferenceEquals(_cachedSaveSettings, saveSettings))
            {
                RebuildTargets(saveSettings);
                _cachedSaveSettings = saveSettings;
            }

            if (!CachedTargets.TryGetValue((fsm.gameObject.scene.name, fsm.FsmName), out List<LoreTarget> targets)
                || targets.Count == 0)
            {
                return false;
            }

            string goName = fsm.gameObject.name;
            string hierarchyPath = null;
            LoreTarget wildcardFallback = null;

            foreach (LoreTarget target in targets)
            {
                if (string.IsNullOrEmpty(target.NormalizedObjectName))
                {
                    wildcardFallback ??= target;
                    continue;
                }

                if (!target.HasSlash)
                {
                    if (string.Equals(goName, target.NormalizedObjectName, StringComparison.Ordinal))
                    {
                        matched = target;
                        return true;
                    }
                }
                else
                {
                    hierarchyPath ??= GetHierarchyPath(fsm.gameObject);
                    if (string.Equals(hierarchyPath, target.NormalizedObjectName, StringComparison.Ordinal)
                        || hierarchyPath.EndsWith("/" + target.NormalizedObjectName, StringComparison.Ordinal))
                    {
                        matched = target;
                        return true;
                    }
                }
            }

            if (wildcardFallback != null)
            {
                matched = wildcardFallback;
                return true;
            }

            return false;
        }

        private static void RebuildTargets(ItemChanger.Settings saveSettings)
        {
            CachedTargets.Clear();

            foreach (AbstractPlacement placement in saveSettings.Placements.Values)
            {
                if (placement is not IPrimaryLocationPlacement iplp || iplp.Location == null) continue;

                Type locType = iplp.Location.GetType();
                string fullName = locType.FullName;
                if (string.IsNullOrEmpty(fullName)
                    || !fullName.StartsWith("LoreCore.Locations.", StringComparison.Ordinal))
                {
                    continue;
                }

                string sceneName = iplp.Location.sceneName;
                if (string.IsNullOrEmpty(sceneName)) continue;

                if (locType.Name == "InspectLocation")
                {
                    string rawObj = GetCachedStringProp(iplp.Location, locType, "GameObjectName", GameObjectNameProps);
                    string objName = string.IsNullOrEmpty(rawObj) ? iplp.Location.name : rawObj;
                    AddTarget(sceneName, "inspect_region", LoreTargetKind.Inspect, objName, placement.Name);
                }
                else if (locType.Name == "StagEggLocation")
                {
                    AddTarget(sceneName, "Conversation Control", LoreTargetKind.StagEgg, "Eggs Inspect", placement.Name);
                }
                else if (HasProperty(locType, "GameObjectName", GameObjectNameProps))
                {
                    string rawObj = GetCachedStringProp(iplp.Location, locType, "GameObjectName", GameObjectNameProps);
                    string objName = string.IsNullOrEmpty(rawObj) ? "Dream Dialogue" : rawObj;
                    AddTarget(sceneName, "npc_dream_dialogue", LoreTargetKind.DreamNail, objName, placement.Name);
                }
                else if (HasProperty(locType, "ObjectName", ObjectNameProps))
                {
                    string rawObj = GetCachedStringProp(iplp.Location, locType, "ObjectName", ObjectNameProps);
                    string fsmName = GetCachedStringProp(iplp.Location, locType, "FsmName", FsmNameProps);
                    if (string.IsNullOrEmpty(fsmName)) fsmName = "Conversation Control";
                    AddTarget(sceneName, fsmName, LoreTargetKind.Dialogue, rawObj, placement.Name);
                }
            }
        }

        private static void AddTarget(
            string sceneName,
            string fsmName,
            LoreTargetKind kind,
            string rawObjectName,
            string placementName)
        {
            if (string.IsNullOrEmpty(sceneName) || string.IsNullOrEmpty(fsmName) || string.IsNullOrEmpty(placementName))
            {
                return;
            }

            string normalized = rawObjectName?.TrimStart('/');
            var key = (sceneName, fsmName);
            if (!CachedTargets.TryGetValue(key, out List<LoreTarget> list))
            {
                list = new List<LoreTarget>();
                CachedTargets[key] = list;
            }

            list.Add(new LoreTarget
            {
                Kind = kind,
                NormalizedObjectName = normalized,
                HasSlash = !string.IsNullOrEmpty(normalized) && normalized.IndexOf('/') >= 0,
                PlacementName = placementName
            });
        }

        private static bool HasProperty(Type type, string propName, Dictionary<Type, PropertyInfo> cache)
        {
            if (!cache.TryGetValue(type, out PropertyInfo prop))
            {
                prop = type.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                cache[type] = prop;
            }
            return prop != null;
        }

        private static string GetCachedStringProp(
            object instance, Type type, string propName, Dictionary<Type, PropertyInfo> cache)
        {
            if (!cache.TryGetValue(type, out PropertyInfo prop))
            {
                prop = type.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                cache[type] = prop;
            }
            return prop?.GetValue(instance, null) as string;
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

        private static bool GetBool(object instance, string propName)
        {
            if (!SettingsProps.TryGetValue(propName, out PropertyInfo prop))
            {
                prop = instance.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                SettingsProps[propName] = prop;
            }
            return prop?.GetValue(instance, null) is bool b && b;
        }

        private static void LogFailureOnce(Exception ex)
        {
            if (_failureLogged) return;
            _failureLogged = true;
            ModLog.Warn($"LoreRandomizer reflection failed (the API may have changed): {ex.Message}");
        }
    }
}