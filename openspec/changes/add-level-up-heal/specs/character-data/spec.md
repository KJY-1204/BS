# Spec Delta

## MODIFIED Requirements

### Requirement: The server applies the character to the survivor
When a survivor is created, the server SHALL set the survivor's starting maximum health, starting maximum mana, mana recovery and movement speed from that survivor's character data, and the survivor SHALL start with full health and full mana of those values. Growth per level and from skills SHALL stay the same common rules for every character and add to the character's starting values.

#### Scenario: Different starting values
- **WHEN** two survivors with different characters start
- **THEN** each starts with its own character's full health and full mana, and each shows its own maximum

#### Scenario: Growth adds to the character's base
- **WHEN** a survivor of any character reaches level 2
- **THEN** the maximum health and mana rise by the common per-level amounts above that character's starting values, and current mana is not refilled (health is healed only by the level-up heal)
