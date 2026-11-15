using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using ItemChanger.Internal;
using Modding;

namespace NightmareAccess.Interop
{
    /// <summary>Reflection-based bridge to GodhomeRandomizer (soft dependency). Reflection data is cached.</summary>
    internal static class GodhomeRandomizerInterop
    {
        private const string ModName = "GodhomeRandomizer";

        internal sealed class GodhomeConfig
        {
            public bool Enabled;
            public int RandomizeTiers;
            public bool IncludeUnlockLocations;
            public bool RandomizeOrdeal;
            public int PantheonsIncluded;
            public bool Lifeblood;
            public bool Completion;
            public bool Nail;
            public bool Shell;
            public bool Charms;
            public bool Soul;
            public bool AllAtOnce;
            public bool Hitless;
        }

        private static bool _gsPropResolved;
        private static PropertyInfo _gsProp;
        private static readonly Dictionary<string, PropertyInfo> RootProps = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, PropertyInfo> HogProps = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, PropertyInfo> PantheonProps = new(StringComparer.Ordinal);
        private static bool _failureLogged;

        internal static Func<bool> IsLoadedOverrideForTests { get; set; }
        internal static Func<GodhomeConfig> ConfigOverrideForTests { get; set; }

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

        public static GodhomeConfig GetConfig()
        {
            if (ConfigOverrideForTests != null) return ConfigOverrideForTests();
            if (!IsLoaded) return null;

            try
            {
                object mod = GetModInstance();
                if (mod == null) return null;

                if (!_gsPropResolved)
                {
                    _gsPropResolved = true;
                    _gsProp = mod.GetType().GetProperty("GS", BindingFlags.Public | BindingFlags.Instance);
                }

                object gs = _gsProp?.GetValue(mod, null);
                if (gs == null || !GetBool(gs, "Enabled", RootProps)) return null;

                object hog = GetPropValue(gs, "HallOfGods", RootProps);
                object pantheons = GetPropValue(gs, "Pantheons", RootProps);

                return new GodhomeConfig
                {
                    Enabled = true,
                    RandomizeTiers = hog != null ? GetEnumInt(hog, "RandomizeTiers", HogProps) : 0,
                    IncludeUnlockLocations = hog != null && GetBool(hog, "IncludeUnlockLocations", HogProps),
                    RandomizeOrdeal = hog != null && GetBool(hog, "RandomizeOrdeal", HogProps),
                    PantheonsIncluded = pantheons != null ? GetEnumInt(pantheons, "PantheonsIncluded", PantheonProps) : 0,
                    Lifeblood = pantheons != null && GetBool(pantheons, "Lifeblood", PantheonProps),
                    Completion = pantheons != null && GetBool(pantheons, "Completion", PantheonProps),
                    Nail = pantheons != null && GetBool(pantheons, "Nail", PantheonProps),
                    Shell = pantheons != null && GetBool(pantheons, "Shell", PantheonProps),
                    Charms = pantheons != null && GetBool(pantheons, "Charms", PantheonProps),
                    Soul = pantheons != null && GetBool(pantheons, "Soul", PantheonProps),
                    AllAtOnce = pantheons != null && GetBool(pantheons, "AllAtOnce", PantheonProps),
                    Hitless = pantheons != null && GetBool(pantheons, "Hitless", PantheonProps)
                };
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
                return null;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool IsPlacementActiveInSave(string placementName)
        {
            if (NightmareAccess.Instance == null || string.IsNullOrEmpty(placementName)) return false;
            var placements = Ref.Settings?.Placements;
            return placements != null && placements.ContainsKey(placementName);
        }

        private static object GetPropValue(object instance, string propName, Dictionary<string, PropertyInfo> cache)
        {
            if (!cache.TryGetValue(propName, out PropertyInfo prop))
            {
                prop = instance.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                cache[propName] = prop;
            }
            return prop?.GetValue(instance, null);
        }

        private static bool GetBool(object instance, string propName, Dictionary<string, PropertyInfo> cache) =>
            GetPropValue(instance, propName, cache) is bool b && b;

        private static int GetEnumInt(object instance, string propName, Dictionary<string, PropertyInfo> cache)
        {
            object val = GetPropValue(instance, propName, cache);
            return val != null ? Convert.ToInt32(val) : 0;
        }

        private static void LogFailureOnce(Exception ex)
        {
            if (_failureLogged) return;
            _failureLogged = true;
            ModLog.Warn($"GodhomeRandomizer reflection failed (the API may have changed): {ex.Message}");
        }
    }
}