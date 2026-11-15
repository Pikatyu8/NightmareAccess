using System;
using System.Reflection;
using HutongGames.PlayMaker;
using ItemChanger;
using NightmareAccess.InGame;
using NightmareAccess.Interop;
using NightmareAccess.Logic;
using Satchel;
using UnityEngine;

namespace NightmareAccess.Integrations.YARC
{
    /// <summary>Runtime gating hooks for YARC in-place AutoLocations (Stalactites, Vines, Chain, Hive Platforms, Scarecrow, Telescope).</summary>
    internal static class YarcInGameGating
    {
        private static readonly FieldInfo StalactiteFallenField =
            typeof(StalactiteControl).GetField("fallen", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo VineActivatedField =
            typeof(VinePlatformCut).GetField("activated", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo VineColliderField =
            typeof(VinePlatformCut).GetField("col", BindingFlags.Instance | BindingFlags.NonPublic);

        private static bool _hooked;

        public static void Hook()
        {
            if (_hooked) return;
            _hooked = true;

            MethodInfo stalactiteHandleTrigger = typeof(StalactiteControl).GetMethod(
                "HandleTriggerEnter", BindingFlags.Instance | BindingFlags.NonPublic);
            HookUtil.Install(stalactiteHandleTrigger, typeof(YarcInGameGating), nameof(OnStalactiteHandleTriggerDetour));

            MethodInfo stalactiteTrigger2D = typeof(StalactiteControl).GetMethod(
                "OnTriggerEnter2D", BindingFlags.Instance | BindingFlags.NonPublic);
            HookUtil.Install(stalactiteTrigger2D, typeof(YarcInGameGating), nameof(OnStalactiteTrigger2DDetour));

            MethodInfo vineTrigger2D = typeof(VinePlatformCut).GetMethod(
                "OnTriggerEnter2D", BindingFlags.Instance | BindingFlags.NonPublic);
            HookUtil.Install(vineTrigger2D, typeof(YarcInGameGating), nameof(OnVineTrigger2DDetour));

            MethodInfo fsmOnEnable = typeof(PlayMakerFSM).GetMethod(
                "OnEnable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            HookUtil.InstallAfterItemChanger(fsmOnEnable, typeof(YarcInGameGating), nameof(OnFsmEnableDetour));
        }

        // ------------------------------------------------------------------
        // Stalactites
        // ------------------------------------------------------------------

        private static void OnStalactiteHandleTriggerDetour(
            Action<StalactiteControl, Collider2D, GameObject> orig,
            StalactiteControl self,
            Collider2D collider,
            GameObject sender)
        {
            if (self != null
                && NightmareLogicHandler.IsSaveActive
                && YarcInterop.IsStalactitesRandomized()
                && collider != null
                && collider.tag == "Player"
                && Physics2D.Linecast(self.transform.position, collider.transform.position, 256).collider == null
                && YarcInterop.TryGetStalactitePlacementName(self.gameObject.scene.name, self.gameObject.name, out string placementName))
            {
                if (!CornerPromptHelper.CheckOrShowLocked(
                        self.gameObject, placementName, FsmGateHelper.FormatPlacementName(placementName)))
                {
                    return;
                }
            }

            orig(self, collider, sender);
        }

        private static void OnStalactiteTrigger2DDetour(
            Action<StalactiteControl, Collider2D> orig,
            StalactiteControl self,
            Collider2D collision)
        {
            if (self != null
                && NightmareLogicHandler.IsSaveActive
                && YarcInterop.IsStalactitesRandomized()
                && collision != null
                && collision.tag == "Nail Attack"
                && !(StalactiteFallenField != null && (bool)StalactiteFallenField.GetValue(self))
                && YarcInterop.TryGetStalactitePlacementName(self.gameObject.scene.name, self.gameObject.name, out string placementName))
            {
                if (!CornerPromptHelper.CheckOrShowLocked(
                        self.gameObject, placementName, FsmGateHelper.FormatPlacementName(placementName)))
                {
                    return;
                }
            }

            orig(self, collision);
        }

        // ------------------------------------------------------------------
        // Vines
        // ------------------------------------------------------------------

        private static void OnVineTrigger2DDetour(
            Action<VinePlatformCut, Collider2D> orig,
            VinePlatformCut self,
            Collider2D collision)
        {
            if (self != null
                && NightmareLogicHandler.IsSaveActive
                && YarcInterop.IsVinesRandomized()
                && collision != null
                && collision.tag == "Nail Attack"
                && IsValidVineSlash(self, collision))
            {
                string parentName = self.transform.parent?.parent?.name;
                if (!string.IsNullOrEmpty(parentName)
                    && YarcInterop.TryGetVinePlacementName($"{self.gameObject.scene.name}/{parentName}", out string placementName)
                    && !CornerPromptHelper.CheckOrShowLocked(
                        self.gameObject, placementName, FsmGateHelper.FormatPlacementName(placementName)))
                {
                    return;
                }
            }

            orig(self, collision);
        }

        private static bool IsValidVineSlash(VinePlatformCut vine, Collider2D collision)
        {
            if (VineActivatedField != null && (bool)VineActivatedField.GetValue(vine))
            {
                return false;
            }

            Collider2D col = VineColliderField?.GetValue(vine) as Collider2D ?? vine.GetComponent<Collider2D>();
            if (col != null)
            {
                float y = collision.transform.position.y;
                if (y < col.bounds.min.y || y > col.bounds.max.y)
                {
                    return false;
                }
            }

            PlayMakerFSM damager = PlayMakerFSM.FindFsmOnGameObject(collision.gameObject, "damages_enemy");
            if (damager != null)
            {
                float direction = damager.FsmVariables.FindFsmFloat("direction").Value;
                if (direction >= 45f && (direction < 135f || (direction >= 225f && direction < 360f)))
                {
                    return false;
                }
            }

            return true;
        }

        // ------------------------------------------------------------------
        // FSM-driven objects (Chain, Hive Platforms, Scarecrow, Telescope)
        // ------------------------------------------------------------------

        private static void OnFsmEnableDetour(Action<PlayMakerFSM> orig, PlayMakerFSM self)
        {
            orig(self);

            if (self == null || !NightmareLogicHandler.IsSaveActive) return;

            try
            {
                string fsmName = self.FsmName;
                if (fsmName == "Vine Cut")
                {
                    PatchChainVineCutFsm(self);
                }
                else if (fsmName == "Smash")
                {
                    PatchHivePlatformSmashFsm(self);
                }
                else if (fsmName == "Check" && self.gameObject.name == "Smash Checker")
                {
                    PatchHiveSmashCheckerFsm(self);
                }
                else if (fsmName == "Summon" && self.gameObject.name == "Giant Hopper Summon")
                {
                    PatchScarecrowSummonFsm(self);
                }
                else if (fsmName == "npc_control" && self.gameObject.name == "Telescope Inspect")
                {
                    PatchTelescopeNpcControlFsm(self);
                }
            }
            catch (Exception ex)
            {
                ModLog.Error($"Failed to patch YARC FSM '{self.FsmName}' on '{self.gameObject.name}'", ex);
            }
        }

        private static void PatchChainVineCutFsm(PlayMakerFSM fsm)
        {
            if (!YarcInterop.IsVinesRandomized()) return;
            if (fsm.gameObject.scene.name != SceneNames.Ruins1_28) return;
            if (fsm.transform.parent == null || fsm.transform.parent.name != "Chain Platform") return;

            FsmGateHelper.InterceptCostGate(
                fsm, "Check", "LEFT", "Left", "Idle",
                () => YarcIntegration.ChainLocation, () => "Chain - Storerooms");
            FsmGateHelper.InterceptCostGate(
                fsm, "Check", "RIGHT", "Right", "Idle",
                () => YarcIntegration.ChainLocation, () => "Chain - Storerooms");
        }

        private static void PatchHivePlatformSmashFsm(PlayMakerFSM fsm)
        {
            if (!YarcInterop.IsHivePlatformsRandomized()) return;

            string key = $"{fsm.gameObject.scene.name}/{fsm.gameObject.name}";
            if (!YarcInterop.TryGetHivePlatPlacementName(key, out string placementName)) return;

            FsmGateHelper.InterceptCostGate(
                fsm, "Idle", "HIT", "Smash", "Idle",
                () => placementName);
        }

        private static void PatchHiveSmashCheckerFsm(PlayMakerFSM fsm)
        {
            if (!YarcInterop.IsHivePlatformsRandomized()) return;
            if (fsm.transform.parent == null) return;
            if (fsm.TryGetState("NA Cooldown", out _)) return;

            string parentKey = $"{fsm.gameObject.scene.name}/{fsm.transform.parent.name}";
            if (!YarcInterop.TryGetHivePlatPlacementName(parentKey, out _)) return;

            if (!fsm.TryGetState("Detect", out _) || !fsm.TryGetState("Send", out FsmState send)) return;

            // Reset Smash Checker back to Detect after a short delay when the platform remains unbroken
            FsmState cooldown = fsm.AddState("NA Cooldown");
            cooldown.AddTransition("FINISHED", "Detect");
            cooldown.AddAction(FsmGateHelper.CreateDelayAction(0.35f));

            if (send.GetTransition("FINISHED") == null)
            {
                send.AddTransition("FINISHED", "NA Cooldown");
            }
        }

        private static void PatchScarecrowSummonFsm(PlayMakerFSM fsm)
        {
            if (!YarcInterop.IsScarecrowRandomized()) return;

            FsmGateHelper.InterceptCostGate(
                fsm, "Idle", "SUMMON", "Antic", "Idle",
                () => YarcIntegration.ScarecrowLocation, () => YarcIntegration.ScarecrowLocation);
        }

        private static void PatchTelescopeNpcControlFsm(PlayMakerFSM fsm)
        {
            if (!YarcInterop.IsTelescopeRandomized()) return;

            FsmGateHelper.InterceptCostGate(
                fsm, "Can Talk?", "FINISHED", "Take Control", "Cancel Frame",
                () => YarcIntegration.TelescopeLocation, () => "Lurien's Telescope");
        }
    }
}