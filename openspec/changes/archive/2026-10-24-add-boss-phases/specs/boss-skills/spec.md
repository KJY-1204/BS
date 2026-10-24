# Spec Delta

## MODIFIED Requirements

### Requirement: The boss has two skills on Q and E
The boss player SHALL be able to use two base skills, Q and E, whose effects are those the playing boss kind gives them (the shot ring, the shockwave, the beam or the roar); a third skill that a phase unlocks on the F key is defined by the boss phases. A skill SHALL be usable only while a match is running and the boss has not fallen, and only when its cooldown has passed. A request from anyone other than the boss player SHALL be ignored. A skill SHALL NOT cost summon energy; each skill SHALL have a cooldown of its own and nothing else. All skill values SHALL be adjustable without changing rules elsewhere.

#### Scenario: Using a skill
- **WHEN** the boss player presses Q while the match is running, the boss stands and the Q cooldown has passed
- **THEN** the Q cooldown starts at once, a warning is shown and the Q skill's effect is released when the warning time has passed

#### Scenario: Still cooling down
- **WHEN** the boss player presses a skill key before its cooldown has passed
- **THEN** nothing happens and the cooldown is unchanged

#### Scenario: No energy needed
- **WHEN** the boss has no summon energy and uses a skill
- **THEN** the skill works and the summon energy is unchanged

#### Scenario: Not the boss
- **WHEN** a survivor's client sends a boss skill request
- **THEN** the server ignores it and nothing happens

#### Scenario: Match over or boss fallen
- **WHEN** the match has ended or the boss has fallen and the boss player presses a skill key
- **THEN** nothing happens

#### Scenario: Time stop
- **WHEN** a survivor's skill has stopped the mobs
- **THEN** the boss's skills still work
