using System;
using System.Linq;
using System.Reflection;
using HutongGames.PlayMaker;
using ItemChanger;
using NightmareAccess.InGame;
using NightmareAccess.Logic;

namespace NightmareAccess.Integrations.BenchRando
{
    /// <summary>Runtime gating hooks for BenchRando benches (uses BenchContainer's injected states).</summary>
    internal static class BenchRandoInGameGating
    {
        private static bool _hooked;

        public static void Hook()
        {
            if (_hooked) return;
            _hooked = true;

            MethodInfo fsmOnEnable = typeof(PlayMakerFSM).GetMethod(
                "OnEnable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            HookUtil.InstallAfterItemChanger(fsmOnEnable, typeof(BenchRandoInGameGating), nameof(OnFsmEnableDetour));
        }

        private static void OnFsmEnableDetour(Action<PlayMakerFSM> orig, PlayMakerFSM self)
        {
            orig(self);

            if (self == null || !NightmareLogicHandler.IsSaveActive || self.FsmName != "Bench Control")
            {
                return;
            }

            try
            {
                ContainerInfo info = ContainerInfo.FindContainerInfo(self.gameObject);
                string placementName = info?.giveInfo?.placement?.Name;
                if (string.IsNullOrEmpty(placementName)
                    || !placementName.StartsWith(BenchRandoIntegration.BenchPrefix, StringComparison.Ordinal))
                {
                    return;
                }

                // 1. Locked bench (RequireUnlockToRestTag) using BenchContainer's *-No Rest states
                FsmGateHelper.InterceptCostGate(
                    self,
                    "In Range-No Rest",
                    "UP PRESSED",
                    "Hero Down-No Rest",
                    "Idle-No Rest",
                    () => placementName);

                // 2. Restable bench: keep Save Game / resting intact, skip Give when locked
                FsmState saveGame = self.FsmStates.FirstOrDefault(s => s.Name == "Save Game");
                FsmTransition saveToGive = saveGame?.Transitions.FirstOrDefault(t => t.ToState == "Give");
                if (saveToGive != null)
                {
                    FsmGateHelper.InterceptCostGate(
                        self,
                        "Save Game",
                        saveToGive.EventName,
                        "Give",
                        "Give Recover",
                        () => placementName);
                }

                // 3. Benchwarp load-in: skip Give-Init Rest when locked while preserving wakeup flow
                FsmState startle = self.FsmStates.FirstOrDefault(s => s.Name == "Startle");
                FsmTransition startleToGive = startle?.Transitions.FirstOrDefault(t => t.ToState == "Give-Init Rest");
                if (startleToGive != null)
                {
                    FsmGateHelper.InterceptCostGate(
                        self,
                        "Startle",
                        startleToGive.EventName,
                        "Give-Init Rest",
                        "Give Recover-Init Rest",
                        () => placementName);
                }
            }
            catch (Exception ex)
            {
                ModLog.Error($"Failed to patch Bench Control FSM on '{self.gameObject.name}'", ex);
            }
        }
    }
}
