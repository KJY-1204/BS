# survivor-mana-skills Specification

## Purpose

Defines mana and the two key-activated skill slots: mana refills over time, the server decides whether a skill may fire, and every player sees the effect of a skill.

## Requirements

### Requirement: Survivors have mana that refills with time
Every survivor SHALL have mana with a maximum. Mana SHALL refill at a steady rate over time and SHALL NOT exceed the maximum. A survivor SHALL start a room with full mana. Only the owner SHALL see their own mana.

#### Scenario: Refill over time
- **WHEN** a survivor has less than full mana and does not use a skill for one second
- **THEN** mana rises by the refill rate for one second

#### Scenario: Cap
- **WHEN** refilling would take mana above the maximum
- **THEN** mana stops at the maximum

#### Scenario: Mana is private
- **WHEN** a survivor spends mana
- **THEN** only that survivor's player sees the mana bar change

### Requirement: Two skill slots are triggered by keys
A survivor SHALL have two skill slots, triggered by the keys Q and E. A slot SHALL be usable only after a skill has been learned for it, and the owner SHALL send only the slot to the server.

#### Scenario: Unlearned slot
- **WHEN** a player presses a key for a slot with no learned skill
- **THEN** nothing happens

#### Scenario: Learned slot
- **WHEN** a player presses the key of a slot whose skill is learned
- **THEN** the request reaches the server

### Requirement: The server decides whether a skill fires
The server SHALL fire a skill only when the survivor is alive, the skill is learned, the skill is off cooldown and the survivor has enough mana for the skill's cost as the survivor's passives change it. A client SHALL NOT be able to fire without meeting these conditions.

#### Scenario: Allowed
- **WHEN** a living survivor with enough mana presses the key of a ready skill
- **THEN** the skill fires, its mana cost is spent and its cooldown starts

#### Scenario: Not enough mana
- **WHEN** a survivor presses the key with less mana than the cost
- **THEN** the skill does not fire and no mana is spent

#### Scenario: On cooldown
- **WHEN** a survivor presses the key during the cooldown
- **THEN** the skill does not fire

#### Scenario: Dead survivor
- **WHEN** a dead survivor presses a skill key
- **THEN** the skill does not fire

#### Scenario: Repeated requests
- **WHEN** a client sends the same request many times in a second
- **THEN** the skill fires at most once per cooldown

#### Scenario: A passive changes the cost and the cooldown
- **WHEN** a survivor whose own passive lowers the cost or the cooldown presses the key of a ready skill
- **THEN** the mana spent and the cooldown that starts are the changed values

### Requirement: The owner is told the result
The server SHALL tell the owner whether a skill request succeeded and, if so, how long the cooldown is, so the owner's screen shows the cooldown without guessing.

#### Scenario: Success
- **WHEN** a skill fires
- **THEN** the owner's slot shows the cooldown counting down

#### Scenario: Refusal
- **WHEN** a request is refused
- **THEN** the owner's screen shows briefly why (mana, cooldown)

### Requirement: Burst damages mobs around the survivor
The Burst skill SHALL damage every mob within its radius of the survivor when it fires. Its damage and radius SHALL grow with the skill's rank. Mobs it kills SHALL give experience like any other kill.

#### Scenario: Mobs around
- **WHEN** Burst fires with three mobs inside the radius and one outside
- **THEN** the three take damage and the one outside does not

#### Scenario: Kills give experience
- **WHEN** Burst kills a mob
- **THEN** nearby living survivors receive the experience for it

### Requirement: Rapid Fire speeds up the basic attack for a time
The Rapid Fire skill SHALL shorten the survivor's basic attack cooldown for a limited time. The duration and the speedup SHALL grow with the skill's rank. The effect SHALL end by itself.

#### Scenario: During the effect
- **WHEN** Rapid Fire is active
- **THEN** the survivor fires faster than normal

#### Scenario: After the effect
- **WHEN** the duration has passed
- **THEN** the firing rate returns to normal

#### Scenario: Death ends it
- **WHEN** a survivor with Rapid Fire active dies
- **THEN** the effect ends

### Requirement: Everyone sees skill effects
Every client SHALL show a skill's effect at the place and moment the server fired it, on the same display clock used for mobs and shots.

#### Scenario: A teammate uses Burst
- **WHEN** one survivor fires Burst
- **THEN** every client shows an expanding ring at that spot when the display clock reaches the moment it fired

#### Scenario: Mobs killed by Burst
- **WHEN** Burst kills mobs
- **THEN** each client removes those mobs at the same display moment as the ring

### Requirement: Skills keep within the combat traffic budget
Skill events SHALL be small, and four survivors using their skills as often as the cooldowns allow SHALL NOT take the combat traffic above its budget.

#### Scenario: Heavy skill use
- **WHEN** four survivors use their skills whenever ready for one minute with 100 mobs alive
- **THEN** combat traffic to each client averages no more than 10 KB per second
