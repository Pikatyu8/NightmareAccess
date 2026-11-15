using System;
using System.Collections.Generic;
using MenuChanger;
using MenuChanger.Extensions;
using MenuChanger.MenuElements;
using MenuChanger.MenuPanels;
using NightmareAccess.Logic;
using RandomizerMod.Menu;
using UnityEngine;

namespace NightmareAccess.Settings
{
    public static class ConnectionMenu
    {
        private static SmallButton _entryButton;
        private static MenuLabel _warningLabel;
        private static readonly List<Action> RefreshCallbacks = new();

        public static void Hook()
        {
            RandomizerMenuAPI.AddMenuPage(ConstructMenu, TryGetSubmenuButton);
        }

        public static void RefreshMenu()
        {
            foreach (Action refresh in RefreshCallbacks)
            {
                refresh();
            }
            UpdateButtonAppearance();
        }

        private static bool TryGetSubmenuButton(MenuPage landingPage, out SmallButton button)
        {
            button = _entryButton;
            UpdateButtonAppearance();
            return true;
        }

        private static void UpdateButtonAppearance()
        {
            if (_entryButton != null)
            {
                _entryButton.Text.color = NightmareAccess.Settings.Enabled ? Colors.TRUE_COLOR : Colors.DEFAULT_COLOR;
            }

            if (_warningLabel != null)
            {
                _warningLabel.Text.text = "";
                _warningLabel.Hide();
            }
        }

