using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ItemChanger;
using ItemChanger.Internal;
using ItemChanger.Modules;
using Modding;

namespace NightmareAccess.Interop
{
    /// <summary>Reflection-based bridge to ExtraRando (soft dependency). Reflection data is cached.</summary>
    internal static class ExtraRandoInterop
    {
        private const string ModName = "ExtraRando";
        private const string SplitShadeCloakModuleTypeName =
            "ExtraRando.ModInterop.ItemChangerInterop.Modules.SplitShadeCloakModule, ExtraRando";

        private static bool _settingsPropResolved;
        private static PropertyInfo _settingsProp;
        private static readonly Dictionary<string, PropertyInfo> RandoSettingProps = new(StringComparer.Ordinal);

        private static bool _shadeModuleResolved;
        private static MethodInfo _shadeModuleGetMethod;
        private static PropertyInfo _hasLeftCloakProp;
        private static PropertyInfo _hasRightCloakProp;

        private static bool _failureLogged;

        internal static Func<bool> IsLoadedOverrideForTests { get; set; }
        internal static Func<string, bool?> SettingOverrideForTests { get; set; }
        internal static Func<(bool hasLeft, bool hasRight)?> SplitDashStateOverrideForTests { get; set; }

        public static bool IsLoaded
        {
            get
            {
                if (IsLoadedOverrideForTests != null) return IsLoadedOverrideForTests();
                if (SettingOverrideForTests != null || SplitDashStateOverrideForTests != null) return false;
                if (NightmareAccess.Instance == null) return false;
                return IsLoadedViaModHooks();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsLoadedViaModHooks() => ModHooks.GetMod(ModName) != null;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object GetModInstance() => ModHooks.GetMod(ModName);

        public static bool IsEnabled() => GetBoolSetting("Enabled");
        public static bool IsSplitShadeCloakEnabled() => GetBoolSetting("SplitShadeCloak");
        public static bool IsSplitFireballEnabled() => GetBoolSetting("SplitFireball");
        public static bool IsRandomizeHotSpringsEnabled() => GetBoolSetting("RandomizeHotSprings");
        public static bool IsRandomizePantheonAccessEnabled() => GetBoolSetting("RandomizePantheonAccess");
        public static bool IsRandomizeButtEnabled() => GetBoolSetting("RandomizeButt");
        public static bool IsRandomizeAwfulLocationsEnabled() => GetBoolSetting("RandomizeAwfulLocations");

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool IsPlacementActiveInSave(string placementName)
        {
            if (NightmareAccess.Instance == null || string.IsNullOrEmpty(placementName)) return false;
            var placements = Ref.Settings?.Placements;
            return placements != null && placements.ContainsKey(placementName);
        }

        public static bool TryGetSplitDashState(out bool hasLeft, out bool hasRight)
        {
            hasLeft = false;
            hasRight = false;

            if (SplitDashStateOverrideForTests != null)
            {
                var overridden = SplitDashStateOverrideForTests();
                if (overridden.HasValue)
                {
                    hasLeft = overridden.Value.hasLeft;
                    hasRight = overridden.Value.hasRight;
                    return true;
                }
            }

            if (NightmareAccess.Instance == null || !IsLoaded) return false;
            return TryGetSplitDashStateFromModules(out hasLeft, out hasRight);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryGetSplitDashStateFromModules(out bool hasLeft, out bool hasRight)
        {
            hasLeft = false;
            hasRight = false;
            if (Ref.Settings == null) return false;

            try
            {
                if (!_shadeModuleResolved)
                {
                    _shadeModuleResolved = true;
                    Type moduleType = Type.GetType(SplitShadeCloakModuleTypeName);
                    if (moduleType != null)
                    {
                        MethodInfo genericGet = typeof(ModuleCollection)
                            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(m => m.Name == nameof(ModuleCollection.Get)
                                                 && m.IsGenericMethodDefinition
                                                 && m.GetGenericArguments().Length == 1
                                                 && m.GetParameters().Length == 0);
                        _shadeModuleGetMethod = genericGet?.MakeGenericMethod(moduleType);
                        _hasLeftCloakProp = moduleType.GetProperty("HasLeftCloak", BindingFlags.Public | BindingFlags.Instance);
                        _hasRightCloakProp = moduleType.GetProperty("HasRightCloak", BindingFlags.Public | BindingFlags.Instance);
                    }
                }

                object shadeModule = _shadeModuleGetMethod?.Invoke(ItemChangerMod.Modules, null);
                if (shadeModule == null) return false;

                bool shadeLeft = _hasLeftCloakProp?.GetValue(shadeModule, null) is bool sl && sl;
                bool shadeRight = _hasRightCloakProp?.GetValue(shadeModule, null) is bool sr && sr;

                SplitCloak splitCloak = ItemChangerMod.Modules.Get<SplitCloak>();
                bool cloakLeft = splitCloak?.canDashLeft ?? false;
                bool cloakRight = splitCloak?.canDashRight ?? false;

                hasLeft = shadeLeft || cloakLeft;
                hasRight = shadeRight || cloakRight;
                return true;
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
                return false;
            }
        }

        private static bool GetBoolSetting(string propName)
        {
            if (SettingOverrideForTests?.Invoke(propName) is bool overridden) return overridden;
            if (!IsLoaded) return false;

            try
            {
                object mod = GetModInstance();
                if (mod == null) return false;

                if (!_settingsPropResolved)
                {
                    _settingsPropResolved = true;
                    _settingsProp = mod.GetType().GetProperty("Settings", BindingFlags.Public | BindingFlags.Instance);
                }

                object settings = _settingsProp?.GetValue(mod, null);
                if (settings == null) return false;

                if (!GetCachedBoolProperty(settings, "Enabled")) return false;
                if (propName == "Enabled") return true;

                return GetCachedBoolProperty(settings, propName);
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
                return false;
            }
        }

        private static bool GetCachedBoolProperty(object instance, string propName)
        {
            if (!RandoSettingProps.TryGetValue(propName, out PropertyInfo prop))
            {
                prop = instance.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                RandoSettingProps[propName] = prop;
            }
            return prop?.GetValue(instance, null) is bool b && b;
        }

        private static void LogFailureOnce(Exception ex)
        {
            if (_failureLogged) return;
            _failureLogged = true;
            ModLog.Warn($"ExtraRando reflection failed (the API may have changed): {ex.Message}");
        }
    }
}