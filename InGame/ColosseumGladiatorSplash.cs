using System;
using System.Collections.Generic;
using System.Reflection;
using HutongGames.PlayMaker;
using Satchel;
using UnityEngine;

namespace NightmareAccess.InGame
{
    /// <summary>
    /// Replaces the Colosseum hot spring's global nail-splash broadcast with real droplet hit detection
    /// so Spa Gladiator only covers himself when water droplets physically reach him.
    /// </summary>
    internal static class ColosseumGladiatorSplash
    {
        private const string ColosseumSpaScene = "Room_Colosseum_02";
        private const string SplashWaterEvent = "SPLASH WATER";

        private static bool _hooked;
        private static GladiatorSplashMonitor _activeMonitor;
        private static readonly HashSet<string> WatchedDropletPrefixes = new(StringComparer.Ordinal);

        public static void Hook()
        {
            if (_hooked) return;
            _hooked = true;

            MethodInfo fsmOnEnable = typeof(PlayMakerFSM).GetMethod(
                "OnEnable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            HookUtil.Install(fsmOnEnable, typeof(ColosseumGladiatorSplash), nameof(OnFsmEnableDetour));
        }

        private static void OnFsmEnableDetour(Action<PlayMakerFSM> orig, PlayMakerFSM self)
        {
            orig(self);

            if (self == null || self.gameObject.scene.name != ColosseumSpaScene) return;

            try
            {
                string objName = self.gameObject.name;
                string fsmName = self.FsmName;

                if (objName == "Spa Gladiator" && fsmName == "Control")
                {
                    _activeMonitor = self.gameObject.GetAddComponent<GladiatorSplashMonitor>();
                    _activeMonitor.Bind(self);
                }
                else if (objName == "NailSlash Effects" && fsmName == "Nail Splashes")
                {
                    PatchNailSplashes(self);
                }
                else if (objName == "Spa Region (1)" && fsmName == "Spa Region")
                {
                    PatchSpaRegionSplashes(self);
                }
            }
            catch (Exception ex)
            {
                ModLog.Error($"Failed to patch Colosseum spa splash FSM '{self.FsmName}' on '{self.gameObject.name}'", ex);
            }
        }

        private static void PatchNailSplashes(PlayMakerFSM fsm)
        {
            if (fsm.TryGetState("Check Direction", out FsmState checkDir))
            {
                foreach (FsmStateAction action in checkDir.Actions)
                {
                    if (action == null) continue;
                    string typeName = action.GetType().Name;
                    if (!typeName.Contains("SendEvent")) continue;

                    FieldInfo sendEventField = action.GetType().GetField(
                        "sendEvent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    object val = sendEventField?.GetValue(action);
                    string evName = (val as FsmString)?.Value ?? (val as FsmEvent)?.Name;
                    if (evName == SplashWaterEvent)
                    {
                        action.Enabled = false;
                    }
                }
            }

            HookDropletStates(fsm, "Slash Left", "Slash Right", "Slash Up");
        }

        private static void PatchSpaRegionSplashes(PlayMakerFSM fsm)
        {
            HookDropletStates(fsm, "Enter", "Splash In Big");
        }

        private static void HookDropletStates(PlayMakerFSM fsm, params string[] stateNames)
        {
            foreach (string stateName in stateNames)
            {
                if (!fsm.TryGetState(stateName, out FsmState state)) continue;

                CollectDropletPrefixes(state);

                bool alreadyHooked = false;
                foreach (FsmStateAction act in state.Actions)
                {
                    if (act is SplashWatchAction)
                    {
                        alreadyHooked = true;
                        break;
                    }
                }

                if (!alreadyHooked)
                {
                    state.AddAction(new SplashWatchAction());
                }
            }
        }

        private static void CollectDropletPrefixes(FsmState state)
        {
            foreach (FsmStateAction action in state.Actions)
            {
                if (action == null || action.GetType().Name != "FlingObjectsFromGlobalPool") continue;

                FieldInfo goField = action.GetType().GetField(
                    "gameObject", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (goField?.GetValue(action) is FsmGameObject fgo && fgo.Value != null && !string.IsNullOrEmpty(fgo.Value.name))
                {
                    WatchedDropletPrefixes.Add(fgo.Value.name);
                }
            }
        }

        private sealed class SplashWatchAction : FsmStateAction
        {
            public override void OnEnter()
            {
                _activeMonitor?.NotifySplash();
                Finish();
            }
        }

        private sealed class GladiatorSplashMonitor : MonoBehaviour
        {
            private PlayMakerFSM _fsm;
            private Renderer _renderer;
            private float _watchUntil;
            private float _cooldownUntil;

            public void Bind(PlayMakerFSM fsm)
            {
                _fsm = fsm;
                _renderer = GetComponent<Renderer>();
            }

            public void NotifySplash()
            {
                _watchUntil = Time.time + 1.8f;
            }

            private void Update()
            {
                if (Time.time > _watchUntil || Time.time < _cooldownUntil) return;
                if (_fsm == null || _renderer == null || WatchedDropletPrefixes.Count == 0) return;

                string state = _fsm.ActiveStateName;
                if (state != "Look" && state != "Splashed") return;

                Bounds bodyBounds = _renderer.bounds;
                bodyBounds.Expand(new Vector3(-0.35f, -0.25f, 0f));

                foreach (Rigidbody2D rb in UnityEngine.Object.FindObjectsOfType<Rigidbody2D>())
                {
                    if (rb == null || !rb.gameObject.activeInHierarchy) continue;
                    if (!IsWatchedDroplet(rb.gameObject.name)) continue;

                    Vector2 pos = rb.position;
                    if (pos.x >= bodyBounds.min.x && pos.x <= bodyBounds.max.x
                        && pos.y >= bodyBounds.min.y && pos.y <= bodyBounds.max.y)
                    {
                        _cooldownUntil = Time.time + 0.35f;
                        _fsm.SendEvent(SplashWaterEvent);
                        break;
                    }
                }
            }

            private static bool IsWatchedDroplet(string objName)
            {
                foreach (string prefix in WatchedDropletPrefixes)
                {
                    if (objName.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }
}