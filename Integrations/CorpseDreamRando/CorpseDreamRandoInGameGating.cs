using System;
using System.Reflection;
using NightmareAccess.InGame;
using NightmareAccess.Interop;
using NightmareAccess.Logic;
using UnityEngine;

namespace NightmareAccess.Integrations.CorpseDreamRando
{
    /// <summary>Runtime FSM gating for CorpseDreamRando npc_dream_dialogue objects.</summary>
    internal static class CorpseDreamRandoInGameGating
    {
        private static bool _hooked;

        public static void Hook()
        {
            if (_hooked) return;
            _hooked = true;

            MethodInfo fsmOnEnable = typeof(PlayMakerFSM).GetMethod(
                "OnEnable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            HookUtil.InstallAfterItemChanger(fsmOnEnable, typeof(CorpseDreamRandoInGameGating), nameof(OnFsmEnableDetour));
        }

        private static void OnFsmEnableDetour(Action<PlayMakerFSM> orig, PlayMakerFSM self)
        {
            orig(self);

            if (self == null || !NightmareLogicHandler.IsSaveActive) return;

            try
            {
                string path = GetHierarchyPath(self.gameObject);
                if (!CorpseDreamRandoInterop.TryGetPlacementName(self.gameObject.scene.name, path, out string placementName))
                {
                    return;
                }

                if (!CorpseDreamRandoInterop.IsPlacementActiveInSave(placementName))
                {
                    return;
                }

                FsmGateHelper.InterceptCostGate(
                    self,
                    "Idle",
                    "HIT",
                    "Take Control",
                    "Idle",
                    () => placementName);
            }
            catch (Exception ex)
            {
                ModLog.Error($"Failed to patch CorpseDreamRando FSM '{self.FsmName}' on '{self.gameObject.name}'", ex);
            }
        }

        private static string GetHierarchyPath(GameObject obj)
        {
            string path = obj.name;
            Transform current = obj.transform.parent;
            while (current != null)
            {
                path = $"{current.name}/{path}";
                current = current.parent;
            }
            return path;
        }
    }
}