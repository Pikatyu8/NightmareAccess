using System;
using System.Collections.Generic;
using System.Reflection;
using ItemChanger;
using ItemChanger.Util;
using NightmareAccess.InGame;
using NightmareAccess.Interop;
using NightmareAccess.Logic;
using Satchel;

namespace NightmareAccess.Integrations.AccessRandomizer
{
    /// <summary>Runtime gating hooks for AccessRandomizer AutoLocations (Mantis Respect, Hollow Knight Chains, Trap Bench, Shade Gates).</summary>
    internal static class AccessRandomizerInGameGating
    {
        private static bool _hooked;

        public static void Hook()
        {
            if (_hooked) return;
            _hooked = true;

            MethodInfo fsmOnEnable = typeof(PlayMakerFSM).GetMethod(
                "OnEnable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            HookUtil.InstallAfterItemChanger(fsmOnEnable, typeof(AccessRandomizerInGameGating), nameof(OnFsmEnableDetour));

            MethodInfo giveSequentially = typeof(ItemUtility).GetMethod(
                nameof(ItemUtility.GiveSequentially),
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(IEnumerable<AbstractItem>), typeof(AbstractPlacement), typeof(GiveInfo), typeof(Action) },
                null);
            HookUtil.Install(giveSequentially, typeof(AccessRandomizerInGameGating), nameof(OnGiveSequentiallyDetour));
        }

        private static void OnGiveSequentiallyDetour(
            Action<IEnumerable<AbstractItem>, AbstractPlacement, GiveInfo, Action> orig,
            IEnumerable<AbstractItem> items,
            AbstractPlacement placement,
            GiveInfo info,
            Action callback)
        {
            if (NightmareLogicHandler.IsSaveActive
                && placement != null
                && AccessRandomizerIntegration.IsShadeGateLocation(placement.Name)
                && AccessRandomizerInterop.IsShadeGatesActiveInSave())
            {
                if (!CornerPromptHelper.CheckOrShowLocked(
                        placement.Name, FsmGateHelper.FormatPlacementName(placement.Name)))
                {
                    callback?.Invoke();
                    return;
                }
            }

            orig(items, placement, info, callback);
        }

        private static void OnFsmEnableDetour(Action<PlayMakerFSM> orig, PlayMakerFSM self)
        {
            orig(self);

            if (self == null || !NightmareLogicHandler.IsSaveActive) return;

            try
            {
                string fsmName = self.FsmName;
                string objName = self.gameObject.name;
                string sceneName = self.gameObject.scene.name;

                if (fsmName == "Challenge Start" && objName == "Challenge Prompt")
                {
                    PatchMantisChallengePrompt(self);
                }
                else if (fsmName == "Control" && sceneName == SceneNames.Room_Final_Boss_Core)
                {
                    PatchHollowKnightChain(self);
                }
                else if (fsmName == "Bench Control Spider"
                         && objName == "RestBench Spider"
                         && sceneName == SceneNames.Deepnest_Spider_Town)
                {
                    PatchTrapBench(self);
                }
            }
            catch (Exception ex)
            {
                ModLog.Error($"Failed to patch AccessRandomizer FSM '{self.FsmName}' on '{self.gameObject.name}'", ex);
            }
        }

        private static void PatchMantisChallengePrompt(PlayMakerFSM fsm)
        {
            if (!AccessRandomizerInterop.IsMantisRespectActiveInSave()) return;

            FsmGateHelper.InterceptCostGate(
                fsm,
                "Can Talk?",
                "FINISHED",
                "Take Control",
                "Cancel Frame",
                () => AccessRandomizerIntegration.MantisRespectName,
                () => "Mantis' Respect");
        }

        private static void PatchHollowKnightChain(PlayMakerFSM fsm)
        {
            if (!AccessRandomizerInterop.IsHollowKnightChainsActiveInSave()) return;
            if (!TryGetChainPlacementName(fsm.gameObject.name, out string chainPlacement)) return;

            FsmGateHelper.InterceptCostGate(
                fsm,
                "Check Hits",
                "BREAK",
                "Break",
                "Hit",
                () => chainPlacement,
                () => chainPlacement.Replace('_', ' ').Replace("-", " #"));
        }

        private static void PatchTrapBench(PlayMakerFSM fsm)
        {
            if (!AccessRandomizerInterop.IsTrapBenchActiveInSave()) return;
            if (!fsm.TryGetState("GiveItem", out _)) return;

            // Skip GiveItem when locked while still allowing Sit Start -> Neutral so Beast's Den entry is preserved
            FsmGateHelper.InterceptCostGate(
                fsm,
                "Sit Start",
                "FINISHED",
                "GiveItem",
                "Neutral",
                () => AccessRandomizerIntegration.TrapBenchName,
                () => "Trap Bench");
        }

        private static bool TryGetChainPlacementName(string objectName, out string placementName)
        {
            placementName = objectName switch
            {
                "hollow_knight_chain_base" => "Hollow_Knight_Chain-1",
                "hollow_knight_chain_base 2" => "Hollow_Knight_Chain-2",
                "hollow_knight_chain_base 3" => "Hollow_Knight_Chain-3",
                "hollow_knight_chain_base 4" => "Hollow_Knight_Chain-4",
                _ => null
            };
            return placementName != null;
        }
    }
}