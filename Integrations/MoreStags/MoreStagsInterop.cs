using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Modding;

namespace NightmareAccess.Interop
{
    /// <summary>Reflection-based bridge to MoreStags (soft dependency).</summary>
    internal static class MoreStagsInterop
    {
        private const string ModName = "MoreStags";
        private const string MainTypeName = "MoreStags.MoreStags, MoreStags";

        private static bool _resolved;
        private static PropertyInfo _settingsProp;
        private static PropertyInfo _localDataProp;
        private static FieldInfo _enabledField;
        private static FieldInfo _activeStagsField;
        private static FieldInfo _stagNameField;
        private static FieldInfo _isVanillaField;
        private static bool _failureLogged;

        internal static Func<bool> IsLoadedOverrideForTests { get; set; }
        internal static Func<IEnumerable<string>> ActiveStagsOverrideForTests { get; set; }

        public static bool IsLoaded
        {
            get
            {
                if (IsLoadedOverrideForTests != null) return IsLoadedOverrideForTests();
                if (ActiveStagsOverrideForTests != null) return false;
                if (NightmareAccess.Instance == null) return false;
                return IsLoadedViaModHooks();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsLoadedViaModHooks() => ModHooks.GetMod(ModName) != null;

        public static IEnumerable<string> GetActiveNonVanillaStagLocations()
        {
            if (ActiveStagsOverrideForTests != null)
            {
                foreach (string name in ActiveStagsOverrideForTests())
                {
                    if (!string.IsNullOrEmpty(name)) yield return name;
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
                    Type mainType = Type.GetType(MainTypeName);
                    if (mainType != null)
                    {
                        _settingsProp = mainType.GetProperty("Settings", BindingFlags.Public | BindingFlags.Static);
                        _localDataProp = mainType.GetProperty("localData", BindingFlags.Public | BindingFlags.Static);
                    }
                }

                object settings = _settingsProp?.GetValue(null);
                if (settings == null) yield break;

                _enabledField ??= settings.GetType().GetField("Enabled", BindingFlags.Public | BindingFlags.Instance);
                if (_enabledField?.GetValue(settings) is not bool enabled || !enabled) yield break;

                object localData = _localDataProp?.GetValue(null);
                if (localData == null) yield break;

                _activeStagsField ??= localData.GetType().GetField("activeStags", BindingFlags.Public | BindingFlags.Instance);
                if (_activeStagsField?.GetValue(localData) is not IEnumerable activeStags) yield break;

                foreach (object stagData in activeStags)
                {
                    if (stagData == null) continue;
                    Type stagType = stagData.GetType();
                    _isVanillaField ??= stagType.GetField("isVanilla", BindingFlags.Public | BindingFlags.Instance);
                    _stagNameField ??= stagType.GetField("name", BindingFlags.Public | BindingFlags.Instance);

                    if (_isVanillaField?.GetValue(stagData) is bool isVanilla && isVanilla) continue;
                    if (_stagNameField?.GetValue(stagData) is string rawName && !string.IsNullOrEmpty(rawName))
                    {
                        results.Add("Stag-" + rawName.Replace(' ', '_'));
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

        private static void LogFailureOnce(Exception ex)
        {
            if (_failureLogged) return;
            _failureLogged = true;
            ModLog.Warn($"MoreStags reflection failed: {ex.Message}");
        }
    }
}
