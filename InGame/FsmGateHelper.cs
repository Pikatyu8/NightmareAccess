using System;
using HutongGames.PlayMaker;
using Satchel;
using Satchel.Futils;
using UnityEngine;

namespace NightmareAccess.InGame
{
    /// <summary>Shared Satchel-backed helper for intercepting PlayMaker FSM transitions with Nightmare Access requirements.</summary>
    internal static class FsmGateHelper
    {
        public static void InterceptCostGate(
            PlayMakerFSM fsm,
            string fromState,
            string eventName,
            string fallbackDefaultTarget,
            string customBlockState,
            Func<string> placementNameResolver,
            Func<string> fallbackDisplayResolver = null,
            Func<GameObject> hostResolver = null)
        {
            if (fsm == null) return;
            if (!fsm.TryGetState(fromState, out FsmState state)) return;
            if (!fsm.TryGetState(customBlockState, out _)) return;

            FsmTransition origTransition = state.GetTransition(eventName);
            if (origTransition == null) return;

            string defaultTarget = !string.IsNullOrEmpty(origTransition.ToState)
                ? origTransition.ToState
                : fallbackDefaultTarget;
            if (!fsm.TryGetState(defaultTarget, out _)) return;

            var interceptor = new TransitionInterceptor
            {
                fromState = fromState,
                eventName = eventName,
                toStateDefault = defaultTarget,
                toStateCustom = customBlockState
            };

            if (fsm.HasIntercept(interceptor)) return;

            interceptor.shouldIntercept = () =>
            {
                string placementName = placementNameResolver();
                if (string.IsNullOrEmpty(placementName)) return false;

                string fallback = fallbackDisplayResolver?.Invoke() ?? FormatPlacementName(placementName);
                GameObject host = hostResolver?.Invoke() ?? fsm.gameObject;
                return !CornerPromptHelper.CheckOrShowLocked(host, placementName, fallback);
            };

            fsm.Intercept(interceptor);
        }

        public static FsmStateAction CreateDelayAction(float durationSeconds) =>
            new DelayAction(durationSeconds);

        public static string FormatPlacementName(string placementName)
        {
            return string.IsNullOrEmpty(placementName)
                ? "Item"
                : placementName.Replace('_', ' ').Replace("-", " - ");
        }

        private sealed class DelayAction : FsmStateAction
        {
            private readonly float _duration;
            private float _elapsed;

            public DelayAction(float duration)
            {
                _duration = duration;
            }

            public override void OnEnter()
            {
                _elapsed = 0f;
            }

            public override void OnUpdate()
            {
                _elapsed += Time.deltaTime;
                if (_elapsed >= _duration)
                {
                    Finish();
                }
            }
        }
    }
}