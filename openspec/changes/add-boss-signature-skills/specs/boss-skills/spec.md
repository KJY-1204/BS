# Spec Delta

## ADDED Requirements

### Requirement: A skill can be aimed at the target
A boss skill SHALL be able to be marked as aimed at the target. When such a skill is used and a living survivor exists, the server SHALL take the nearest living survivor to the boss at that moment as the target, and the skill's place SHALL be that survivor's position at that moment instead of the boss's: the announcement SHALL carry that place, and the effect SHALL take effect there when the announcement has passed, wherever the survivor has moved to since. For a beam the place is still the boss's, and its aim is the direction from the boss to the target at that moment. With no living survivor the skill SHALL be used as an ordinary skill, at the boss. A skill that is not marked SHALL behave as before.

#### Scenario: A shockwave aimed at the target
- **WHEN** an aimed shockwave is used and the nearest living survivor stands 8 units from the boss
- **THEN** the announcement is drawn on that survivor's position and, when it has passed, survivors within the radius of that position are hurt

#### Scenario: The target moves
- **WHEN** the target survivor walks away during the warning
- **THEN** the effect still takes effect at the place announced

#### Scenario: A beam aimed at the target
- **WHEN** an aimed beam is used
- **THEN** its line runs from the boss toward the target survivor's position at that moment, whatever the boss player was aiming at

#### Scenario: A roar aimed at the target
- **WHEN** an aimed roar is used
- **THEN** its mobs appear around the target survivor's position

#### Scenario: Nobody alive
- **WHEN** an aimed skill is used and no survivor is alive
- **THEN** it is announced and takes effect at the boss like an ordinary skill

#### Scenario: The nearest one
- **WHEN** two survivors are alive
- **THEN** the one nearer to the boss is the target

### Requirement: Each kind has a signature skill aimed at the target
Each boss kind SHALL have one skill that only it has, aimed at the target and in place of an ordinary skill on the same key: the giant's Q is the leap strike (a shockwave of radius 5.5, damage 40, cooldown 7 s and a warning of 1.1 s), the sniper's E is the targeted shot (a beam of length 24, half width 0.5, damage 30, cooldown 12 s and a warning of 1.5 s), and the standard kind's F, unlocked in phase 2, is the rally (a roar of 8 mobs, cooldown 16 s and a warning of 1.0 s). These values SHALL be data of the kind. No key SHALL be added, and the effects of one kind's skills SHALL not repeat.

#### Scenario: The giant's leap strike
- **WHEN** the boss player plays the giant and presses Q
- **THEN** a shockwave is announced on the nearest survivor and released there after 1.1 seconds

#### Scenario: The sniper's targeted shot
- **WHEN** the boss player plays the sniper and presses E
- **THEN** a beam toward the nearest survivor is announced and released after 1.5 seconds

#### Scenario: The standard kind's rally
- **WHEN** the boss player plays the standard kind in phase 2 and presses F
- **THEN** a roar is announced at the nearest survivor and its mobs appear around that place after 1.0 seconds

#### Scenario: No new key
- **WHEN** any kind is played
- **THEN** the skills are still on Q, E and (from phase 2) F

#### Scenario: Effects do not repeat
- **WHEN** the skills of any kind are examined
- **THEN** no two of its Q, E and F skills have the same effect
