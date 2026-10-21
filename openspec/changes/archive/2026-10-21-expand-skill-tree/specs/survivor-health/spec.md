# Spec Delta

## MODIFIED Requirements

### Requirement: Damage reduces health and never goes below zero
Damage dealt to a survivor SHALL reduce health by the damage amount, and health SHALL NOT go below zero. When the survivor has a damage-reduction passive, the damage amount SHALL first be lowered by the passive's fraction, with a hit that would deal damage still dealing at least one.

#### Scenario: Normal hit
- **WHEN** a survivor with health 100 takes damage 10
- **THEN** the survivor has health 90

#### Scenario: Overkill
- **WHEN** a survivor with health 5 takes damage 10
- **THEN** the survivor has health 0

#### Scenario: A reduced hit
- **WHEN** a survivor with a damage-reduction passive that lowers damage by twenty percent takes damage 10 at health 100
- **THEN** the survivor has health 92

#### Scenario: The least a hit can do
- **WHEN** a survivor with a damage-reduction passive takes damage 1
- **THEN** the survivor loses one health
