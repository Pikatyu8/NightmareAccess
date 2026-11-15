using System;
using System.Collections.Generic;
using System.Reflection;
using ItemChanger;
using NightmareAccess.InGame;
using NightmareAccess.Interop;
using NightmareAccess.Logic;
using Satchel;

namespace NightmareAccess.Integrations.FlowerRandomizer
{
    /// <summary>
    /// Runtime FSM gating for all 13 FlowerRandomizer quest NPCs.
    /// Intercepts flower hand-in paths before PlayerData.hasXunFlower or NPC flags are mutated.
    /// </summary>
    internal static class FlowerRandomizerInGameGating
    {
        internal sealed class GateRoute
        {
            public string PlacementName;
            public string DisplayName;
            public string FromState;
            public string EventName;
            public string DefaultTarget;
            public string CustomBlockState;
            public string RequiredState;
        }

        private static readonly Dictionary<(string Scene, string Obj), GateRoute> ConversationRoutes = new()
        {
            [(SceneNames.Town, "Elderbug")] = new GateRoute
            {
                PlacementName = "Flower_Quest-Elderbug",
                DisplayName = "Flower Quest - Elderbug",
                FromState = "Send Text",
                EventName = "YES",
                DefaultTarget = "Accept",
                CustomBlockState = "Decline"
            },
            [(SceneNames.Room_nailmaster_03, "NM Oro NPC")] = new GateRoute
            {
                PlacementName = "Flower_Quest-Oro",
                DisplayName = "Flower Quest - Oro",
                FromState = "Send Text 2",
                EventName = "YES",
                DefaultTarget = "Yes 2",
                CustomBlockState = "Decline Pause 2"
            },
            [(SceneNames.GG_Waterways, "Godseeker Awake")] = new GateRoute
            {
                PlacementName = "Flower_Quest-Godseeker",
                DisplayName = "Flower Quest - Godseeker",
                FromState = "Send Text",
                EventName = "YES",
                DefaultTarget = "Yes",
                CustomBlockState = "Decline Pause"
            },
            [(SceneNames.Ruins_House_03, "Emilitia NPC")] = new GateRoute
            {
                PlacementName = "Flower_Quest-Emilitia",
                DisplayName = "Flower Quest - Emilitia",
                FromState = "Send Text 2",
                EventName = "YES",
                DefaultTarget = "Yes 2",
                CustomBlockState = "Decline Pause 2"
            },
            [(SceneNames.Room_Queen, "Queen")] = new GateRoute
            {
                PlacementName = "Flower_Quest-White_Lady",
                DisplayName = "Flower Quest - White Lady",
                FromState = "Send Text 2",
                EventName = "YES",
                DefaultTarget = "Yes 2",
                CustomBlockState = "Decline Pause 2"
            },
            [(SceneNames.Room_nailmaster, "NM Mato NPC")] = CustomRoute("Flower_Quest-Mato", "Flower Quest - Mato"),
            [(SceneNames.Room_nailmaster_02, "NM Sheo NPC")] = CustomRoute("Flower_Quest-Sheo", "Flower Quest - Sheo"),
            [(SceneNames.Room_nailmaster_02, "NM Sheo NPC Modeller")] = CustomRoute("Flower_Quest-Sheo", "Flower Quest - Sheo"),
            [("Ruins_Bathhouse", "Ghost NPC")] = CustomRoute("Flower_Quest-Marissa", "Flower Quest - Marissa"),
            [("Deepnest_41", "NPC")] = CustomRoute("Flower_Quest-Midwife", "Flower Quest - Midwife"),
            [(SceneNames.White_Palace_20, "End Scene")] = new GateRoute
            {
                PlacementName = "Flower_Quest-Pain",
                DisplayName = "Flower Quest - Path of Pain",
                FromState = "Flower?",
                EventName = "FLOWER",
                DefaultTarget = "Give Flower",
                CustomBlockState = "Disappear Scne",
                RequiredState = "Give Flower"
            }
        };

        private static readonly Dictionary<string, GateRoute> DreamRoutes = new(StringComparer.Ordinal)
        {
            ["Waterways_13"] = DreamRoute("Flower_Quest-Isma", "Flower Quest - Isma"),
            ["Mines_34"] = DreamRoute("Flower_Quest-Radiance", "Flower Quest - Radiance"),
            [SceneNames.White_Palace_09] = DreamRoute("Flower_Quest-Pale_King", "Flower Quest - Pale King")
        };

        private static bool _hooked;

        public static void Hook()
        {
            if (_hooked) return;
            _hooked = true;

            MethodInfo fsmOnEnable = typeof(PlayMakerFSM).GetMethod(
                "OnEnable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            HookUtil.InstallAfterItemChanger(fsmOnEnable, typeof(FlowerRandomizerInGameGating), nameof(OnFsmEnableDetour));
        }

        internal static bool TryResolveGateRoute(string sceneName, string objName, string fsmName, out GateRoute route)
        {
            if (fsmName == "Conversation Control")
            {
                return ConversationRoutes.TryGetValue((sceneName, objName), out route);
            }

            if (fsmName == "npc_dream_dialogue" && objName == "Dream Dialogue")
            {
                return DreamRoutes.TryGetValue(sceneName, out route);
            }

            route = null;
            return false;
        }

        private static GateRoute CustomRoute(string placementName, string displayName) => new()
        {
            PlacementName = placementName,
            DisplayName = displayName,
            FromState = "Box Up",
            EventName = "FLOWER",
            DefaultTarget = "Flower Convo",
            CustomBlockState = "Convo Choice",
            RequiredState = "Flower Convo"
        };

        private static GateRoute DreamRoute(string placementName, string displayName) => new()
        {
            PlacementName = placementName,
            DisplayName = displayName,
            FromState = "Box Up",
            EventName = "FLOWER",
            DefaultTarget = "Flower Convo",
            CustomBlockState = "Convo",
            RequiredState = "Flower Convo"
        };

        private static void OnFsmEnableDetour(Action<PlayMakerFSM> orig, PlayMakerFSM self)
        {
            orig(self);

            if (self == null || !NightmareLogicHandler.IsSaveActive) return;

            try
            {
                if (!TryResolveGateRoute(self.gameObject.scene.name, self.gameObject.name, self.FsmName, out GateRoute route))
                {
                    return;
                }

                if (!FlowerRandomizerInterop.IsPlacementActiveInSave(route.PlacementName)) return;
                if (!string.IsNullOrEmpty(route.RequiredState) && !self.TryGetState(route.RequiredState, out _)) return;

                FsmGateHelper.InterceptCostGate(
                    self,
                    route.FromState,
                    route.EventName,
                    route.DefaultTarget,
                    route.CustomBlockState,
                    () => route.PlacementName,
                    () => route.DisplayName);
            }
            catch (Exception ex)
            {
                ModLog.Error($"Failed to patch FlowerRandomizer FSM '{self.FsmName}' on '{self.gameObject.name}'", ex);
            }
        }
    }
}
