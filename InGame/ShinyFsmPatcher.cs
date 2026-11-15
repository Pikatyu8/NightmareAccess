using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HutongGames.PlayMaker;
using ItemChanger;
using ItemChanger.Placements;
using ItemChanger.Util;
using NightmareAccess.Integrations;
using NightmareAccess.Logic;
using Satchel;
using UnityEngine;

namespace NightmareAccess.InGame
{
    /// <summary>Patches "Shiny Control" FSMs so an already satisfied requirement skips the Y/N prompt.</summary>
    internal static class ShinyFsmPatcher
    {
        private const string ShinyFsmName = "Shiny Control";

        private static readonly Dictionary<Type, FieldInfo[]> CostFieldCache = new();

        public static void Hook()
        {
            MethodInfo onEnable = typeof(PlayMakerFSM).GetMethod(
                "OnEnable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            HookUtil.InstallAfterItemChanger(onEnable, typeof(ShinyFsmPatcher), nameof(OnFsmEnableHook));

            MethodInfo giveAll = typeof(AbstractPlacement)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "GiveAll"
                    && m.GetParameters().Length >= 1
                    && m.GetParameters()[0].ParameterType == typeof(GiveInfo));

            if (giveAll != null && giveAll.GetParameters().Length == 2)
            {
                HookUtil.Install(giveAll, typeof(ShinyFsmPatcher), nameof(OnPlacementGiveAllWithCallbackDetour));
            }
            else
            {
                HookUtil.Install(giveAll, typeof(ShinyFsmPatcher), nameof(OnPlacementGiveAllDetour));
            }

            MethodInfo makeMultiShiny = typeof(ShinyUtility).GetMethod(
                nameof(ShinyUtility.MakeNewMultiItemShiny),
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(ContainerInfo) },
                null);
            HookUtil.Install(makeMultiShiny, typeof(ShinyFsmPatcher), nameof(OnMakeMultiShinyDetour));

            MethodInfo makeShiny = typeof(ShinyUtility).GetMethod(
                nameof(ShinyUtility.MakeNewShiny),
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(AbstractPlacement), typeof(AbstractItem), typeof(FlingType) },
                null);
            HookUtil.Install(makeShiny, typeof(ShinyFsmPatcher), nameof(OnMakeShinyDetour));
        }

        private static GameObject OnMakeShinyDetour(
            Func<AbstractPlacement, AbstractItem, FlingType, GameObject> orig,
            AbstractPlacement placement,
            AbstractItem item,
            FlingType flingType)
        {
            GameObject shiny = orig(placement, item, flingType);
            if (NightmareLogicHandler.IsSaveActive && shiny != null && placement != null && item != null)
            {
                ContainerInfo info = ContainerInfo.FindContainerInfo(shiny);
                if (info != null && info.costInfo == null)
                {
                    Cost tagCost = item.GetTag<CostTag>()?.Cost ?? placement.GetTag<CostTag>()?.Cost;
                    if (NightmareAccessCost.Find(tagCost) != null)
                    {
                        info.costInfo = new CostInfo
                        {
                            placement = placement,
                            cost = tagCost,
                            previewItems = new[] { item }
                        };
                    }
                }
            }

            return shiny;
        }

        private static GameObject OnMakeMultiShinyDetour(Func<ContainerInfo, GameObject> orig, ContainerInfo info)
        {
            if (NightmareLogicHandler.IsSaveActive && info != null && info.costInfo == null && info.giveInfo?.placement != null)
            {
                Cost tagCost = info.giveInfo.placement.GetTag<CostTag>()?.Cost;
                if (NightmareAccessCost.Find(tagCost) != null)
                {
                    info.costInfo = new CostInfo
                    {
                        placement = info.giveInfo.placement,
                        cost = tagCost,
                        previewItems = info.giveInfo.items ?? info.giveInfo.placement.Items
                    };
                }
            }

            return orig(info);
        }

        private static bool ShouldBlockAutoGive(AbstractPlacement self, GiveInfo info)
        {
            return IntegrationRegistry.ShouldBlockAutoGive(self, info);
        }

        private static void OnPlacementGiveAllDetour(Action<AbstractPlacement, GiveInfo> orig, AbstractPlacement self, GiveInfo info)
        {
            if (ShouldBlockAutoGive(self, info)) return;
            orig(self, info);
        }

        private static void OnPlacementGiveAllWithCallbackDetour(
            Action<AbstractPlacement, GiveInfo, Action> orig, AbstractPlacement self, GiveInfo info, Action callback)
        {
            if (ShouldBlockAutoGive(self, info))
            {
                callback?.Invoke();
                return;
            }
            orig(self, info, callback);
        }

        private static void OnFsmEnableHook(Action<PlayMakerFSM> orig, PlayMakerFSM self)
        {
            orig(self);

            if (self == null || self.FsmName != ShinyFsmName) return;

            try
            {
                Patch(self);
            }
            catch (Exception ex)
            {
                ModLog.Error("Failed to patch Shiny Control FSM", ex);
            }
        }

        private static void Patch(PlayMakerFSM fsm)
        {
            FsmState promptState = fsm.FsmStates.FirstOrDefault(s => s.Transitions.Any(t => t.EventName == "YES"));
            if (promptState == null) return;

            // OnEnable can run several times on the same FSM; never insert the action twice
            if (promptState.Actions.Any(a => a is FastPickupAction)) return;

            Cost rootCost = FindRootCost(fsm);
            if (NightmareAccessCost.Find(rootCost) == null) return;

            promptState.InsertAction(new FastPickupAction(fsm, rootCost), 0);
        }

        private static Cost FindRootCost(PlayMakerFSM fsm)
        {
            foreach (FsmState state in fsm.FsmStates)
            {
                foreach (FsmStateAction action in state.Actions)
                {
                    if (action == null) continue;

                    foreach (FieldInfo field in GetCostFields(action.GetType()))
                    {
                        if (field.GetValue(action) is Cost cost && NightmareAccessCost.Find(cost) != null)
                        {
                            return cost;
                        }
                    }
                }
            }
            return null;
        }

        private static FieldInfo[] GetCostFields(Type actionType)
        {
            if (!CostFieldCache.TryGetValue(actionType, out FieldInfo[] fields))
            {
                fields = actionType
                    .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(f => f.FieldType.IsAssignableFrom(typeof(Cost)) || typeof(Cost).IsAssignableFrom(f.FieldType))
                    .ToArray();
                CostFieldCache[actionType] = fields;
            }
            return fields;
        }

        private sealed class FastPickupAction : FsmStateAction
        {
            private readonly PlayMakerFSM _fsm;
            private readonly Cost _rootCost;

            public FastPickupAction(PlayMakerFSM fsm, Cost rootCost)
            {
                _fsm = fsm;
                _rootCost = rootCost;
            }

            public override void OnEnter()
            {
                // Skip Y/N prompt only when no consumable payment (e.g. GeoCost) is attached alongside NightmareAccessCost
                if (_rootCost != null && !_rootCost.HasPayEffects() && _rootCost.CanPay())
                {
                    _fsm.SendEvent("YES");
                }
                Finish();
            }
        }
    }
}
