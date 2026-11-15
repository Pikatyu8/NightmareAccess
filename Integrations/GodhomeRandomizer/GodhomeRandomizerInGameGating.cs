using System;
using System.Reflection;
using HutongGames.PlayMaker;
using ItemChanger;
using NightmareAccess.InGame;
using NightmareAccess.Interop;
using NightmareAccess.Logic;
using Satchel;

namespace NightmareAccess.Integrations.GodhomeRandomizer
{
    /// <summary>Runtime FSM gating for GodhomeRandomizer LifebloodLocation and OrdealLocation.</summary>
    internal static class GodhomeRandomizerInGameGating
    {
        private static bool _hooked;

        public static void Hook()
        {
            if (_hooked) return;
            _hooked = true;

            MethodInfo fsmOnEnable = typeof(PlayMakerFSM).GetMethod(
                "OnEnable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            HookUtil.InstallAfterItemChanger(fsmOnEnable, typeof(GodhomeRandomizerInGameGating), nameof(OnFsmEnableDetour));
        }

        private static void OnFsmEnableDetour(Action<PlayMakerFSM> orig, PlayMakerFSM self)
        {
            orig(self);

            if (self == null || !NightmareLogicHandler.IsSaveActive) return;

            try
            {
                string sceneName = self.gameObject.scene.name;
                string objName = self.gameObject.name;
                string fsmName = self.FsmName;

                if (sceneName == SceneNames.GG_Blue_Room
                    && objName == "gg_blue_core"
                    && fsmName == "Dream React"
                    && GodhomeRandomizerInterop.IsPlacementActiveInSave(GodhomeRandomizerIntegration.LifebloodName)
                    && self.TryGetState("GiveItem", out _))
                {
                    FsmGateHelper.InterceptCostGate(
                        self,
                        "Regain Control",
                        "FINISHED",
                        "GiveItem",
                        "Disable",
                        () => GodhomeRandomizerIntegration.LifebloodName,
                        () => "Godhome Lifeblood");
                }
                else if (sceneName == "GG_Mighty_Zote"
                         && objName == "Battle Control"
                         && fsmName == "Control"
                         && GodhomeRandomizerInterop.IsPlacementActiveInSave(GodhomeRandomizerIntegration.OrdealName)
                         && self.TryGetState("GiveItem", out FsmState giveItem))
                {
                    string resumeState = giveItem.GetTransition("FINISHED")?.ToState ?? "Add Warrior 3";
                    FsmGateHelper.InterceptCostGate(
                        self,
                        "Add Zoteling 2",
                        "NEXT",
                        "GiveItem",
                        resumeState,
                        () => GodhomeRandomizerIntegration.OrdealName,
                        () => "Eternal Ordeal");
                }
            }
            catch (Exception ex)
            {
                ModLog.Error($"Failed to patch GodhomeRandomizer FSM '{self.FsmName}' on '{self.gameObject.name}'", ex);
            }
        }
    }
}