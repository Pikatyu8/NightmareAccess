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
    /// <summary>Reflection-based bridge to AccessRandomizer (soft dependency). Reflection data is cached.</summary>
    internal static class AccessRandomizerInterop
    {
        private const string ModName = "AccessRandomizer";
        private const string AccessModuleTypeName = "AccessRandomizer.Modules.AccessModule, AccessRandomizer";
        private const string CustomKeysPrefix = "CustomKeys.";

        private static bool _gsPropResolved;
        private static PropertyInfo _gsProp;

        private static bool _moduleTypeResolved;
        private static Type _moduleType;
        private static MethodInfo _moduleGetMethod;

        private static readonly Dictionary<string, PropertyInfo> GlobalProps = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, PropertyInfo> SaveSettingsProps = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, FieldInfo> CustomKeyFields = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, PropertyInfo> ModuleBoolProps = new(StringComparer.Ordinal);

        private static bool _failureLogged;

        internal static Func<bool> IsLoadedOverrideForTests { get; set; }
        internal static Func<string, bool?> PoolOverrideForTests { get; set; }
        internal static Func<int?> UniqueKeyCountOverrideForTests { get; set; }

        public static bool IsLoaded
        {
            get
            {
                if (IsLoadedOverrideForTests != null) return IsLoadedOverrideForTests();
                if (PoolOverrideForTests != null || UniqueKeyCountOverrideForTests != null) return false;
                if (NightmareAccess.Instance == null) return false;
                return IsLoadedViaModHooks();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsLoadedViaModHooks() => ModHooks.GetMod(ModName) != null;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object GetModInstance() => ModHooks.GetMod(ModName);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsRandoKeysPoolEnabledInSave() =>
            RandomizerMod.RandomizerMod.RS?.GenerationSettings?.PoolSettings?.Keys ?? false;

        // ------------------------------------------------------------------
        // Generation-time settings (AccessRandomizer.GS)
        // ------------------------------------------------------------------

        public static bool IsMantisRespectRandomized() => IsEnabledInGlobal("MantisRespect");
        public static bool IsHollowKnightChainsRandomized() => IsEnabledInGlobal("HollowKnightChains");
        public static bool IsUniqueKeysRandomized() => IsEnabledInGlobal("UniqueKeys");
        public static bool IsSplitTramRandomized() => IsEnabledInGlobal("SplitTram");
        public static bool IsSplitElevatorRandomized() => IsEnabledInGlobal("SplitElevator");
        public static bool IsTrapBenchRandomized() => IsEnabledInGlobal("TrapBench");
        public static bool IsShadeGatesRandomized() => IsEnabledInGlobal("ShadeGates");
        public static bool IsMapperKeyRandomized() => IsEnabledInGlobal("CustomKeys.MapperKey");
        public static bool IsSlyKeyRandomized() => IsEnabledInGlobal("CustomKeys.SlyKey");
        public static bool IsBrettaKeyRandomized() => IsEnabledInGlobal("CustomKeys.BrettaKey");
        public static bool IsZoteKeyRandomized() => IsEnabledInGlobal("CustomKeys.ZoteKey");
        public static bool IsRelicKeyRandomized() => IsEnabledInGlobal("CustomKeys.RelicKey");
        public static bool IsGladeKeyRandomized() => IsEnabledInGlobal("CustomKeys.GladeKey");
        public static bool IsBirthplaceKeyRandomized() => IsEnabledInGlobal("CustomKeys.BirthplaceKey");

        // ------------------------------------------------------------------
        // Runtime in-save settings (AccessModule.Settings)
        // ------------------------------------------------------------------

        public static bool IsMantisRespectActiveInSave() => IsEnabledInSave("MantisRespect");
        public static bool IsHollowKnightChainsActiveInSave() => IsEnabledInSave("HollowKnightChains");
        public static bool IsTrapBenchActiveInSave() => IsEnabledInSave("TrapBench");
        public static bool IsShadeGatesActiveInSave() => IsEnabledInSave("ShadeGates");

        public static bool IsUniqueKeysActiveInSave()
        {
            if (UniqueKeyCountOverrideForTests != null) return true;
            if (!IsEnabledInSave("UniqueKeys")) return false;
            return IsRandoKeysPoolEnabledInSave();
        }

        public static bool TryGetObtainedUniqueKeysCount(out int count)
        {
            count = 0;
            if (UniqueKeyCountOverrideForTests != null)
            {
                int? overridden = UniqueKeyCountOverrideForTests();
                if (overridden.HasValue)
                {
                    count = overridden.Value;
                    return true;
                }
            }

            if (!IsUniqueKeysActiveInSave()) return false;

            object module = TryGetAccessModuleInSave();
            if (module == null) return false;

            try
            {
                int total = 0;
                if (GetModuleBool(module, "GraveyardKey")) total++;
                if (GetModuleBool(module, "WaterwaysKey")) total++;
                if (GetModuleBool(module, "PleasureKey")) total++;
                if (GetModuleBool(module, "CoffinKey")) total++;
                count = total;
                return true;
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
                return false;
            }
        }

        private static bool IsEnabledInGlobal(string settingPath)
        {
            if (PoolOverrideForTests?.Invoke(settingPath) is bool overridden) return overridden;
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

                if (!GetCachedBoolProperty(gs, "Enabled", GlobalProps)) return false;

                if (settingPath.StartsWith(CustomKeysPrefix, StringComparison.Ordinal))
                {
                    object customKeys = GetCachedPropertyValue(gs, "CustomKeys", GlobalProps);
                    if (customKeys == null) return false;
                    return GetCachedBoolField(customKeys, settingPath.Substring(CustomKeysPrefix.Length));
                }

                return GetCachedBoolProperty(gs, settingPath, GlobalProps);
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
                return false;
            }
        }

        private static bool IsEnabledInSave(string settingPath)
        {
            if (PoolOverrideForTests?.Invoke(settingPath) is bool overridden) return overridden;
            if (!IsLoaded) return false;

            try
            {
                object module = TryGetAccessModuleInSave();
                if (module == null) return false;

                object saveSettings = GetCachedPropertyValue(module, "Settings", ModuleBoolProps);
                if (saveSettings == null) return false;

                if (!GetCachedBoolProperty(saveSettings, "IsRando", SaveSettingsProps)) return false;

                if (settingPath.StartsWith(CustomKeysPrefix, StringComparison.Ordinal))
                {
                    object customKeys = GetCachedPropertyValue(saveSettings, "CustomKeys", SaveSettingsProps);
                    if (customKeys == null) return false;
                    return GetCachedBoolField(customKeys, settingPath.Substring(CustomKeysPrefix.Length));
                }

                return GetCachedBoolProperty(saveSettings, settingPath, SaveSettingsProps);
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object TryGetAccessModuleInSave()
        {
            if (Ref.Settings == null) return null;

            if (!_moduleTypeResolved)
            {
                _moduleTypeResolved = true;
                _moduleType = Type.GetType(AccessModuleTypeName);
                if (_moduleType != null)
                {
                    MethodInfo genericGet = typeof(ModuleCollection)
                        .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(m => m.Name == nameof(ModuleCollection.Get)
                                             && m.IsGenericMethodDefinition
                                             && m.GetGenericArguments().Length == 1
                                             && m.GetParameters().Length == 0);
                    _moduleGetMethod = genericGet?.MakeGenericMethod(_moduleType);
                }
            }

            return _moduleGetMethod?.Invoke(ItemChangerMod.Modules, null);
        }

        private static bool GetModuleBool(object module, string propName) =>
            GetCachedBoolProperty(module, propName, ModuleBoolProps);

        private static object GetCachedPropertyValue(object instance, string propName, Dictionary<string, PropertyInfo> cache)
        {
            if (!cache.TryGetValue(propName, out PropertyInfo prop))
            {
                prop = instance.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                cache[propName] = prop;
            }
            return prop?.GetValue(instance, null);
        }

        private static bool GetCachedBoolProperty(object instance, string propName, Dictionary<string, PropertyInfo> cache) =>
            GetCachedPropertyValue(instance, propName, cache) is bool b && b;

        private static bool GetCachedBoolField(object instance, string fieldName)
        {
            if (!CustomKeyFields.TryGetValue(fieldName, out FieldInfo field))
            {
                field = instance.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
                CustomKeyFields[fieldName] = field;
            }
            return field?.GetValue(instance) is bool b && b;
        }

        private static void LogFailureOnce(Exception ex)
        {
            if (_failureLogged) return;
            _failureLogged = true;
            ModLog.Warn($"AccessRandomizer reflection failed (the API may have changed): {ex.Message}");
        }
    }
}