        private static void ConstructMenu(MenuPage connectionPage)
        {
            RefreshCallbacks.Clear();

            MenuPage mainPage = new("Nightmare Access", connectionPage);
            MenuPage currPage = new("Currencies & Relics", mainPage);
            MenuPage upgPage = new("Upgrades & Equipment", mainPage);
            MenuPage abilitiesPage = new("Abilities & Spells", mainPage);
            MenuPage worldPage = new("World & Collectibles", mainPage);

            _entryButton = new SmallButton(connectionPage, "Nightmare Access");
            _entryButton.AddHideAndShowEvent(connectionPage, mainPage);

            mainPage.BeforeShow += RefreshMenu;

            var s = NightmareAccess.Settings;

            // ==========================================
            // 1. MAIN PAGE
            // ==========================================
            _warningLabel = new MenuLabel(mainPage, "");
            _warningLabel.Text.color = Color.red;
            _warningLabel.Text.fontSize = 20;
            _warningLabel.Text.alignment = TextAnchor.MiddleCenter;
            _warningLabel.MoveTo(new Vector2(0, 335));
            _warningLabel.Hide();

            ToggleButton enabledToggle = new(mainPage, "Enabled");
            enabledToggle.SetValue(s.Enabled);
            enabledToggle.SelfChanged += el =>
            {
                s.Enabled = ((ToggleButton)el).Value;
                UpdateButtonAppearance();
            };
            RefreshCallbacks.Add(() => enabledToggle.SetValue(s.Enabled));

            MenuEnum<RequirementScope> scopeEnum = new(mainPage, "Scope");
            scopeEnum.SetValue(s.Scope);
            scopeEnum.ValueChanged += val => s.Scope = val;
            RefreshCallbacks.Add(() => scopeEnum.SetValue(s.Scope));

            NumericEntryField<int> chanceField = new(mainPage, "Requirement Chance (%)");
            chanceField.SetClamp(0, 100);
            chanceField.SetValue(s.RequirementChance);
            chanceField.ValueChanged += val => s.RequirementChance = val;
            RefreshCallbacks.Add(() => chanceField.SetValue(s.RequirementChance));

            new VerticalItemPanel(mainPage, new Vector2(0, 260), 85f, true,
                enabledToggle,
                scopeEnum,
                chanceField
            );

            SmallButton toCurrBtn = new(mainPage, "Currencies & Relics ->");
            toCurrBtn.AddHideAndShowEvent(mainPage, currPage);

            SmallButton toUpgBtn = new(mainPage, "Upgrades & Equipment ->");
            toUpgBtn.AddHideAndShowEvent(mainPage, upgPage);

            SmallButton toAbilBtn = new(mainPage, "Abilities & Spells ->");
            toAbilBtn.AddHideAndShowEvent(mainPage, abilitiesPage);

            SmallButton toWorldBtn = new(mainPage, "World & Collectibles ->");
            toWorldBtn.AddHideAndShowEvent(mainPage, worldPage);

            new GridItemPanel(mainPage, new Vector2(0, -60), 2, 65f, 440f, true,
                toCurrBtn, toUpgBtn,
                toAbilBtn, toWorldBtn
            );

            // ==========================================
            // 2. CURRENCIES & RELICS
            // ==========================================
            List<IMenuElement> currElements = new();
            AddRow(currPage, currElements, "Geo", () => s.AllowGeo, v => s.AllowGeo = v, () => s.MinGeo, v => s.MinGeo = v, () => s.MaxGeo, v => s.MaxGeo = v, 1, 5000);
            AddRow(currPage, currElements, "Eggs", () => s.AllowEggs, v => s.AllowEggs = v, () => s.MinEggs, v => s.MinEggs = v, () => s.MaxEggs, v => s.MaxEggs = v, 1, 21);
            AddRow(currPage, currElements, "Essence", () => s.AllowEssence, v => s.AllowEssence = v, () => s.MinEssence, v => s.MinEssence = v, () => s.MaxEssence, v => s.MaxEssence = v, 1, 2400);
            AddRow(currPage, currElements, "Grubs", () => s.AllowGrubs, v => s.AllowGrubs = v, () => s.MinGrubs, v => s.MinGrubs = v, () => s.MaxGrubs, v => s.MaxGrubs = v, 1, 46);
            AddRow(currPage, currElements, "Roots", () => s.AllowRoots, v => s.AllowRoots = v, () => s.MinRoots, v => s.MinRoots = v, () => s.MaxRoots, v => s.MaxRoots = v, 1, 15);
            AddRow(currPage, currElements, "Relics", () => s.AllowRelics, v => s.AllowRelics = v, () => s.MinRelics, v => s.MinRelics = v, () => s.MaxRelics, v => s.MaxRelics = v, 1, 10);
            new GridItemPanel(currPage, new Vector2(0, 300), 3, 65f, 420f, true, currElements.ToArray());

            // ==========================================
            // 3. UPGRADES & KEYS
            // ==========================================
            List<IMenuElement> upgElements = new();
            AddRow(upgPage, upgElements, "Notches", () => s.AllowNotches, v => s.AllowNotches = v, () => s.MinNotches, v => s.MinNotches = v, () => s.MaxNotches, v => s.MaxNotches = v, 1, 8);
            AddRow(upgPage, upgElements, "Simple Keys", () => s.AllowSimpleKeys, v => s.AllowSimpleKeys = v, () => s.MinSimpleKeys, v => s.MinSimpleKeys = v, () => s.MaxSimpleKeys, v => s.MaxSimpleKeys = v, 1, 3);
            AddRow(upgPage, upgElements, "Mask Shards", () => s.AllowMaskShards, v => s.AllowMaskShards = v, () => s.MinMaskShards, v => s.MinMaskShards = v, () => s.MaxMaskShards, v => s.MaxMaskShards = v, 1, 16);
            AddRow(upgPage, upgElements, "Vessel Frags", () => s.AllowVesselFragments, v => s.AllowVesselFragments = v, () => s.MinVesselFragments, v => s.MinVesselFragments = v, () => s.MaxVesselFragments, v => s.MaxVesselFragments = v, 1, 9);
            AddRow(upgPage, upgElements, "Pale Ore", () => s.AllowPaleOre, v => s.AllowPaleOre = v, () => s.MinPaleOre, v => s.MinPaleOre = v, () => s.MaxPaleOre, v => s.MaxPaleOre = v, 1, 6);
            new GridItemPanel(upgPage, new Vector2(0, 300), 3, 65f, 420f, true, upgElements.ToArray());

            // ==========================================
            // 4. ABILITIES & SPELLS
            // ==========================================
            ToggleButton tDash = CreateToggle(abilitiesPage, "Allow Dash", () => s.AllowDash, v => s.AllowDash = v);
            ToggleButton tClaw = CreateToggle(abilitiesPage, "Allow Mantis Claw", () => s.AllowClaw, v => s.AllowClaw = v);
            ToggleButton tSuperdash = CreateToggle(abilitiesPage, "Allow Crystal Heart", () => s.AllowSuperdash, v => s.AllowSuperdash = v);
            ToggleButton tWings = CreateToggle(abilitiesPage, "Allow Monarch Wings", () => s.AllowDoubleJump, v => s.AllowDoubleJump = v);
            ToggleButton tIsma = CreateToggle(abilitiesPage, "Allow Isma's Tear", () => s.AllowAcidArmour, v => s.AllowAcidArmour = v);
            ToggleButton tDreamNail = CreateToggle(abilitiesPage, "Allow Dream Nail", () => s.AllowDreamNail, v => s.AllowDreamNail = v);

            ToggleButton tNailArts = CreateToggle(abilitiesPage, "Allow Nail Arts", () => s.AllowNailArts, v => s.AllowNailArts = v);
            ToggleButton tFireball = CreateToggle(abilitiesPage, "Allow Fireball Tiers", () => s.AllowFireball, v => s.AllowFireball = v);
            ToggleButton tQuake = CreateToggle(abilitiesPage, "Allow Quake Tiers", () => s.AllowQuake, v => s.AllowQuake = v);
            ToggleButton tScream = CreateToggle(abilitiesPage, "Allow Scream Tiers", () => s.AllowScream, v => s.AllowScream = v);
            ToggleButton tCrest = CreateToggle(abilitiesPage, "Allow City Crest", () => s.AllowCityCrest, v => s.AllowCityCrest = v);
            ToggleButton tLantern = CreateToggle(abilitiesPage, "Allow Lumafly Lantern", () => s.AllowLantern, v => s.AllowLantern = v);

            new GridItemPanel(abilitiesPage, new Vector2(0, 240), 2, 60f, 450f, true,
                tDash, tNailArts,
                tClaw, tFireball,
                tSuperdash, tQuake,
                tWings, tScream,
                tIsma, tCrest,
                tDreamNail, tLantern
            );

            // ==========================================
            // 5. WORLD & COLLECTIBLES
            // ==========================================
            List<IMenuElement> worldElements = new();

            AddFlexibleGroup(worldPage, worldElements, "Charms",
                () => s.AllowCharms, v => s.AllowCharms = v,
                () => s.AllowSpecificCharms, v => s.AllowSpecificCharms = v,
                () => s.AllowCharmsCount, v => s.AllowCharmsCount = v);

            AddFlexibleGroup(worldPage, worldElements, "Maps",
                () => s.AllowMaps, v => s.AllowMaps = v,
                () => s.AllowSpecificMaps, v => s.AllowSpecificMaps = v,
                () => s.AllowMapsCount, v => s.AllowMapsCount = v);

            AddFlexibleGroup(worldPage, worldElements, "Journal",
                () => s.AllowJournal, v => s.AllowJournal = v,
                () => s.AllowSpecificJournal, v => s.AllowSpecificJournal = v,
                () => s.AllowJournalCount, v => s.AllowJournalCount = v);

            AddFlexibleGroup(worldPage, worldElements, "Stags",
                () => s.AllowStags, v => s.AllowStags = v,
                () => s.AllowSpecificStags, v => s.AllowSpecificStags = v,
                () => s.AllowStagsCount, v => s.AllowStagsCount = v);

            ToggleButton tDreamers = CreateToggle(worldPage, "Allow Dreamers", () => s.AllowDreamers, v => s.AllowDreamers = v);
            ToggleButton tKingsoul = CreateToggle(worldPage, "Allow Kingsoul", () => s.AllowKingsoul, v => s.AllowKingsoul = v);
            ToggleButton tGrimm = CreateToggle(worldPage, "Allow Grimmchild", () => s.AllowGrimmchild, v => s.AllowGrimmchild = v);

            worldElements.Add(tDreamers);
            worldElements.Add(tKingsoul);
            worldElements.Add(tGrimm);

            new GridItemPanel(worldPage, new Vector2(0, 260), 3, 62f, 320f, true, worldElements.ToArray());

            UpdateButtonAppearance();
        }

