using System;
using System.Reflection;
using ItemChanger;
using NightmareAccess.InGame;
using NightmareAccess.Logic;
using Satchel;

namespace NightmareAccess.Integrations.RopeRando
{
    /// <summary>Runtime FSM gating for the Watcher Knights chandelier rope in Ruins2_03.</summary>
    internal static class RopeRandoInGameGating
    {
        private static bool _hooked;

        public static void Hook()
        {
            if (_hooked) return;
            _hooked = true;

            MethodInfo fsmOnEnable = typeof(PlayMakerFSM).GetMethod(
                "OnEnable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            HookUtil.InstallAfterItemChanger(fsmOnEnable, typeof(RopeRandoInGameGating), nameof(OnFsmEnableDetour));
        }

        private static void OnFsmEnableDetour(Action<PlayMakerFSM> orig, PlayMakerFSM self)
        {
            orig(self);

            if (self == null || !NightmareLogicHandler.IsSaveActive) return;

            try
            {
                if (self.FsmName != "Control"
                    || self.gameObject.name != "chandelier"
                    || self.gameObject.scene.name != SceneNames.Ruins2_03)
                {
                    return;
                }

                if (!self.TryGetState("GiveItem", out _)) return;

                FsmGateHelper.InterceptCostGate(
                    self,
                    "Hit",
                    "DESTROY",
                    "GiveItem",
                    "Idle",
                    () => RopeRandoIntegration.LocationName,
                    () => "Watcher Knights Chandelier");
            }
            catch (Exception ex)
            {
                ModLog.Error($"Failed to patch RopeRando FSM '{self.FsmName}' on '{self.gameObject.name}'", ex);
            }
        }
    }
}