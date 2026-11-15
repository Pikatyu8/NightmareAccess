using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using ItemChanger.Internal;
using Modding;

namespace NightmareAccess.Interop
{
    /// <summary>Reflection-based bridge to FlowerRandomizer (soft dependency). Reflection data is cached.</summary>
    internal static class FlowerRandomizerInterop
    {
        private const string ModName = "FlowerRandomizer";

        private static readonly (string LocationName, string PropertyName, bool IsVanilla)[] QuestMappings =
        {
            ("Flower_Quest-Elderbug", "Elderbug", true),
            ("Flower_Quest-Oro", "Oro", true),
            ("Flower_Quest-Godseeker", "Godseeker", true),
            ("Flower_Quest-Emilitia", "Emilitia", true),
            ("Flower_Quest-White_Lady", "WhiteLady", true),
            ("Flower_Quest-Mato", "Mato", false),
            ("Flower_Quest-Sheo", "Sheo", false),
            ("Flower_Quest-Marissa", "Marissa", false),
            ("Flower_Quest-Midwife", "Midwife", false),
            ("Flower_Quest-Isma", "Isma", false),
            ("Flower_Quest-Radiance", "Radiance", false),
            ("Flower_Quest-Pale_King", "PaleKing", false),
            ("Flower_Quest-Pain", "Pain", false)
        };

        private static bool _gsPropResolved;
        private static PropertyInfo _gsProp;
        private static readonly Dictionary<string, PropertyInfo> RootProps = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, PropertyInfo> VanillaProps = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, PropertyInfo> CustomProps = new(StringComparer.Ordinal);
        private static bool _failureLogged;

        internal static Func<bool> IsLoadedOverrideForTests { get; set; }
        internal static Func<bool> EnabledOverrideForTests { get; set; }
        internal static Func<string, bool> QuestEnabledOverrideForTests { get; set; }

        public static bool IsLoaded
        {
            get
            {
                if (IsLoadedOverrideForTests != null) return IsLoadedOverrideForTests();
                if (EnabledOverrideForTests != null || QuestEnabledOverrideForTests != null) return false;
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
            if (EnabledOverrideForTests != null) return EnabledOverrideForTests();
            if (!IsLoaded) return false;

            try
            {
                object gs = GetGlobalSettings();
                if (gs == null) return false;

                return GetCachedBoolProperty(gs, "Enabled", RootProps);
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
                return false;
            }
        }

        public static IEnumerable<string> GetEnabledQuestLocations()
        {
            if (EnabledOverrideForTests != null || QuestEnabledOverrideForTests != null)
            {
                if (EnabledOverrideForTests != null && !EnabledOverrideForTests()) yield break;

                foreach (var entry in QuestMappings)
                {
                    if (QuestEnabledOverrideForTests == null || QuestEnabledOverrideForTests(entry.LocationName))
                    {
                        yield return entry.LocationName;
                    }
                }
                yield break;
            }

            if (!IsLoaded) yield break;

            List<string> results = new();
            try
            {
                object gs = GetGlobalSettings();
                if (gs == null || !GetCachedBoolProperty(gs, "Enabled", RootProps)) yield break;

                object vanillaNpcs = GetCachedPropertyValue(gs, "VanillaNPCs", RootProps);
                object customNpcs = GetCachedPropertyValue(gs, "CustomNPCs", RootProps);

                foreach (var entry in QuestMappings)
                {
                    object holder = entry.IsVanilla ? vanillaNpcs : customNpcs;
                    if (holder == null) continue;

                    Dictionary<string, PropertyInfo> cache = entry.IsVanilla ? VanillaProps : CustomProps;
                    if (GetCachedBoolProperty(holder, entry.PropertyName, cache))
                    {
                        results.Add(entry.LocationName);
                    }
                }
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
                yield break;
            }

            foreach (string locName in results)
            {
                yield return locName;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool IsPlacementActiveInSave(string placementName)
        {
            if (NightmareAccess.Instance == null || string.IsNullOrEmpty(placementName)) return false;
            var placements = Ref.Settings?.Placements;
            return placements != null && placements.ContainsKey(placementName);
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

        private static object GetCachedPropertyValue(
            object instance, string propName, Dictionary<string, PropertyInfo> cache)
        {
            if (!cache.TryGetValue(propName, out PropertyInfo prop))
            {
                prop = instance.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                cache[propName] = prop;
            }
            return prop?.GetValue(instance, null);
        }

        private static bool GetCachedBoolProperty(
            object instance, string propName, Dictionary<string, PropertyInfo> cache) =>
            GetCachedPropertyValue(instance, propName, cache) is bool b && b;

        private static void LogFailureOnce(Exception ex)
        {
            if (_failureLogged) return;
            _failureLogged = true;
            ModLog.Warn($"FlowerRandomizer reflection failed (the API may have changed): {ex.Message}");
        }
    }
}