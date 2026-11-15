using System;
using System.Collections.Generic;
using System.Reflection;
using ItemChanger;
using ItemChanger.Internal;
using ItemChanger.Tags;
using ItemChanger.Util;
using NightmareAccess.InGame;
using NightmareAccess.Interop;
using NightmareAccess.Logic;
using Satchel;
using UnityEngine;

namespace NightmareAccess.Integrations.ExtraRando
{
    /// <summary>Runtime gating hooks for ExtraRando AutoLocations (Hot Springs, White Defender 5, Pantheon Doors).</summary>
    internal static class ExtraRandoInGameGating
    {
        private static readonly Dictionary<string, (string PlacementName, string DisplayName)> HotSpringByScene =
            new(StringComparer.Ordinal)
            {
                ["Crossroads_30"] = ("Crossroads-Hot_Spring", "Crossroads Hot Spring"),
                ["Room_Colosseum_02"] = ("Colosseum-Hot_Spring", "Colosseum Hot Spring"),
                ["Deepnest_30"] = ("Deepnest-Hot_Spring", "Deepnest Hot Spring"),
                ["Ruins_Bathhouse"] = ("Bathhouse-Hot_Spring", "Pleasure House Hot Spring"),
                ["GG_Atrium"] = ("Lower_Godhome-Hot_Spring", "Lower Godhome Hot Spring"),
                ["GG_Atrium_Roof"] = ("Upper_Godhome-Hot_Spring", "Upper Godhome Hot Spring")
            };

        private static readonly Dictionary<string, string> PantheonByPdKey = new(StringComparer.Ordinal)
        {
            ["bossDoorStateTier1"] = "Pantheon-Master",
            ["bossDoorStateTier2"] = "Pantheon-Artist",
            ["bossDoorStateTier3"] = "Pantheon-Sage",
            ["bossDoorStateTier4"] = "Pantheon-Knight",
            ["bossDoorStateTier5"] = "Pantheon-Hallownest"
        };

        private static bool _hooked;

        public static void Hook()
        {
            if (_hooked) return;
            _hooked = true;

            MethodInfo fsmOnEnable = typeof(PlayMakerFSM).GetMethod(
                "OnEnable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            HookUtil.InstallAfterItemChanger(fsmOnEnable, typeof(ExtraRandoInGameGating), nameof(OnFsmEnableDetour));

            MethodInfo doorStart = typeof(BossSequenceDoor).GetMethod(
                "Start", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            HookUtil.Install(doorStart, typeof(ExtraRandoInGameGating), nameof(OnBossSequenceDoorStartDetour));
        }

        private static void OnFsmEnableDetour(Action<PlayMakerFSM> orig, PlayMakerFSM self)
        {
            orig(self);

            if (self == null || !NightmareLogicHandler.IsSaveActive) return;

            try
            {
                string fsmName = self.FsmName;
                string sceneName = self.gameObject.scene.name;

                if (fsmName == "Spa Region"
                    && HotSpringByScene.TryGetValue(sceneName, out var hotSpring)
                    && ExtraRandoInterop.IsPlacementActiveInSave(hotSpring.PlacementName)
                    && self.TryGetState("Check for item", out _))
                {
                    FsmGateHelper.InterceptCostGate(
                        self,
                        "Stay",
                        "WAIT",
                        "Check for item",
                        "Heal Pause",
                        () => hotSpring.PlacementName,
                        () => hotSpring.DisplayName);
                }
                else if (fsmName == "Conversation Control"
                         && sceneName == "Waterways_15"
                         && self.gameObject.name == "Head"
                         && self.transform.parent?.name == "Dung Defender_Awake"
                         && ExtraRandoInterop.IsPlacementActiveInSave(ExtraRandoIntegration.WhiteDefender5Location)
                         && self.TryGetState("Give Item", out _))
                {
                    FsmGateHelper.InterceptCostGate(
                        self,
                        "Box Down",
                        "FINISHED",
                        "Give Item",
                        "End",
                        () => ExtraRandoIntegration.WhiteDefender5Location,
                        () => "White Defender (5 Defeats)");
                }
            }
            catch (Exception ex)
            {
                ModLog.Error($"Failed to patch ExtraRando FSM '{self.FsmName}' on '{self.gameObject.name}'", ex);
            }
        }

        private static void OnBossSequenceDoorStartDetour(Action<BossSequenceDoor> orig, BossSequenceDoor self)
        {
            orig(self);

            if (self == null || !NightmareLogicHandler.IsSaveActive) return;
            if (string.IsNullOrEmpty(self.playerDataString)
                || !PantheonByPdKey.TryGetValue(self.playerDataString, out string placementName))
            {
                return;
            }

            try
            {
                EnsurePantheonShinyIfUnlocked(self, placementName);
            }
            catch (Exception ex)
            {
                ModLog.Error($"Failed to verify Pantheon shiny for '{placementName}'", ex);
            }
        }

        private static void EnsurePantheonShinyIfUnlocked(BossSequenceDoor door, string placementName)
        {
            var placements = Ref.Settings?.Placements;
            if (placements == null || !placements.TryGetValue(placementName, out AbstractPlacement placement)) return;
            if (placement.AllObtained()) return;

            Cost cost = placement.GetTag<CostTag>()?.Cost;
            if (NightmareAccessCost.Find(cost) == null) return;

            BossSequenceDoor.Completion completion =
                PlayerData.instance.GetVariable<BossSequenceDoor.Completion>(door.playerDataString);
            if (!completion.unlocked) return;

            string shinyPrefix = ShinyUtility.GetShinyPrefix(placement);
            foreach (PlayMakerFSM fsm in UnityEngine.Object.FindObjectsOfType<PlayMakerFSM>())
            {
                if (fsm != null
                    && fsm.FsmName == "Shiny Control"
                    && fsm.gameObject.name.StartsWith(shinyPrefix, StringComparison.Ordinal))
                {
                    return;
                }
            }

            GameObject shiny = ShinyUtility.MakeNewMultiItemShiny(
                placement, placement.Items, FlingType.DirectDeposit, cost);
            shiny.transform.position = door.transform.position - new Vector3(5f, 0f, 3f);
            shiny.SetActive(true);
        }
    }
}