using System;
using System.Reflection;
using HutongGames.PlayMaker;
using NightmareAccess.InGame;
using NightmareAccess.Interop;
using NightmareAccess.Logic;
using Satchel;

namespace NightmareAccess.Integrations.BreakableWallRandomizer
{
    /// <summary>Runtime FSM gating for all 7 BreakableWallRandomizer FSM templates (+ collapse tute).</summary>
    internal static class BreakableWallInGameGating
    {
        private static bool _hooked;

        public static void Hook()
        {
            if (_hooked) return;
            _hooked = true;

            MethodInfo fsmOnEnable = typeof(PlayMakerFSM).GetMethod(
                "OnEnable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            HookUtil.InstallAfterItemChanger(fsmOnEnable, typeof(BreakableWallInGameGating), nameof(OnFsmEnableDetour));
        }

        private static void OnFsmEnableDetour(Action<PlayMakerFSM> orig, PlayMakerFSM self)
        {
            orig(self);

            if (self == null || !NightmareLogicHandler.IsSaveActive) return;

            try
            {
                if (!BreakableWallRandomizerInterop.TryGetPlacementName(self, out string placementName))
                {
                    return;
                }

                string fsmName = self.FsmName;
                if (fsmName == "collapse tute")
                {
                    if (self.TryGetState("Give item", out _))
                    {
                        FsmGateHelper.InterceptCostGate(
                            self, "Idle", "BREAK", "Crumble", "Idle", () => placementName);
                    }
                    return;
                }

                // Only gate FSMs that WallLocation has patched with GiveItem
                if (!self.TryGetState("GiveItem", out _)) return;

                switch (fsmName)
                {
                    case "breakable_wall_v2":
                        PatchBreakableWallV2(self, placementName);
                        break;

                    case "FSM":
                        PatchGenericFsmWall(self, placementName);
                        break;

                    case "break_floor":
                        FsmGateHelper.InterceptCostGate(
                            self, "Hit", "HIT 3", "GiveItem", "Idle", () => placementName);
                        break;

                    case "quake_floor":
                        FsmGateHelper.InterceptCostGate(
                            self, "Transient", "DESTROY", "GiveItem", "Solid", () => placementName);
                        break;

                    case "Detect Quake":
                        FsmGateHelper.InterceptCostGate(
                            self, "Check Quake", "QUAKE", "Hit Pause", "Detect", () => placementName);
                        break;

                    case "collapse small":
                        FsmGateHelper.InterceptCostGate(
                            self, "Idle", "BREAK", "Split", "Idle", () => placementName);
                        break;

                    case "Break":
                        FsmGateHelper.InterceptCostGate(
                            self, "Check", "FINISHED", "GiveItem", "Detect", () => placementName);
                        break;
                }
            }
            catch (Exception ex)
            {
                ModLog.Error($"Failed to patch BreakableWallRandomizer FSM '{self.FsmName}' on '{self.gameObject.name}'", ex);
            }
        }

        private static void PatchBreakableWallV2(PlayMakerFSM fsm, string placementName)
        {
            // Gate before Pause Frame (which calls DestroyObject) and before Spell / Wall Breaker destroy paths
            FsmGateHelper.InterceptCostGate(
                fsm, "Idle", "WALL BREAKER", "GiveItem", "Idle", () => placementName);
            FsmGateHelper.InterceptCostGate(
                fsm, "Check If Nail", "SPELL", "Spell Destroy", "Idle", () => placementName);
            FsmGateHelper.InterceptCostGate(
                fsm, "Hit Right", "HIT", "Pause Frame", "Return Right", () => placementName);
            FsmGateHelper.InterceptCostGate(
                fsm, "Hit Left", "HIT", "Pause Frame", "Return Left", () => placementName);
            FsmGateHelper.InterceptCostGate(
                fsm, "Hit Down", "HIT", "Pause Frame", "Return Down", () => placementName);
            FsmGateHelper.InterceptCostGate(
                fsm, "Hit Up", "HIT", "Pause Frame", "Idle", () => placementName);
        }

        private static void PatchGenericFsmWall(PlayMakerFSM fsm, string placementName)
        {
            // Queen's Gardens glass walls route through No Rotate Check instead of Pause Frame / Spell Destroy
            if (fsm.TryGetState("No Rotate Check", out FsmState noRotate)
                && noRotate.GetTransition("FINISHED")?.ToState == "GiveItem")
            {
                FsmGateHelper.InterceptCostGate(
                    fsm, "No Rotate Check", "FINISHED", "GiveItem", "Idle", () => placementName);
                return;
            }

            FsmGateHelper.InterceptCostGate(
                fsm, "Pause Frame", "FINISHED", "GiveItem", "Idle", () => placementName);
            FsmGateHelper.InterceptCostGate(
                fsm, "Spell Destroy", "FINISHED", "GiveItem", "Idle", () => placementName);
        }
    }
}