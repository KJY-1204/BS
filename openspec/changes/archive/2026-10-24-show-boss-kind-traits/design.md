# Design

## Context

`NetworkMenu` builds the kind buttons from `BossCatalog.All`; `SummonHud.DrawSkills` draws the boss player's skill cells from `BossCatalog.Current`. Both are IMGUI and read the catalog, which every client has, so no replication is needed. Existing pure text helpers (`SkillCellText`, `BossBarText`) are what tests cover.

## Goals / Non-Goals

**Goals:** show name and description in the lobby and the boss HUD, from data.
**Non-Goals:** showing whether the trait is active right now (would need replicated state), survivors seeing the boss trait, any rule change.

## Decisions

1. `BossDef.TraitName` and `TraitText` strings; empty means none. The text is written with the same numbers as the value fields, and a test checks the numbers appear.
2. Pure helpers `SummonHud.TraitLineText(BossDef)` (empty when no trait) and `NetworkMenu.KindLabel(BossDef, bool chosen)`, so the strings are testable without IMGUI.
3. The trait line sits just above the device bar, centered, small style; shown only in the boss player's `SummonHud`, which only the boss player runs.

## Risks / Trade-offs

- A long description may overflow the lobby button; it is kept to one short clause and the name only goes in the lobby.
- Window layout is checked by a windowed capture before finishing.