        private static ToggleButton CreateToggle(MenuPage page, string label, Func<bool> getter, Action<bool> setter)
        {
            ToggleButton tb = new(page, label);
            tb.SetValue(getter());
            tb.SelfChanged += el => setter(((ToggleButton)el).Value);
            RefreshCallbacks.Add(() => tb.SetValue(getter()));
            return tb;
        }

        private static void AddFlexibleGroup(MenuPage page, List<IMenuElement> list, string label,
            Func<bool> masterGetter, Action<bool> masterSetter,
            Func<bool> specificGetter, Action<bool> specificSetter,
            Func<bool> countGetter, Action<bool> countSetter)
        {
            ToggleButton masterToggle = new(page, label);
            ToggleButton specificToggle = new(page, "Specific Req");
            ToggleButton countToggle = new(page, "Amount Req");

            var controller = new FlexibleGroupController(
                masterGetter, masterSetter,
                specificGetter, specificSetter,
                countGetter, countSetter,
                val =>
                {
                    if (masterToggle.Locked) masterToggle.Unlock();
                    masterToggle.SetValue(val);
                },
                val => specificToggle.SetValue(val),
                val => countToggle.SetValue(val),
                locked =>
                {
                    if (locked)
                    {
                        specificToggle.Lock();
                        countToggle.Lock();
                    }
                    else
                    {
                        specificToggle.Unlock();
                        countToggle.Unlock();
                    }
                });

            controller.InitializeUi();

            masterToggle.SelfChanged += el => controller.OnMasterChanged(((ToggleButton)el).Value);
            specificToggle.SelfChanged += el => controller.OnSpecificChanged(((ToggleButton)el).Value);
            countToggle.SelfChanged += el => controller.OnCountChanged(((ToggleButton)el).Value);
            RefreshCallbacks.Add(controller.Refresh);

            list.Add(masterToggle);
            list.Add(specificToggle);
            list.Add(countToggle);
        }

