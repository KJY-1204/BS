# Spec Delta

## ADDED Requirements

### Requirement: A level-up heals the survivor
When a living survivor levels up, the server SHALL heal them by 5% of their maximum health after the level-up's new maximum has been applied, rounded and at least 1, never above the maximum. A fallen survivor SHALL not be healed or revived by it. Gaining several levels at once SHALL heal once for each level gained.

#### Scenario: A hurt survivor levels up
- **WHEN** a survivor with half of their health levels up
- **THEN** their health rises by 5% of the new maximum

#### Scenario: Full health
- **WHEN** a survivor at full health levels up
- **THEN** their health is the health they had plus the heal, never above the new maximum (raising the maximum itself does not fill it)

#### Scenario: Fallen survivor
- **WHEN** experience is shared and the level-up would apply to a fallen survivor
- **THEN** they stay fallen with no health

#### Scenario: Several levels
- **WHEN** a survivor gains two levels from one share of experience
- **THEN** the heal is applied twice

### Requirement: The level-up heal can be changed in the harness
The harness SHALL accept an override `levelUpHeal`, the fraction of the maximum health a level-up heals, from 0 to 1: 0 turns the heal off so that a level-up changes no one's health as before the heal existed. A value outside that range SHALL be refused with a reason that names the override.

#### Scenario: Off
- **WHEN** a match runs with `levelUpHeal` 0
- **THEN** a level-up does not heal

#### Scenario: Another fraction
- **WHEN** a match runs with `levelUpHeal` 0.25
- **THEN** a level-up heals by a quarter of the maximum health

#### Scenario: Bad value
- **WHEN** `levelUpHeal` is -0.1 or 1.5
- **THEN** the settings are refused

## MODIFIED Requirements

### Requirement: Each level raises stats automatically
Each level gained SHALL raise the survivor's maximum health, maximum mana and projectile damage by fixed amounts from a growth table, and the survivor's mana recovery SHALL also include what the common passives and the character's own passive add. A level up SHALL NOT restore current mana. A level up heals the survivor by a fraction of their maximum health, and only by that (see the requirement that a level-up heals the survivor); raising the maximum itself does not heal.

#### Scenario: Stats after a level up
- **WHEN** a survivor gains a level
- **THEN** the maximum health, maximum mana and projectile damage are higher by the table amounts

#### Scenario: No healing
- **WHEN** a survivor with 40 health out of 100 gains a level
- **THEN** current health is 40 plus the level-up heal (5% of the new maximum) and the maximum is higher

#### Scenario: No mana refill
- **WHEN** a survivor with 20 mana gains a level
- **THEN** current mana is still 20 and the maximum is higher

#### Scenario: Mana recovery from a passive
- **WHEN** a survivor learns the mana-recovery passive of its own character
- **THEN** its mana recovery per second is higher by the passive's amount for its rank
