# Nightmare Access

An add-on for Randomizer 4 that adds randomized, non-consumable access requirements to items and locations. Unlike standard randomizer costs, currency, resources, and items are not spent upon verification; instead, the mod simply checks whether you own them or have reached the required threshold.

## Key Features

The mod is configured via the Randomizer 4 Connections menu under the Nightmare Access section, where you can enable the add-on, choose the scope of requirements, and set their appearance chance from 0 to 100 percent. For numerical requirements, you can also configure minimum and maximum thresholds, as well as filter allowed condition types individually.

For standard pickup items that do not have a consumable cost, a met requirement automatically skips the confirmation prompt. On objects without a standard selection dialogue - including Whispering Roots, breakable walls, vines, benches, hot springs, Delicate Flower quests, and in-game triggers - an unmet condition displays a notification in the corner of the screen showing the requirement and your current progress, while in the Colosseum of Fools, requirements are displayed directly on the trial plaques. As a built-in fix, the Spa Gladiator in the Colosseum hot spring is only locked when water droplets actually hit him, rather than from any nail strike hitting the water.

## Operation Modes

In `PerLocation` mode, a random condition is bound to a specific location and integrated into the randomizer logic before item placement begins. In `PerObjectGroup` mode, a single shared requirement is assigned to an entire group of similar items - such as all charms, maps, grubs, mask shards, or key items. In `PerObject` mode, each individual object or item receives its own unique requirement, including every individual element inside multi-item containers.

## Requirement Types

Supported numerical metrics include Geo, Rancid Eggs, Essence, Grubs, Whispering Roots, Simple Keys, Mask Shards, Vessel Fragments, Pale Ore, Charm Notches, and Relics, as well as the total count of collected Charms, Maps, Hunter's Journal entries, and unlocked Stag Stations. Available condition pools automatically adapt to your active randomizer settings so that disabled or replaced categories are excluded.

Available abilities and progression items include Dash, Mantis Claw, and Crystal Heart (with full support for their split variants: Split Cloak, Split Claw, and Split Superdash), as well as Monarch Wings, Isma's Tear, Dream Nail upgrades up to Awoken Dream Nail, Nail Arts, and spell upgrades for Vengeful Spirit, Desolate Dive, and Howling Wraiths. You can also allow requirements for the City Crest, Lumafly Lantern, Dreamers, Kingsoul upgrades up to Void Heart, and Grimmchild levels. Flexible toggles for Charms, Maps, the Hunter's Journal, and Stag Stations let you independently allow checks for either a specific item or a required total count.

## Beatability Guarantee and Spoiler Log

After seed generation and before modifying ItemChanger containers, a built-in verifier simulates a full playthrough of the world using RandomizerCore's `ProgressionManager`. It checks the reachability of all base locations, waypoints, and transitions while accounting for actual in-game resource accumulation mechanics and the specifics of connected mods. If any requirement causes a softlock, creates a self-blocking loop, or demands more resources than can be gathered at that stage, the verifier automatically relaxes or rerolls the problematic conditions. Upon export, a `NightmareAccessSpoiler.txt` file is saved containing a verification summary and a complete list of all assigned requirements.

## Mod Compatibility

The mod supports sharing configuration presets via RandoSettingsManager and includes direct integrations with twenty-one randomizer extensions: AccessRandomizer, AlphabetRando, BenchRando, BreakableWallRandomizer, CombatRandomizer, CorpseDreamRando, CurseRandomizer, DarknessRandomizer, ExtraRando, Fishing, FlowerRandomizer, GodhomeRandomizer, LoreRandomizer with LoreCore, MoreLocations, MoreStags, RandoPlus, RgbRando, RopeRando, TheGloryOfBeingAFoolRandomizer, and YetAnotherRandoConnection. For these mods, custom locations, item grouping rules, logic modifications, special counters, and non-standard in-game triggers are all properly accounted for.

## Dependencies and Installation

Requires ItemChanger, MenuChanger, Randomizer 4, and Satchel. For manual installation, simply place `NightmareAccess.dll` into the `hollow_knight_Data/Managed/Mods/NightmareAccess` folder, or install the mod via a mod manager once added to ModLinks.