        internal sealed class FlexibleGroupController
        {
            private readonly Func<bool> _masterGetter;
            private readonly Action<bool> _masterSetter;
            private readonly Func<bool> _specificGetter;
            private readonly Action<bool> _specificSetter;
            private readonly Func<bool> _countGetter;
            private readonly Action<bool> _countSetter;

            private readonly Action<bool> _setMasterUi;
            private readonly Action<bool> _setSpecificUi;
            private readonly Action<bool> _setCountUi;
            private readonly Action<bool> _setChildrenLocked;

            private bool _isUpdating;

            public FlexibleGroupController(
                Func<bool> masterGetter, Action<bool> masterSetter,
                Func<bool> specificGetter, Action<bool> specificSetter,
                Func<bool> countGetter, Action<bool> countSetter,
                Action<bool> setMasterUi,
                Action<bool> setSpecificUi,
                Action<bool> setCountUi,
                Action<bool> setChildrenLocked)
            {
                _masterGetter = masterGetter;
                _masterSetter = masterSetter;
                _specificGetter = specificGetter;
                _specificSetter = specificSetter;
                _countGetter = countGetter;
                _countSetter = countSetter;
                _setMasterUi = setMasterUi;
                _setSpecificUi = setSpecificUi;
                _setCountUi = setCountUi;
                _setChildrenLocked = setChildrenLocked;
            }

