# Spec Delta

## MODIFIED Requirements

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
