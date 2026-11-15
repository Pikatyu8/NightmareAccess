using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ItemChanger;
using ItemChanger.Internal;
using Modding;

namespace NightmareAccess.Interop
{
    /// <summary>Reflection-based bridge to RandoPlus (soft dependency).</summary>
    internal static class RandoPlusInterop
    {
        private const string ModName = "RandoPlus";
        private const string MainTypeName = "RandoPlus.RandoPlus, RandoPlus";
        private const string AreaRestrictionTypeName = "RandoPlus.AreaRestriction.AreaRestriction, RandoPlus";
        private const string NailsmithModuleTypeName = "RandoPlus.NailUpgrades.NailsmithLocationModule, RandoPlus";

        private static bool _gsPropResolved;
        private static PropertyInfo _gsProp;
        private static readonly Dictionary<string, FieldInfo> GlobalFields = new(StringComparer.Ordinal);

        private static bool _invalidLocsResolved;
        private static FieldInfo _invalidLocsField;

        private static bool _nailsmithModuleResolved;
        private static MethodInfo _nailsmithModuleGetMethod;

        private static bool _failureLogged;

        internal static Func<bool> IsLoadedOverrideForTests { get; set; }
        internal static Func<string, bool?> PoolOverrideForTests { get; set; }
        internal static Func<bool?> NailUpgradesInSaveOverrideForTests { get; set; }
        internal static Func<HashSet<string>> AreaBlitzInvalidLocationsOverrideForTests { get; set; }

        public static bool IsLoaded
        {
            get
            {
                if (IsLoadedOverrideForTests != null) return IsLoadedOverrideForTests();
                if (PoolOverrideForTests != null
                    || NailUpgradesInSaveOverrideForTests != null
                    || AreaBlitzInvalidLocationsOverrideForTests != null)
                {
                    return false;
                }
                if (NightmareAccess.Instance == null) return false;
                return IsLoadedViaModHooks();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsLoadedViaModHooks() => ModHooks.GetMod(ModName) != null;

        public static bool IsMrMushroomEnabled() => IsPoolEnabled("MrMushroom");
        public static bool IsNailUpgradesEnabled() => IsPoolEnabled("NailUpgrades");
        public static bool IsNoLanternEnabled() => IsPoolEnabled("NoLantern");
        public static bool IsNoTearEnabled() => IsPoolEnabled("NoTear");
        public static bool IsAreaBlitzEnabled() => IsPoolEnabled("AreaBlitz");
        public static bool IsGhostEssenceEnabled() => IsPoolEnabled("GhostEssence");

        public static bool IsExcludedByAreaBlitz(string locationName)
        {
            if (string.IsNullOrEmpty(locationName)) return false;

            if (AreaBlitzInvalidLocationsOverrideForTests != null)
            {
                HashSet<string> testSet = AreaBlitzInvalidLocationsOverrideForTests();
                return testSet != null && testSet.Contains(locationName);
            }

            if (!IsAreaBlitzEnabled()) return false;

            try
            {
                if (!_invalidLocsResolved)
                {
                    _invalidLocsResolved = true;
                    _invalidLocsField = Type.GetType(AreaRestrictionTypeName)
                        ?.GetField("InvalidLocations", BindingFlags.Public | BindingFlags.Static);
                }

                return _invalidLocsField?.GetValue(null) is HashSet<string> set && set.Contains(locationName);
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
                return false;
            }
        }

        public static bool IsNailUpgradesActiveInSave()
        {
            if (NailUpgradesInSaveOverrideForTests?.Invoke() is bool overridden) return overridden;
            if (NightmareAccess.Instance == null || !IsLoaded) return false;
            return HasNailsmithModuleInSave();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool HasNailsmithModuleInSave()
        {
            if (Ref.Settings == null) return false;

            try
            {
                if (!_nailsmithModuleResolved)
                {
                    _nailsmithModuleResolved = true;
                    Type moduleType = Type.GetType(NailsmithModuleTypeName);
                    if (moduleType != null)
                    {
                        MethodInfo genericGet = typeof(ModuleCollection)
                            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(m => m.Name == nameof(ModuleCollection.Get)
                                                 && m.IsGenericMethodDefinition
                                                 && m.GetGenericArguments().Length == 1
                                                 && m.GetParameters().Length == 0);
                        _nailsmithModuleGetMethod = genericGet?.MakeGenericMethod(moduleType);
                    }
                }

                return _nailsmithModuleGetMethod?.Invoke(ItemChangerMod.Modules, null) != null;
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
                return false;
            }
        }

        private static bool IsPoolEnabled(string fieldName)
        {
            if (PoolOverrideForTests?.Invoke(fieldName) is bool overridden) return overridden;
            if (!IsLoaded) return false;

            try
            {
                if (!_gsPropResolved)
                {
                    _gsPropResolved = true;
                    _gsProp = Type.GetType(MainTypeName)?.GetProperty("GS", BindingFlags.Public | BindingFlags.Static);
                }

                object gs = _gsProp?.GetValue(null, null);
                if (gs == null) return false;

                if (!GlobalFields.TryGetValue(fieldName, out FieldInfo field))
                {
                    field = gs.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
                    GlobalFields[fieldName] = field;
                }

                return field?.GetValue(gs) is bool b && b;
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
                return false;
            }
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
            ModLog.Warn($"RandoPlus reflection failed: {ex.Message}");
        }
    }
}