            public void InitializeUi() => Refresh();

            public void OnMasterChanged(bool active)
            {
                if (_isUpdating) return;
                _isUpdating = true;
                try
                {
                    _masterSetter(active);

                    if (!active)
                    {
                        _specificSetter(false);
                        _countSetter(false);
                        // Unlock before SetValue because MenuChanger ignores SetValue while Locked
                        _setChildrenLocked(false);
                        _setSpecificUi(false);
                        _setCountUi(false);
                        _setChildrenLocked(true);
                    }
                    else
                    {
                        if (!_specificGetter() && !_countGetter())
                        {
                            _specificSetter(true);
                            _countSetter(true);
                        }
                        _setChildrenLocked(false);
                        _setSpecificUi(_specificGetter());
                        _setCountUi(_countGetter());
                    }
                }
                finally
                {
                    _isUpdating = false;
                }
            }

            public void OnSpecificChanged(bool active)
            {
                if (_isUpdating) return;
                if (!_masterGetter())
                {
                    _isUpdating = true;
                    try
                    {
                        _setChildrenLocked(false);
                        _setSpecificUi(false);
                        _setChildrenLocked(true);
                    }
                    finally
                    {
                        _isUpdating = false;
                    }
                    return;
                }

                _specificSetter(active);
                if (!active && !_countGetter())
                {
                    DisableMasterFromChildren();
                }
            }

            public void OnCountChanged(bool active)
            {
                if (_isUpdating) return;
                if (!_masterGetter())
                {
                    _isUpdating = true;
                    try
                    {
                        _setChildrenLocked(false);
                        _setCountUi(false);
                        _setChildrenLocked(true);
                    }
                    finally
                    {
                        _isUpdating = false;
                    }
                    return;
                }

                _countSetter(active);
                if (!active && !_specificGetter())
                {
                    DisableMasterFromChildren();
                }
            }

            public void Refresh()
            {
                if (_isUpdating) return;
                _isUpdating = true;
                try
                {
                    bool active = _masterGetter();
                    _setMasterUi(active);
                    _setChildrenLocked(false);
                    _setSpecificUi(active && _specificGetter());
                    _setCountUi(active && _countGetter());
                    if (!active)
                    {
                        _setChildrenLocked(true);
                    }
                }
                finally
                {
                    _isUpdating = false;
                }
            }

            private void DisableMasterFromChildren()
            {
                _isUpdating = true;
                try
                {
                    _masterSetter(false);
                    _setMasterUi(false);
                    _setChildrenLocked(true);
                }
                finally
                {
                    _isUpdating = false;
                }
            }
        }

        private static void AddRow(MenuPage page, List<IMenuElement> list, string label,
            Func<bool> getAllow, Action<bool> setAllow,
            Func<int> getMin, Action<int> setMin,
            Func<int> getMax, Action<int> setMax,
            int minBound, int maxBound)
        {
            ToggleButton toggle = new(page, $"Allow {label}");
            toggle.SetValue(getAllow());
            toggle.SelfChanged += el => setAllow(((ToggleButton)el).Value);

            NumericEntryField<int> minField = new(page, $"Min {label}");
            minField.SetClamp(minBound, maxBound);
            minField.SetValue(getMin());
            minField.ValueChanged += val => setMin(val);

            NumericEntryField<int> maxField = new(page, $"Max {label}");
            maxField.SetClamp(minBound, maxBound);
            maxField.SetValue(getMax());
            maxField.ValueChanged += val => setMax(val);

            RefreshCallbacks.Add(() =>
            {
                toggle.SetValue(getAllow());
                minField.SetValue(getMin());
                maxField.SetValue(getMax());
            });

            list.Add(toggle);
            list.Add(minField);
            list.Add(maxField);
        }
    }
}