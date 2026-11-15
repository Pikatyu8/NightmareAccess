using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ItemChanger;
using ItemChanger.Internal;
using ItemChanger.UIDefs;
using NightmareAccess.Logic;
using NightmareAccess.Settings;
using UnityEngine;

namespace NightmareAccess.InGame
{
    /// <summary>Shared helper for in-place gated objects that display a corner requirement popup when locked.</summary>
    internal static class CornerPromptHelper
    {
        private const float DefaultCooldownSeconds = 3.0f;
        private static readonly Dictionary<string, float> CooldownUntilByPlacement = new(StringComparer.Ordinal);

        internal static PlayerData PlayerDataOverrideForTests { get; set; }
        internal static IReadOnlyDictionary<string, AbstractPlacement> PlacementsOverrideForTests { get; set; }
        internal static Action<string, ISprite> SendCornerMessageOverrideForTests { get; set; }

        private static IReadOnlyDictionary<string, AbstractPlacement> GetPlacements()
        {
            if (PlacementsOverrideForTests != null) return PlacementsOverrideForTests;
            if (NightmareAccess.Instance == null) return null;
            return GetRefPlacements();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IReadOnlyDictionary<string, AbstractPlacement> GetRefPlacements() =>
            Ref.Settings?.Placements;

        public static AccessCondition GetConditionForPlacement(string placementName, string fallbackGroup = null)
        {
            if (string.IsNullOrEmpty(placementName)) return null;

            if (NightmareLogicHandler.ActiveScope == RequirementScope.PerObjectGroup)
            {
                string groupKey = fallbackGroup;
                var placements = GetPlacements();
                if (placements != null && placements.TryGetValue(placementName, out AbstractPlacement placement))
                {
                    AbstractItem firstItem = placement.Items.FirstOrDefault();
                    if (firstItem != null)
                    {
                        groupKey = NightmareLogicHandler.GetItemTypeGroup(firstItem.name);
                    }
                }

                if (string.IsNullOrEmpty(groupKey)) return null;
                return NightmareLogicHandler.ItemTypeConditions.TryGetValue(groupKey, out var groupCond)
                    ? groupCond
                    : null;
            }

            if (NightmareLogicHandler.ActiveScope == RequirementScope.PerObject)
            {
                return NightmareLogicHandler.ObjectConditions.TryGetValue(placementName, out var objCond)
                    ? objCond
                    : null;
            }

            return NightmareLogicHandler.LocationConditions.TryGetValue(placementName, out var locCond)
                ? locCond
                : null;
        }

        public static bool CheckOrShowLocked(
            GameObject hostObj,
            string placementName,
            string fallbackName,
            ISprite fallbackSprite = null)
        {
            return CheckOrShowLocked(placementName, fallbackName, fallbackSprite);
        }

        public static bool CheckOrShowLocked(
            string placementName,
            string fallbackName,
            ISprite fallbackSprite = null)
        {
            if (!NightmareLogicHandler.IsSaveActive) return true;
            if (NightmareLogicHandler.IsExcludedLocation(placementName)) return true;

            var placements = GetPlacements();
            if (placements != null
                && placements.TryGetValue(placementName, out AbstractPlacement existing)
                && existing.Items.Count > 0
                && existing.AllObtained())
            {
                return true;
            }

            AccessCondition condition = GetConditionForPlacement(placementName);
            if (condition == null) return true;

            PlayerData pd = PlayerDataOverrideForTests ?? PlayerData.instance;
            if (condition.IsSatisfied(pd, out int current))
            {
                ModLog.Debug($"[InPlaceGate] Unlocked '{placementName}' (satisfied: {condition.GetRequirementText()}, current={current})");
                return true;
            }

            if (!TryAcquireCooldown(placementName))
            {
                return false;
            }

            ModLog.Debug($"[InPlaceGate] Blocked '{placementName}' (requires: {condition.GetRequirementText()}, current={current})");
            ShowLockedMessage(placementName, condition, current, fallbackName, fallbackSprite);
            return false;
        }

        private static bool TryAcquireCooldown(string placementName)
        {
            if (string.IsNullOrEmpty(placementName)) return true;
            if (NightmareAccess.Instance == null) return true;

            return TryAcquireUnityCooldown(placementName);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryAcquireUnityCooldown(string placementName)
        {
            float now = Time.realtimeSinceStartup;
            if (CooldownUntilByPlacement.TryGetValue(placementName, out float until)
                && now < until
                && until - now <= DefaultCooldownSeconds)
            {
                return false;
            }

            CooldownUntilByPlacement[placementName] = now + DefaultCooldownSeconds;
            return true;
        }

        public static void ShowLockedMessage(
            string placementName,
            AccessCondition condition,
            int current,
            string fallbackName,
            ISprite fallbackSprite = null)
        {
            if (NightmareAccess.Instance == null && SendCornerMessageOverrideForTests == null) return;

            AbstractItem firstItem = null;
            var placements = GetPlacements();
            if (placements != null && placements.TryGetValue(placementName, out AbstractPlacement placement))
            {
                firstItem = placement.Items.FirstOrDefault();
            }

            string itemName = GetItemDisplayName(firstItem, fallbackName);
            ISprite itemSprite = GetItemSprite(firstItem, fallbackSprite);

            string req = condition.GetRequirementText();
            string suffix = condition.IsProgressive()
                ? $" (have: Lv{current})"
                : condition.IsNumeric()
                    ? $" (have: {current})"
                    : "";

            string msg = $"{itemName}: {req}{suffix}";

            if (SendCornerMessageOverrideForTests != null)
            {
                SendCornerMessageOverrideForTests(msg, itemSprite);
                return;
            }

            SendCornerMessage(msg, itemSprite);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SendCornerMessage(string msg, ISprite itemSprite)
        {
            var cornerMsg = new MsgUIDef
            {
                name = new BoxedString(msg),
                sprite = itemSprite ?? new ItemChangerSprite("ShopIcons.Geo")
            };
            cornerMsg.SendMessage(MessageType.Corner, null);
        }

        private static string GetItemDisplayName(AbstractItem item, string fallbackName)
        {
            if (item != null)
            {
                try
                {
                    string uiName = item.UIDef?.GetPreviewName();
                    if (!string.IsNullOrEmpty(uiName)) return uiName;
                }
                catch (Exception ex)
                {
                    ModLog.Debug($"Failed to read UIDef name for item '{item.name}': {ex.Message}");
                }

                if (!string.IsNullOrEmpty(item.name))
                {
                    return item.name.Replace('_', ' ');
                }
            }

            return string.IsNullOrEmpty(fallbackName) ? "Item" : fallbackName;
        }

        private static ISprite GetItemSprite(AbstractItem item, ISprite fallbackSprite)
        {
            if (item?.UIDef is MsgUIDef msgUi && msgUi.sprite != null)
            {
                return msgUi.sprite;
            }

            return fallbackSprite;
        }
    }
}
