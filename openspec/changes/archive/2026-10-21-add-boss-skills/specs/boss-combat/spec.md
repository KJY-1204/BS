# Spec Delta

## MODIFIED Requirements

### Requirement: The boss player sees the fight
The boss player SHALL see the boss's own health, the remaining time, the summon energy, the standing order and the cooldowns of the boss's skills (see the summoning, orders and boss-skills capabilities), and a summary of every survivor (character, health, and whether fallen), without needing a survivor in view. The camera SHALL keep following the boss's own character.

#### Scenario: Survivor summary
- **WHEN** a survivor is hurt or falls
- **THEN** the boss player's summary shows that survivor's lower health or fallen state

#### Scenario: Survivors do not see the summary
- **WHEN** a survivor looks at the screen
- **THEN** the boss-only survivor summary, the summon energy, the order and the skill cooldowns are not shown, but the time and the boss's health bar are

#### Scenario: Summon energy and order on the boss screen
- **WHEN** the boss player looks at the screen during a match
- **THEN** the summon energy and the standing order, if any, are shown together with the health and the time

#### Scenario: Skill cooldowns on the boss screen
- **WHEN** the boss player looks at the screen during a match
- **THEN** the two skills and their cooldowns are shown together with the energy and the order
