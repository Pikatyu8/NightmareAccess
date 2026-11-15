using System;
using System.Collections.Generic;
using Modding;
using NightmareAccess.InGame;
using NightmareAccess.Integrations;
using NightmareAccess.Interop;
using NightmareAccess.Logic;
using NightmareAccess.Settings;
using RandomizerMod.Logging;
using RandomizerMod.RC;

namespace NightmareAccess
{
    public class NightmareAccessSaveData
    {
        public bool Enabled { get; set; }
        public RequirementScope? Scope { get; set; }
        public Dictionary<string, AccessCondition> LocationConditions { get; set; } = new();
        public Dictionary<string, List<AccessCondition>> ShopConditions { get; set; } = new();
        public Dictionary<string, AccessCondition> ItemTypeConditions { get; set; } = new();
        public Dictionary<string, AccessCondition> ObjectConditions { get; set; } = new();
        public Dictionary<string, int> IntegrationInts { get; set; } = new();
    }

    public class NightmareAccess : Mod, IGlobalSettings<NightmareAccessSettings>, ILocalSettings<NightmareAccessSaveData>
    {
        // Single source of truth for the version: AssemblyVersion in the csproj
        private static readonly string ModVersion = typeof(NightmareAccess).Assembly.GetName().Version.ToString();

        public static NightmareAccess Instance { get; private set; }

        /// <summary>
        /// One instance for the whole mod lifetime. Menu closures capture it, so it must never be replaced;
        /// use CopyFrom to load values into it.
        /// </summary>
        public static NightmareAccessSettings Settings { get; } = new();

        public NightmareAccess() : base("Nightmare Access")
        {
            Instance = this;
        }

        public override string GetVersion() => ModVersion;

        public void OnLoadGlobal(NightmareAccessSettings s) => Settings.CopyFrom(s);

        public NightmareAccessSettings OnSaveGlobal() => Settings;

        public void OnLoadLocal(NightmareAccessSaveData s) => ApplySaveData(s);

        public NightmareAccessSaveData OnSaveLocal() => ExportSaveData();

        internal static void ApplySaveData(NightmareAccessSaveData s)
        {
            // A null save must reset the state, otherwise conditions leak from the previously loaded save
            NightmareLogicHandler.LocationConditions = s?.LocationConditions ?? new();
            NightmareLogicHandler.ShopConditions = s?.ShopConditions ?? new();
            NightmareLogicHandler.ItemTypeConditions = s?.ItemTypeConditions ?? new();
            NightmareLogicHandler.ObjectConditions = s?.ObjectConditions ?? new();
            NightmareLogicHandler.IntegrationInts = s?.IntegrationInts ?? new();

            NightmareLogicHandler.IsSaveActive = s != null && (
                s.Enabled
                || NightmareLogicHandler.LocationConditions.Count > 0
                || NightmareLogicHandler.ShopConditions.Count > 0
                || NightmareLogicHandler.ItemTypeConditions.Count > 0
                || NightmareLogicHandler.ObjectConditions.Count > 0);

            NightmareLogicHandler.ActiveScope = InferScope(s);

            ModLog.Info(
                $"Save loaded: Active={NightmareLogicHandler.IsSaveActive}, Scope={NightmareLogicHandler.ActiveScope}, " +
                $"Loc={NightmareLogicHandler.LocationConditions.Count}, " +
                $"ObjGroup={NightmareLogicHandler.ItemTypeConditions.Count}, " +
                $"Obj={NightmareLogicHandler.ObjectConditions.Count}");
        }

        private static RequirementScope InferScope(NightmareAccessSaveData s)
        {
            if (s?.Scope != null) return s.Scope.Value;
            if (NightmareLogicHandler.ObjectConditions.Count > 0) return RequirementScope.PerObject;
            if (NightmareLogicHandler.ItemTypeConditions.Count > 0 && NightmareLogicHandler.LocationConditions.Count == 0)
            {
                return RequirementScope.PerObjectGroup;
            }
            return RequirementScope.PerLocation;
        }

        internal static NightmareAccessSaveData ExportSaveData()
        {
            return new NightmareAccessSaveData
            {
                Enabled = NightmareLogicHandler.IsSaveActive,
                Scope = NightmareLogicHandler.ActiveScope,
                LocationConditions = NightmareLogicHandler.LocationConditions,
                ShopConditions = NightmareLogicHandler.ShopConditions,
                ItemTypeConditions = NightmareLogicHandler.ItemTypeConditions,
                ObjectConditions = NightmareLogicHandler.ObjectConditions,
                IntegrationInts = NightmareLogicHandler.IntegrationInts
            };
        }

        public override void Initialize()
        {
            Log("Initializing Nightmare Access...");

            IntegrationRegistry.Initialize();
            NightmareLogicHandler.Hook();
            ConnectionMenu.Hook();
            RSM_Interop.Hook();
            LogManager.AddLogger(new NightmareSpoilerLog());
            EssenceChoicePrompt.Hook();
            ShinyFsmPatcher.Hook();
            ColosseumPrompt.Hook();
            ColosseumGladiatorSplash.Hook();

            RandoController.OnExportCompleted += OnRandoExportCompleted;
            ModHooks.LanguageGetHook += OnLanguageGet;

            Log("Nightmare Access initialized successfully.");
        }

        private static void OnRandoExportCompleted(RandoController rc)
        {
            try
            {
                NightmareLogicHandler.IsSaveActive = Settings.Enabled;
                NightmareLogicHandler.ActiveScope = Settings.Scope;
                CostApplier.Apply(rc);
            }
            catch (Exception ex)
            {
                // Never let a failure here abort the randomizer export
                ModLog.Error("Failed to apply Nightmare Access costs", ex);
            }
        }

        private static string OnLanguageGet(string key, string sheetTitle, string orig) =>
            RewriteGeoPrompt(key, orig);

        internal static string RewriteGeoPrompt(string key, string orig)
        {
            if (!NightmareLogicHandler.IsSaveActive) return orig;

            if (key == "NOT_ENOUGH_GEO" || (orig != null && orig.StartsWith("Not enough Geo", StringComparison.Ordinal)))
            {
                return "Not enough.";
            }
            return orig;
        }
    }
}