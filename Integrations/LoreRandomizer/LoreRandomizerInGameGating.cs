using System;
using System.Reflection;
using HutongGames.PlayMaker;
using NightmareAccess.InGame;
using NightmareAccess.Interop;
using NightmareAccess.Logic;
using Satchel;

namespace NightmareAccess.Integrations.LoreRandomizer
{
    /// <summary>Runtime FSM gating for LoreCore InspectLocation, DreamNailLocation, DialogueLocation, and StagEggLocation.</summary>
    internal static class LoreRandomizerInGameGating
    {
        private static bool _hooked;

        public static void Hook()
        {
            if (_hooked) return;
            _hooked = true;

            MethodInfo fsmOnEnable = typeof(PlayMakerFSM).GetMethod(
                "OnEnable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            HookUtil.InstallAfterItemChanger(fsmOnEnable, typeof(LoreRandomizerInGameGating), nameof(OnFsmEnableDetour));
        }

        private static void OnFsmEnableDetour(Action<PlayMakerFSM> orig, PlayMakerFSM self)
        {
            orig(self);

            if (self == null || !NightmareLogicHandler.IsSaveActive) return;

            try
            {
                if (!LoreRandomizerInterop.TryGetTarget(self, out LoreRandomizerInterop.LoreTarget target))
                {
                    return;
                }

                switch (target.Kind)
                {
                    case LoreRandomizerInterop.LoreTargetKind.Inspect:
                        if (self.TryGetState("Give Items", out _))
                        {
                            FsmGateHelper.InterceptCostGate(
                                self,
                                "Hero Look Up?",
                                "FINISHED",
                                "Give Items",
                                "Look Up End?",
                                () => target.PlacementName);
                        }
                        break;

                    case LoreRandomizerInterop.LoreTargetKind.DreamNail:
                        if (self.TryGetState("Give Items", out _))
                        {
                            FsmGateHelper.InterceptCostGate(
                                self,
                                "Impact",
                                "FINISHED",
                                "Give Items",
                                "Box Down",
                                () => target.PlacementName);
                        }
                        break;

                    case LoreRandomizerInterop.LoreTargetKind.StagEgg:
                        if (self.TryGetState("Skip", out _))
                        {
                            FsmGateHelper.InterceptCostGate(
                                self,
                                "Hero Anim",
                                "FINISHED",
                                "Skip",
                                "Talk Finish",
                                () => target.PlacementName);
                        }
                        break;

                    case LoreRandomizerInterop.LoreTargetKind.Dialogue:
                        if (self.TryGetState("Give Items", out FsmState giveItems))
                        {
                            string resumeState = giveItems.GetTransition("CONVO_FINISH")?.ToState ?? "Talk Finish";
                            foreach (FsmState state in self.FsmStates)
                            {
                                foreach (FsmTransition tr in state.Transitions)
                                {
                                    if (tr.ToState == "Give Items")
                                    {
                                        FsmGateHelper.InterceptCostGate(
                                            self,
                                            state.Name,
                                            tr.EventName,
                                            "Give Items",
                                            resumeState,
                                            () => target.PlacementName);
                                    }
                                }
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                ModLog.Error($"Failed to patch LoreCore FSM '{self.FsmName}' on '{self.gameObject.name}'", ex);
            }
        }
    }
}