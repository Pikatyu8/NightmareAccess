using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Modding;
using RandomizerMod.Settings;

namespace NightmareAccess.Interop
{
    /// <summary>Reflection-based bridge to BenchRando (soft dependency).</summary>
    internal static class BenchRandoInterop
    {
        private const string ModName = "BenchRando";
        private const string RandoInteropTypeName = "BenchRando.Rando.RandoInterop, BenchRando";

        private static bool _resolved;
        private static FieldInfo _lsField;
        private static FieldInfo _lsSettingsField;
        private static FieldInfo _randomizedItemsField;
        private static FieldInfo _randomizedBenchesField;
        private static bool _failureLogged;

        private static readonly HashSet<string> WhitePalaceBenches = new(StringComparer.Ordinal)
        {
            "Bench-Palace_Entrance",
            "Bench-Palace_Atrium",
            "Bench-Palace_Balcony",
            "Bench-Kingsmould_Duelist",
            "Bench-Palace_West",
            "Bench-Sawblade_Choir",
            "Bench-Palace_East",
            "Bench-Thorny_Respite",
            "Bench-Palace_Workshop",
            "Bench-Throne_Approach",
            "Bench-Path_Midpoint"
        };

        internal static Func<bool> IsLoadedOverrideForTests { get; set; }
        internal static Func<IEnumerable<string>> RandomizedBenchesOverrideForTests { get; set; }

        public static bool IsLoaded
        {
            get
            {
                if (IsLoadedOverrideForTests != null) return IsLoadedOverrideForTests();
                if (RandomizedBenchesOverrideForTests != null) return false;
                if (NightmareAccess.Instance == null) return false;
                return IsLoadedViaModHooks();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsLoadedViaModHooks() => ModHooks.GetMod(ModName) != null;

        public static IEnumerable<string> GetActiveRandomizedBenches(GenerationSettings gs)
        {
            LongLocationSettings.WPSetting? wpSetting = gs?.LongLocationSettings?.WhitePalaceRando;

            if (RandomizedBenchesOverrideForTests != null)
            {
                foreach (string b in RandomizedBenchesOverrideForTests())
                {
                    if (!string.IsNullOrEmpty(b) && !IsExcludedByWhitePalaceSetting(b, wpSetting))
                    {
                        yield return b;
                    }
                }
                yield break;
            }

            if (!IsLoaded) yield break;

            List<string> results = new();
            try
            {
                if (!_resolved)
                {
                    _resolved = true;
                    Type interopType = Type.GetType(RandoInteropTypeName);
                    _lsField = interopType?.GetField("LS", BindingFlags.Public | BindingFlags.Static);
                }

                object ls = _lsField?.GetValue(null);
                if (ls == null) yield break;

                _lsSettingsField ??= ls.GetType().GetField("Settings", BindingFlags.Public | BindingFlags.Instance);
                object settings = _lsSettingsField?.GetValue(ls);
                if (settings == null) yield break;

                _randomizedItemsField ??= settings.GetType().GetField("RandomizedItems", BindingFlags.Public | BindingFlags.Instance);
                object itemModeObj = _randomizedItemsField?.GetValue(settings);
                if (itemModeObj == null || Convert.ToInt32(itemModeObj) == 0) yield break;

                _randomizedBenchesField ??= ls.GetType().GetField("RandomizedBenches", BindingFlags.Public | BindingFlags.Instance);
                if (_randomizedBenchesField?.GetValue(ls) is IEnumerable<string> benches)
                {
                    foreach (string bench in benches)
                    {
                        if (!string.IsNullOrEmpty(bench) && !IsExcludedByWhitePalaceSetting(bench, wpSetting))
                        {
                            results.Add(bench);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
                yield break;
            }

            foreach (string bench in results)
            {
                yield return bench;
            }
        }

        private static bool IsExcludedByWhitePalaceSetting(string benchName, LongLocationSettings.WPSetting? wp)
        {
            if (wp == LongLocationSettings.WPSetting.ExcludeWhitePalace)
            {
                return WhitePalaceBenches.Contains(benchName);
            }
            if (wp == LongLocationSettings.WPSetting.ExcludePathOfPain)
            {
                return benchName == "Bench-Path_Midpoint";
            }
            return false;
        }

        private static void LogFailureOnce(Exception ex)
        {
            if (_failureLogged) return;
            _failureLogged = true;
            ModLog.Warn($"BenchRando reflection failed: {ex.Message}");
        }
    }
}
