using System;
using System.Collections.Generic;
using System.Reflection;
using NightmareAccess.InGame;
using NightmareAccess.Interop;
using NightmareAccess.Logic;
using Satchel;

namespace NightmareAccess.Integrations.RandoPlus
{
    /// <summary>Runtime FSM gating for RandoPlus Ghost_Essence-* ghost_npc_death objects.</summary>
    internal static class RandoPlusInGameGating
    {
        // Exact (SceneName, ObjectName) -> Ghost_Essence-* mapping from RandoPlus ghostdata.json
        private static readonly Dictionary<(string SceneName, string ObjectName), string> GhostMap = new()
        {
            [("Town", "Gravedigger NPC")] = "Ghost_Essence-Gravedigger",
            [("Hive_05", "Vespa NPC")] = "Ghost_Essence-Vespa",
            [("Cliffs_05", "Ghost NPC Joni")] = "Ghost_Essence-Joni",
            [("Ruins_Bathhouse", "Ghost NPC")] = "Ghost_Essence-Marissa",
            [("Ruins_Elevator", "Ghost NPC")] = "Ghost_Essence-Poggy_Thorax",
            [("Fungus1_24", "Ghost NPC")] = "Ghost_Essence-Caelif_Fera_Orthop",
            [("Fungus3_23", "Cloth Ghost NPC")] = "Ghost_Essence-Cloth",
            [("RestingGrounds_08", "Ghost revek")] = "Ghost_Essence-Revek",
            [("RestingGrounds_08", "Ghost milly")] = "Ghost_Essence-Millybug",
            [("RestingGrounds_08", "Ghost caspian")] = "Ghost_Essence-Caspian",
            [("RestingGrounds_08", "Ghost atra")] = "Ghost_Essence-Atra",
            [("RestingGrounds_08", "Ghost chagax")] = "Ghost_Essence-Dr_Chagax",
            [("RestingGrounds_08", "Ghost garro")] = "Ghost_Essence-Garro",
            [("RestingGrounds_08", "Ghost kcin")] = "Ghost_Essence-Kcin",
            [("RestingGrounds_08", "karina")] = "Ghost_Essence-Karina",
            [("RestingGrounds_08", "Ghost NPC 100 nail")] = "Ghost_Essence-Hundred_Nail_Warrior",
            [("RestingGrounds_08", "Ghost grohac")] = "Ghost_Essence-Grohac",
            [("RestingGrounds_08", "Ghost perpetos")] = "Ghost_Essence-Perpetos_Noo",
            [("RestingGrounds_08", "Ghost molten")] = "Ghost_Essence-Molten",
            [("RestingGrounds_08", "Ghost magnus")] = "Ghost_Essence-Magnus_Strong",
            [("RestingGrounds_08", "Ghost waldie")] = "Ghost_Essence-Waldie",
            [("RestingGrounds_08", "Ghost wayner")] = "Ghost_Essence-Wayner",
            [("RestingGrounds_08", "Ghost wyatt")] = "Ghost_Essence-Wyatt",
            [("RestingGrounds_08", "Ghost hex")] = "Ghost_Essence-Hex",
            [("RestingGrounds_08", "Ghost thistlewind")] = "Ghost_Essence-Thistlewind",
            [("RestingGrounds_08", "Ghost boss")] = "Ghost_Essence-Boss"
        };

        private static bool _hooked;

        public static void Hook()
        {
            if (_hooked) return;
            _hooked = true;

            MethodInfo fsmOnEnable = typeof(PlayMakerFSM).GetMethod(
                "OnEnable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            HookUtil.InstallAfterItemChanger(fsmOnEnable, typeof(RandoPlusInGameGating), nameof(OnFsmEnableDetour));
        }

        private static void OnFsmEnableDetour(Action<PlayMakerFSM> orig, PlayMakerFSM self)
        {
            orig(self);

            if (self == null || !NightmareLogicHandler.IsSaveActive || self.FsmName != "ghost_npc_death")
            {
                return;
            }

            try
            {
                if (!GhostMap.TryGetValue((self.gameObject.scene.name, self.gameObject.name), out string placementName))
                {
                    return;
                }

                if (!RandoPlusInterop.IsPlacementActiveInSave(placementName))
                {
                    return;
                }

                // Intercept Dream Nail before Revek? / Impact so neither reward nor ghost death triggers while locked
                if (self.TryGetState("Idle", out _))
                {
                    FsmGateHelper.InterceptCostGate(
                        self,
                        "Idle",
                        "DREAMNAIL",
                        "Revek?",
                        "Idle",
                        () => placementName);
                }
            }
            catch (Exception ex)
            {
                ModLog.Error($"Failed to patch RandoPlus ghost FSM '{self.FsmName}' on '{self.gameObject.name}'", ex);
            }
        }
    }
}