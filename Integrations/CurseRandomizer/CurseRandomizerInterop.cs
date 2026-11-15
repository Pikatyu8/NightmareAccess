using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ItemChanger;
using ItemChanger.Internal;
using Modding;
using NightmareAccess.Logic;

namespace NightmareAccess.Interop
{
    /// <summary>Reflection-based bridge to CurseRandomizer (soft dependency).</summary>
    internal static class CurseRandomizerInterop
    {
        private const string ModName = "CurseRandomizer";
        private const string ModManagerTypeName = "CurseRandomizer.Manager.ModManager, CurseRandomizer";
        private const string VesselModuleTypeName = "CurseRandomizer.Modules.VesselModule, CurseRandomizer";

        private static bool _modResolved;
        private static PropertyInfo _settingsProp;
        private static PropertyInfo _generalSettingsProp;
        private static PropertyInfo _enabledProp;
        private static PropertyInfo _cursedVesselProp;

        private static bool _walletPropResolved;
        private static PropertyInfo _walletCapacitiesProp;

        private static bool _vesselModuleResolved;
        private static MethodInfo _vesselModuleGetMethod;
        private static PropertyInfo _soulVesselProp;

        private static bool _failureLogged;

        internal static Func<bool> IsLoadedOverrideForTests { get; set; }
        internal static Func<int[]> WalletCapacitiesOverrideForTests { get; set; }
        internal static Func<int?> CursedVesselCountOverrideForTests { get; set; }
        internal static Func<int?> SavedCursedVesselOverrideForTests { get; set; }
        internal static Func<int?> CurrentSoulVesselOverrideForTests { get; set; }

        public static bool IsLoaded
        {
            get
            {
                if (IsLoadedOverrideForTests != null) return IsLoadedOverrideForTests();
                if (WalletCapacitiesOverrideForTests != null
                    || CursedVesselCountOverrideForTests != null
                    || SavedCursedVesselOverrideForTests != null
                    || CurrentSoulVesselOverrideForTests != null)
                {
                    return false;
                }
                if (NightmareAccess.Instance == null) return false;
                return IsLoadedViaModHooks();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsLoadedViaModHooks() => ModHooks.GetMod(ModName) != null;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object GetModInstance() => ModHooks.GetMod(ModName);

        public static bool TryGetWalletCapacities(out int[] capacities)
        {
            if (WalletCapacitiesOverrideForTests != null)
            {
                capacities = WalletCapacitiesOverrideForTests();
                return capacities != null && capacities.Length > 0;
            }

            capacities = null;
            if (!IsLoaded) return false;

            try
            {
                if (!_walletPropResolved)
                {
                    _walletPropResolved = true;
                    Type mmType = Type.GetType(ModManagerTypeName);
                    _walletCapacitiesProp = mmType?.GetProperty("WalletCapacities", BindingFlags.Public | BindingFlags.Static);
                }

                capacities = _walletCapacitiesProp?.GetValue(null, null) as int[];
                return capacities != null && capacities.Length > 0;
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
                return false;
            }
        }

        public static int GetCursedVesselCount()
        {
            if (CursedVesselCountOverrideForTests != null)
            {
                return Math.Max(0, CursedVesselCountOverrideForTests() ?? 0);
            }

            if (!IsLoaded) return 0;

            try
            {
                object mod = GetModInstance();
                if (mod == null) return 0;

                if (!_modResolved)
                {
                    _modResolved = true;
                    _settingsProp = mod.GetType().GetProperty("Settings", BindingFlags.Public | BindingFlags.Instance);
                }

                object randoSettings = _settingsProp?.GetValue(mod, null);
                if (randoSettings == null) return 0;

                _generalSettingsProp ??= randoSettings.GetType().GetProperty("GeneralSettings", BindingFlags.Public | BindingFlags.Instance);
                object general = _generalSettingsProp?.GetValue(randoSettings, null);
                if (general == null) return 0;

                _enabledProp ??= general.GetType().GetProperty("Enabled", BindingFlags.Public | BindingFlags.Instance);
                if (_enabledProp?.GetValue(general, null) is not bool enabled || !enabled) return 0;

                _cursedVesselProp ??= general.GetType().GetProperty("CursedVessel", BindingFlags.Public | BindingFlags.Instance);
                if (_cursedVesselProp?.GetValue(general, null) is int count && count > 0)
                {
                    return count;
                }
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
            }

            return 0;
        }

        public static bool TryGetCollectedVesselFragments(PlayerData pd, out int count)
        {
            count = 0;
            if (pd == null) return false;

            int initialCursedVessel = 0;
            if (SavedCursedVesselOverrideForTests != null)
            {
                initialCursedVessel = SavedCursedVesselOverrideForTests() ?? 0;
            }
            else if (NightmareLogicHandler.IntegrationInts.TryGetValue("CurseRandomizer.CursedVessel", out int savedCv))
            {
                initialCursedVessel = savedCv;
            }

            if (initialCursedVessel <= 0) return false;

            int soulVessel;
            if (CurrentSoulVesselOverrideForTests != null)
            {
                int? overridden = CurrentSoulVesselOverrideForTests();
                if (!overridden.HasValue) return false;
                soulVessel = overridden.Value;
            }
            else
            {
                if (NightmareAccess.Instance == null || !IsLoaded) return false;
                if (!TryGetSoulVesselInSave(out soulVessel)) return false;
            }

            int restoredTiers = Math.Max(0, soulVessel - (2 - initialCursedVessel));
            count = restoredTiers * 3 + Math.Max(0, (pd.MPReserveMax / 33) * 3) + pd.vesselFragments;
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryGetSoulVesselInSave(out int soulVessel)
        {
            soulVessel = 0;
            if (Ref.Settings == null) return false;

            try
            {
                if (!_vesselModuleResolved)
                {
                    _vesselModuleResolved = true;
                    Type moduleType = Type.GetType(VesselModuleTypeName);
                    if (moduleType != null)
                    {
                        MethodInfo genericGet = typeof(ModuleCollection)
                            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(m => m.Name == nameof(ModuleCollection.Get)
                                                 && m.IsGenericMethodDefinition
                                                 && m.GetGenericArguments().Length == 1
                                                 && m.GetParameters().Length == 0);
                        _vesselModuleGetMethod = genericGet?.MakeGenericMethod(moduleType);
                        _soulVesselProp = moduleType.GetProperty("SoulVessel", BindingFlags.Public | BindingFlags.Instance);
                    }
                }

                object module = _vesselModuleGetMethod?.Invoke(ItemChangerMod.Modules, null);
                if (module == null) return false;

                if (_soulVesselProp?.GetValue(module, null) is int val)
                {
                    soulVessel = val;
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
            ModLog.Warn($"CurseRandomizer reflection failed: {ex.Message}");
        }
    }
}
