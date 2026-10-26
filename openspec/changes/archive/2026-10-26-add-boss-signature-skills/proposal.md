# Proposal

## Why

The three boss kinds differ in numbers, in which of the four skill effects they use and in one trait, but no kind has a skill that only it has: the standard, the giant and the sniper all pick from the same shot ring, shockwave, beam and roar, always centred on the boss or along the boss's own aim. A kind should give its player something to do that no other kind does. The smallest step that needs no new key and no new warning drawing is a skill that strikes where the nearest survivor stands instead of where the boss stands: it asks the boss player to choose when to use it and the survivors to move when they are chosen.

## What Changes

- A boss skill can be marked as aimed at the target: when it is used, the server picks the nearest living survivor to the boss, and the skill's place (and, for a beam, its aim) is that survivor's position at that moment instead of the boss's own position and aim. The announcement is the existing one, drawn at the place the event carries, so every client already shows it in the right place. With no living survivor the skill is used as before (at the boss).
- Each kind gets one signature skill that replaces an existing skill on the same key, so no key is added:
  - The giant's Q "대지 강타" becomes **도약 강타**: a shockwave that lands on the nearest survivor (radius 5.5, damage 40, cooldown 7 s, warning 1.1 s).
  - The sniper's E "저격" becomes **표적 저격**: a beam from the boss that aims itself at the nearest survivor (length 24, half width 0.5, damage 30, cooldown 12 s, warning 1.5 s).
  - The standard boss's F (phase 2) "저격선" becomes **집결**: a roar that makes its mobs around the nearest survivor instead of around the boss (8 mobs, cooldown 16 s, warning 1.0 s).
- The kinds keep their other skills, their traits and the rule that the effects of one kind's skills do not repeat (the standard kind's three skills become shot ring, shockwave, roar; the giant's and the sniper's are unchanged in effects).
- The balance gate stays: 35-70% survivor wins for the standard, giant and sniper boss on 360 matches each. The values above are first values; they are tuned with the harness (the survivor bots already step out of announced shockwaves and beams, so the warning times matter).

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
- `boss-skills`: a skill can be aimed at the target, and each kind has a signature skill that is.
- `boss-phases`: the standard kind's third skill is the rally roar, not the beam.

## Impact

- `BossSkillDef` (`AtTarget`), `BossSkillState` (the target), `BossSkillSim` (place and aim of an aimed skill), `MatchWorld` (finds the nearest living survivor), `BossCatalog` (the three skills), tests that assume the standard boss's F is a beam, README, `tools/harness/signature-skills/`. No new key, no wire-format change, no new warning drawing.
