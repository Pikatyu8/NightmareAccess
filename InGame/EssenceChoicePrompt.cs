using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ItemChanger;
using ItemChanger.Internal;
using ItemChanger.Internal.Preloaders;
using ItemChanger.UIDefs;
using NightmareAccess.Integrations;
using NightmareAccess.Logic;
using NightmareAccess.Settings;
using Satchel;
using UnityEngine;

namespace NightmareAccess.InGame
{
    public static class EssenceChoicePrompt
    {
        private const float ExitCooldownSeconds = 3.0f;

        private static readonly FieldInfo PickedUpField =
            typeof(DreamPlantOrb).GetField("pickedUp", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CanPickupField =
            typeof(DreamPlantOrb).GetField("canPickup", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo IsActiveField =
            typeof(DreamPlantOrb).GetField("isActive", BindingFlags.Instance | BindingFlags.NonPublic);

        private static bool _capturingPopup;
        private static GameObject _capturedPopup;

        public static void Hook()
        {
            MethodInfo targetMethod = typeof(DreamPlantOrb).GetMethod(
                "OnTriggerEnter2D", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            HookUtil.Install(targetMethod, typeof(EssenceChoicePrompt), nameof(OnOrbTriggerDetour));

            MethodInfo getRelicMsg = typeof(CorePreloader).GetProperty(
                nameof(CorePreloader.RelicGetMsg), BindingFlags.Instance | BindingFlags.Public)?.GetGetMethod();
            HookUtil.Install(getRelicMsg, typeof(EssenceChoicePrompt), nameof(OnGetRelicGetMsgDetour));

            MethodInfo sendMessageMethod = typeof(MessageController).GetMethod(
                "SendMessage", BindingFlags.Instance | BindingFlags.NonPublic);
            HookUtil.Install(sendMessageMethod, typeof(EssenceChoicePrompt), nameof(OnCornerSendMessageDetour));
        }

        public static bool IsEssenceRandomized()
        {
            var gs = RandomizerMod.RandomizerMod.RS?.GenerationSettings;
            if (gs == null) return false;

            return gs.PoolSettings.WhisperingRoots || IntegrationRegistry.HasExternalEssenceOrbSupport();
        }

        private static bool IsOrbVisible(DreamPlantOrb orb)
        {
            Renderer rend = orb.GetComponent<Renderer>() ?? orb.GetComponentInChildren<Renderer>();
            return rend == null || rend.enabled;
        }

        private static void OnOrbTriggerDetour(Action<DreamPlantOrb, Collider2D> orig, DreamPlantOrb self, Collider2D collision)
        {
            if (self == null || collision == null || !collision.CompareTag("Player"))
            {
                orig(self, collision);
                return;
            }

            if ((IsActiveField != null && !(bool)IsActiveField.GetValue(self))
                || (PickedUpField != null && (bool)PickedUpField.GetValue(self))
                || (CanPickupField != null && !(bool)CanPickupField.GetValue(self)))
            {
                return;
            }

            if (!NightmareLogicHandler.IsSaveActive || !IsEssenceRandomized())
            {
                orig(self, collision);
                return;
            }

            if (!IsOrbVisible(self))
            {
                return;
            }

            AccessCondition condition = GetConditionForOrb(self);
            if (condition == null)
            {
                ModLog.Debug($"[Essence] Collecting '{GetPrimaryOrbPlacementName(self)}' (scope={NightmareLogicHandler.ActiveScope}, no condition)");
                orig(self, collision);
                return;
            }

            if (condition.IsSatisfied(out int currentAmount))
            {
                ModLog.Debug($"[Essence] Collecting '{GetPrimaryOrbPlacementName(self)}' (satisfied: {condition.GetRequirementText()}, current={currentAmount})");
                orig(self, collision);
                return;
            }

            OrbTriggerTracker tracker = self.gameObject.GetAddComponent<OrbTriggerTracker>();

            if (tracker.TryAcquirePrompt(collision))
            {
                ModLog.Debug($"[Essence] Blocked '{GetPrimaryOrbPlacementName(self)}' (requires: {condition.GetRequirementText()}, current={currentAmount})");
                ShowLockedMessage(self, condition, currentAmount);
            }
        }

        private static GameObject OnGetRelicGetMsgDetour(Func<CorePreloader, GameObject> orig, CorePreloader self)
        {
            GameObject popup = orig(self);
            if (_capturingPopup)
            {
                _capturedPopup = popup;
            }
            return popup;
        }

        private static IEnumerator OnCornerSendMessageDetour(
            Func<MessageController, Sprite, string, IEnumerator> orig,
            MessageController self, Sprite sprite, string text)
        {
            IEnumerator inner = orig(self, sprite, text);
            if (inner != null)
            {
                GameObject popup = null;
                bool moved;
                _capturingPopup = true;
                _capturedPopup = null;
                try
                {
                    moved = inner.MoveNext();
                    popup = _capturedPopup;
                }
                finally
                {
                    _capturingPopup = false;
                    _capturedPopup = null;
                }

                if (moved)
                {
                    RelicGetMsgResizer.AdjustPopup(popup, text);
                    yield return inner.Current;
                }

                while (inner.MoveNext())
                {
                    yield return inner.Current;
                }
            }
        }

        private static void ShowLockedMessage(DreamPlantOrb orb, AccessCondition condition, int current)
        {
            string itemName = GetItemNameForOrb(orb);
            ISprite itemSprite = GetItemSpriteForOrb(orb);

            string req = condition.GetRequirementText();
            string suffix = condition.IsProgressive()
                ? $" (have: Lv{current})"
                : condition.IsNumeric()
                    ? $" (have: {current})"
                    : "";

            string msg = $"{itemName}: {req}{suffix}";

            var cornerMsg = new MsgUIDef
            {
                name = new BoxedString(msg),
                sprite = itemSprite ?? new ItemChangerSprite("ShopIcons.Essence")
            };
            cornerMsg.SendMessage(MessageType.Corner, null);
        }

        private static IEnumerable<string> GetOrbPlacementCandidates(DreamPlantOrb orb)
        {
            foreach (string externalName in IntegrationRegistry.GetDreamOrbPlacementCandidates(orb))
            {
                yield return externalName;
            }

            yield return GetRootPlacementName(orb);
        }

        private static string GetPrimaryOrbPlacementName(DreamPlantOrb orb)
        {
            return GetOrbPlacementCandidates(orb).FirstOrDefault();
        }

        private static AbstractItem TryGetFirstOrbItem(DreamPlantOrb orb)
        {
            var placements = Ref.Settings?.Placements;
            if (placements == null) return null;

            foreach (string candidate in GetOrbPlacementCandidates(orb))
            {
                if (!string.IsNullOrEmpty(candidate) && placements.TryGetValue(candidate, out AbstractPlacement placement))
                {
                    return placement.Items.FirstOrDefault();
                }
            }

            return null;
        }

        private static string GetItemNameForOrb(DreamPlantOrb orb)
        {
            AbstractItem firstItem = TryGetFirstOrbItem(orb);
            if (firstItem != null)
            {
                try
                {
                    string uiName = firstItem.UIDef?.GetPreviewName();
                    if (!string.IsNullOrEmpty(uiName)) return uiName;
                }
                catch (Exception ex)
                {
                    ModLog.Debug($"Failed to read UIDef name for orb item '{firstItem.name}': {ex.Message}");
                }

                if (!string.IsNullOrEmpty(firstItem.name))
                {
                    return firstItem.name.Replace('_', ' ');
                }
            }

            return "Dream Orb";
        }

        private static ISprite GetItemSpriteForOrb(DreamPlantOrb orb)
        {
            AbstractItem firstItem = TryGetFirstOrbItem(orb);
            if (firstItem?.UIDef is MsgUIDef msgUi && msgUi.sprite != null)
            {
                return msgUi.sprite;
            }

            return new ItemChangerSprite("ShopIcons.Essence");
        }

        private static AccessCondition GetConditionForOrb(DreamPlantOrb orb)
        {
            if (NightmareLogicHandler.ActiveScope == RequirementScope.PerObjectGroup)
            {
                AbstractItem firstItem = TryGetFirstOrbItem(orb);
                string groupKey = firstItem != null
                    ? NightmareLogicHandler.GetItemTypeGroup(firstItem.name)
                    : "Essence";

                return NightmareLogicHandler.ItemTypeConditions.TryGetValue(groupKey, out var groupCond)
                    ? groupCond
                    : null;
            }

            if (NightmareLogicHandler.ActiveScope == RequirementScope.PerObject)
            {
                foreach (string candidate in GetOrbPlacementCandidates(orb))
                {
                    if (!string.IsNullOrEmpty(candidate)
                        && NightmareLogicHandler.ObjectConditions.TryGetValue(candidate, out var objCond))
                    {
                        return objCond;
                    }
                }
                return null;
            }

            foreach (string candidate in GetOrbPlacementCandidates(orb))
            {
                if (!string.IsNullOrEmpty(candidate)
                    && NightmareLogicHandler.LocationConditions.TryGetValue(candidate, out var locCond))
                {
                    return locCond;
                }
            }

            return null;
        }

        private static string GetRootPlacementName(DreamPlantOrb orb)
        {
            string rootArea = GetRootAreaFromScene(orb.gameObject.scene.name);
            return $"Whispering_Root-{rootArea}";
        }

        private static string GetRootAreaFromScene(string sceneName) =>
            ConditionCatalog.GetRootAreaFromScene(sceneName);

        private sealed class OrbTriggerTracker : MonoBehaviour
        {
            private readonly HashSet<Collider2D> _overlappingPlayerColliders = new();
            private float _cooldownUntil;

            public bool TryAcquirePrompt(Collider2D playerCollider)
            {
                _overlappingPlayerColliders.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);

                bool wasInside = _overlappingPlayerColliders.Count > 0;
                _overlappingPlayerColliders.Add(playerCollider);

                if (wasInside) return false;
                return Time.time >= _cooldownUntil;
            }

            private void OnTriggerExit2D(Collider2D collision)
            {
                if (collision == null || !collision.CompareTag("Player")) return;

                if (_overlappingPlayerColliders.Remove(collision))
                {
                    _overlappingPlayerColliders.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
                    if (_overlappingPlayerColliders.Count == 0)
                    {
                        _cooldownUntil = Time.time + ExitCooldownSeconds;
                    }
                }
            }
        }

        private sealed class BackdropScaleKeeper : MonoBehaviour
        {
            private sealed class Entry
            {
                public Transform Transform;
                public SpriteRenderer Renderer;
                public float BaseScaleX;
                public float LastAppliedScaleX;
                public float BaseLocalPosX;
                public float LastAppliedLocalPosX;
                public float AnchorInParentX;
            }

            private readonly List<Entry> _entries = new();
            private Transform _popupRoot;
            private Transform _textTf;
            private Component _tmp;
            private Component _container;
            private string _expectedText;
            private bool _measured;
            private float _targetRatio = 1f;

            public void Initialize(
                Transform popupRoot,
                Transform textTf,
                Component tmp,
                Component container,
                Transform iconTf,
                List<SpriteRenderer> backdrops,
                string expectedText)
            {
                _popupRoot = popupRoot;
                _textTf = textTf;
                _tmp = tmp;
                _container = container;
                _expectedText = expectedText;
                _measured = false;
                _targetRatio = 1f;
                _entries.Clear();

                foreach (SpriteRenderer sr in backdrops)
                {
                    if (sr == null) continue;
                    Transform bg = sr.transform;

                    float anchorInParentX = bg.localPosition.x;
                    if (iconTf != null && bg.parent != null)
                    {
                        Vector3 leftOfIconWorld = popupRoot.TransformPoint(iconTf.localPosition - new Vector3(0.6f, 0f, 0f));
                        anchorInParentX = bg.parent.InverseTransformPoint(leftOfIconWorld).x;
                    }

                    _entries.Add(new Entry
                    {
                        Transform = bg,
                        Renderer = sr,
                        BaseScaleX = bg.localScale.x,
                        LastAppliedScaleX = bg.localScale.x,
                        BaseLocalPosX = bg.localPosition.x,
                        LastAppliedLocalPosX = bg.localPosition.x,
                        AnchorInParentX = anchorInParentX
                    });
                }

                TryMeasureAndApply();
            }

            private void LateUpdate()
            {
                if (!_measured)
                {
                    TryMeasureAndApply();
                }

                ApplyScale();
            }

            private void TryMeasureAndApply()
            {
                if (_tmp == null || _textTf == null || !_textTf.gameObject.activeInHierarchy) return;

                if (RelicGetMsgResizer.TryMeasureAndExpandText(
                        _textTf, _tmp, _container, _expectedText, out float preferredWidth, out float origDeltaX))
                {
                    _measured = true;
                    _targetRatio = RelicGetMsgResizer.ComputeBackdropRatio(preferredWidth);
                    ApplyScale();
                    RelicGetMsgResizer.LogFirstMeasurement(
                        _popupRoot != null ? _popupRoot.name : "null",
                        preferredWidth,
                        origDeltaX,
                        _targetRatio,
                        _entries);
                }
            }

            private void ApplyScale()
            {
                if (!_measured || _targetRatio <= 1.001f || _entries.Count == 0) return;

                foreach (Entry e in _entries)
                {
                    if (e.Transform == null) continue;

                    Vector3 scale = e.Transform.localScale;
                    if (!Mathf.Approximately(scale.x, e.LastAppliedScaleX))
                    {
                        e.BaseScaleX = scale.x;
                    }

                    Vector3 pos = e.Transform.localPosition;
                    if (!Mathf.Approximately(pos.x, e.LastAppliedLocalPosX))
                    {
                        e.BaseLocalPosX = pos.x;
                    }

                    float appliedScaleX = e.BaseScaleX * _targetRatio;
                    float appliedPosX = e.BaseLocalPosX + (e.AnchorInParentX - e.BaseLocalPosX) * (1f - _targetRatio);

                    e.Transform.localScale = new Vector3(appliedScaleX, scale.y, scale.z);
                    e.Transform.localPosition = new Vector3(appliedPosX, pos.y, pos.z);

                    e.LastAppliedScaleX = appliedScaleX;
                    e.LastAppliedLocalPosX = appliedPosX;
                }
            }
        }

        private static class RelicGetMsgResizer
        {
            private const float MinTextWidthForScale = 5.8f;
            private const float VisibleBackdropBaseWidth = 7.5f;
            private const float MaxBackdropScale = 3.0f;

            private static bool _reflectionResolved;
            private static Type _tmpType;
            private static PropertyInfo _preferredWidthProp;
            private static PropertyInfo _wordWrapProp;
            private static PropertyInfo _textProp;
            private static MethodInfo _forceMeshUpdateMethod;
            private static MethodInfo _forceMeshUpdateBoolMethod;
            private static MethodInfo _getPreferredValuesStrMethod;
            private static PropertyInfo _containerWidthProp;
            private static bool _loggedOnce;

            public static void AdjustPopup(GameObject popup, string expectedText)
            {
                try
                {
                    if (popup == null)
                    {
                        popup = FindFallbackPopup(expectedText);
                    }
                    if (popup == null) return;

                    Transform textTf = popup.transform.Find("Text");
                    Component tmp = textTf != null ? textTf.GetComponent("TextMeshPro") : null;
                    if (tmp == null) return;

                    Component container = textTf.GetComponent("TextContainer");
                    ResolveTmpReflection(tmp.GetType(), container?.GetType());
                    if (_preferredWidthProp == null) return;

                    Transform iconTf = popup.transform.Find("Icon");
                    List<SpriteRenderer> backdrops = CollectBackdrops(popup, textTf, iconTf);

                    BackdropScaleKeeper keeper = popup.GetAddComponent<BackdropScaleKeeper>();
                    keeper.Initialize(popup.transform, textTf, tmp, container, iconTf, backdrops, expectedText);
                }
                catch (Exception ex)
                {
                    ModLog.Error("Failed to adjust RelicGetMsg layout", ex);
                }
            }

            public static float ComputeBackdropRatio(float preferredWidth)
            {
                return preferredWidth > MinTextWidthForScale
                    ? Mathf.Clamp((preferredWidth + 1.7f) / VisibleBackdropBaseWidth, 1f, MaxBackdropScale)
                    : 1f;
            }

            public static bool TryMeasureAndExpandText(
                Transform textTf,
                Component tmp,
                Component container,
                string expectedText,
                out float preferredWidth,
                out float origDeltaX)
            {
                preferredWidth = 0f;
                origDeltaX = 0f;

                RectTransform textRect = textTf as RectTransform;
                Vector2 origTextSizeDelta = textRect != null ? textRect.sizeDelta : Vector2.zero;
                origDeltaX = origTextSizeDelta.x;
                Vector3 origTextLocalPos = textTf.localPosition;
                bool origWordWrap = _wordWrapProp != null && (bool)_wordWrapProp.GetValue(tmp, null);

                _wordWrapProp?.SetValue(tmp, false, null);
                InvokeForceMeshUpdate(tmp);
                preferredWidth = (float)_preferredWidthProp.GetValue(tmp, null);

                if (preferredWidth <= 0.01f && _getPreferredValuesStrMethod != null && !string.IsNullOrEmpty(expectedText))
                {
                    if (_getPreferredValuesStrMethod.Invoke(tmp, new object[] { expectedText }) is Vector2 pv)
                    {
                        preferredWidth = pv.x;
                    }
                }

                if (preferredWidth <= 0.01f)
                {
                    _wordWrapProp?.SetValue(tmp, origWordWrap, null);
                    return false;
                }

                if (textRect != null && preferredWidth > origTextSizeDelta.x)
                {
                    float targetTextWidth = preferredWidth + 0.5f;
                    float delta = targetTextWidth - origTextSizeDelta.x;
                    textRect.sizeDelta = new Vector2(targetTextWidth, origTextSizeDelta.y);
                    textRect.localPosition = origTextLocalPos + new Vector3(delta * textRect.pivot.x, 0f, 0f);

                    if (container != null && _containerWidthProp != null)
                    {
                        _containerWidthProp.SetValue(container, targetTextWidth, null);
                    }
                    InvokeForceMeshUpdate(tmp);
                }
                else
                {
                    _wordWrapProp?.SetValue(tmp, origWordWrap, null);
                    InvokeForceMeshUpdate(tmp);
                }

                return true;
            }

            public static void LogFirstMeasurement<T>(
                string popupName, float prefWidth, float origDeltaX, float ratio, List<T> entries)
            {
                if (_loggedOnce) return;
                _loggedOnce = true;
                ModLog.Info(
                    $"[RelicGetMsg] Measured popup='{popupName}', prefWidth={prefWidth:F2}, " +
                    $"origDelta={origDeltaX:F2}, bgCount={entries.Count}, ratio={ratio:F2}");
            }

            private static void InvokeForceMeshUpdate(Component tmp)
            {
                if (_forceMeshUpdateBoolMethod != null)
                {
                    _forceMeshUpdateBoolMethod.Invoke(tmp, new object[] { true });
                }
                else
                {
                    _forceMeshUpdateMethod?.Invoke(tmp, null);
                }
            }

            private static List<SpriteRenderer> CollectBackdrops(GameObject popup, Transform textTf, Transform iconTf)
            {
                var list = new List<SpriteRenderer>();
                foreach (SpriteRenderer sr in popup.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (sr == null || sr.sprite == null) continue;
                    if (sr.transform == textTf || sr.transform.IsChildOf(textTf)) continue;
                    if (iconTf != null && (sr.transform == iconTf || sr.transform.IsChildOf(iconTf))) continue;
                    if (sr.sprite.bounds.size.x <= 0.01f) continue;

                    list.Add(sr);
                }
                return list;
            }

            private static GameObject FindFallbackPopup(string expectedText)
            {
                _tmpType ??= Type.GetType("TMPro.TextMeshPro, Unity.TextMeshPro");
                if (_tmpType == null) return null;

                _textProp ??= _tmpType.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                if (_textProp == null) return null;

                foreach (UnityEngine.Object obj in UnityEngine.Object.FindObjectsOfType(_tmpType))
                {
                    if (obj is not Component c || !c.gameObject.activeInHierarchy) continue;
                    if (c.transform.parent == null || c.transform.name != "Text") continue;
                    if (!string.Equals(_textProp.GetValue(c, null) as string, expectedText, StringComparison.Ordinal)) continue;

                    Transform parent = c.transform.parent;
                    if (parent.Find("Icon") != null && parent.Find("pop_up_backboard") != null)
                    {
                        return parent.gameObject;
                    }
                }

                return null;
            }

            private static void ResolveTmpReflection(Type tmpType, Type containerType)
            {
                if (_reflectionResolved) return;
                _reflectionResolved = true;

                _tmpType = tmpType;
                _preferredWidthProp = tmpType.GetProperty("preferredWidth", BindingFlags.Public | BindingFlags.Instance);
                _wordWrapProp = tmpType.GetProperty("enableWordWrapping", BindingFlags.Public | BindingFlags.Instance);
                _textProp = tmpType.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                _forceMeshUpdateBoolMethod = tmpType.GetMethod("ForceMeshUpdate", new[] { typeof(bool) });
                _forceMeshUpdateMethod = tmpType.GetMethod("ForceMeshUpdate", Type.EmptyTypes);
                _getPreferredValuesStrMethod = tmpType.GetMethod("GetPreferredValues", new[] { typeof(string) });
                _containerWidthProp = containerType?.GetProperty("width", BindingFlags.Public | BindingFlags.Instance);
            }
        }
    }
}
