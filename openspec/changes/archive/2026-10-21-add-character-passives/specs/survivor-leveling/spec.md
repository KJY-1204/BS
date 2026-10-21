# Spec Delta

## MODIFIED Requirements

### Requirement: Each level raises stats automatically
Each level gained SHALL raise the survivor's maximum health, maximum mana and projectile damage by fixed amounts from a growth table, and the survivor's mana recovery SHALL also include what the common passives and the character's own passive add. A level up SHALL NOT restore current health or current mana.

#### Scenario: Stats after a level up
- **WHEN** a survivor gains a level
- **THEN** the maximum health, maximum mana and projectile damage are higher by the table amounts

#### Scenario: No healing
- **WHEN** a survivor with 40 health out of 100 gains a level
- **THEN** current health is still 40 and the maximum is higher

#### Scenario: No mana refill
- **WHEN** a survivor with 20 mana gains a level
- **THEN** current mana is still 20 and the maximum is higher

#### Scenario: Mana recovery from a passive
- **WHEN** a survivor learns the mana-recovery passive of its own character
- **THEN** its mana recovery per second is higher by the passive's amount for its rank
