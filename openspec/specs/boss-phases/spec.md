# boss-phases Specification

## Purpose

Defines the phases of a match with a boss: how the boss's health moves the match from one phase to the next, what each change brings (an announced burst of mobs, a third boss skill, faster cooldowns) and how everyone sees it, so that the boss side has an arc over the match like the survivors have.

## Requirements

### Requirement: The boss's health moves the match through three phases
A match with a boss SHALL have three phases decided by the server from the boss's health: phase 1 from the start, phase 2 once the boss's health has fallen to 66 percent of its maximum or less, phase 3 once it has fallen to 33 percent or less. A phase SHALL NOT go back, a new match SHALL start at phase 1, and the thresholds SHALL be adjustable values. A match without a boss has no phases.

#### Scenario: Phase 2
- **WHEN** the boss's health falls to 66 percent of its maximum
- **THEN** the match is in phase 2

#### Scenario: Phase 3
- **WHEN** the boss's health falls to 33 percent of its maximum
- **THEN** the match is in phase 3

#### Scenario: No way back
- **WHEN** the boss's health rises again after it reached phase 2
- **THEN** the match stays in phase 2

#### Scenario: A new match
- **WHEN** a match starts after one that reached phase 3
- **THEN** it starts at phase 1

### Requirement: A phase change is announced and brings a burst of mobs
When the match enters phase 2 or phase 3 the server SHALL announce it with a warning ring around the boss, seen by every player like any boss skill warning, and when the warning has passed a burst of summoned mobs SHALL appear around the boss, as many as the mob cap allows, more in phase 3 than in phase 2. The burst SHALL NOT make the boss invulnerable, SHALL NOT cost summon energy and SHALL be dropped if the match is no longer running or the boss has fallen.

#### Scenario: The announcement
- **WHEN** the boss's health falls to the threshold of a phase
- **THEN** every client shows a warning ring around the boss for the warning time

#### Scenario: The burst
- **WHEN** the warning has passed and the match is running
- **THEN** a burst of summoned mobs appears around the boss and the summon energy is unchanged

#### Scenario: The boss can be hurt
- **WHEN** survivors shoot the boss during the warning
- **THEN** it takes damage as usual

#### Scenario: The match ends first
- **WHEN** the boss falls or the match ends before the warning has passed
- **THEN** no mobs appear

### Requirement: Phase 2 unlocks a third boss skill on the F key
Phase 2 SHALL unlock a third boss skill, used with the F key, whose effect depends on the boss kind and is one the kind's Q and E skills do not have: the roar for the standard kind (its rally, aimed at the target), the shot ring for the giant, the shockwave for the sniper. Before phase 2 pressing F SHALL do nothing and start no cooldown, and the skill SHALL be ready the moment it is unlocked.

#### Scenario: Locked
- **WHEN** the boss player presses F in phase 1
- **THEN** nothing happens and no cooldown starts

#### Scenario: Unlocked
- **WHEN** the match is in phase 2 and the boss player presses F
- **THEN** the skill is announced with a warning, its cooldown starts and its effect is released when the warning has passed

#### Scenario: The kind's effect
- **WHEN** the boss player plays the standard kind, the giant or the sniper and uses the skill
- **THEN** it is the roar, the shot ring or the shockwave

### Requirement: The third skill follows the rules of the Q and E skills
The third skill SHALL be usable only by the boss player, only while the match is running and the boss stands, only when its own cooldown has passed, and SHALL cost no summon energy. It SHALL be announced before it takes effect and SHALL grow stronger as the boss loses health by the same rules as the other skills.

#### Scenario: Not the boss
- **WHEN** a survivor's client sends a request for the third skill
- **THEN** the server ignores it

#### Scenario: A weaker boss
- **WHEN** the boss at low health uses the third skill
- **THEN** it deals more than its normal damage and its cooldown is shorter, by the same rules as the other skills

#### Scenario: Its own cooldown
- **WHEN** the boss uses the third skill and then Q
- **THEN** each has its own cooldown and Q is not refused because of the third skill

### Requirement: Phase 3 shortens the cooldowns of the skills
In phase 3 the cooldown of each of the boss's three skills SHALL be shorter, to 85 percent of what it would otherwise be, on top of the shortening that grows with the boss's lost health. It SHALL be decided when a cooldown starts, so a running cooldown is not changed. The factor SHALL be an adjustable value.

#### Scenario: Before phase 3
- **WHEN** the boss uses a skill in phase 2
- **THEN** its cooldown is the one its health gives, without the phase 3 factor

#### Scenario: In phase 3
- **WHEN** the boss uses a skill in phase 3
- **THEN** its cooldown is shorter by the phase 3 factor as well

#### Scenario: A running cooldown
- **WHEN** the match enters phase 3 while a skill is cooling down
- **THEN** that cooldown is not shortened and the next use gets the shorter one

### Requirement: Everyone sees the phase and the boss sees the third skill
Every client SHALL be told the current phase, SHALL show a banner when it changes and SHALL show the phase near the boss's health bar. The boss player's screen SHALL show the third skill with its key F, its name and its cooldown, marked as locked until phase 2. Survivors SHALL NOT receive or show the boss's skill cooldowns, as before.

#### Scenario: The banner
- **WHEN** the match enters a new phase
- **THEN** every client shows a banner with the phase

#### Scenario: The boss screen
- **WHEN** the boss player looks at the screen in phase 1 and in phase 2
- **THEN** the F skill is shown as locked in phase 1 and as ready or with its remaining seconds in phase 2

#### Scenario: Survivors
- **WHEN** a survivor looks at the screen
- **THEN** the phase is shown but no boss skill cooldown is

### Requirement: The phases can be switched off
The phases SHALL be switchable off by a development and harness setting. With them off the match SHALL stay in phase 1, the third skill SHALL stay locked and no burst SHALL happen, so the match plays as it did before the phases.

#### Scenario: Switched off
- **WHEN** a match with the phases off has its boss fall to 10 percent health
- **THEN** it is still phase 1, pressing F does nothing and no burst has happened
