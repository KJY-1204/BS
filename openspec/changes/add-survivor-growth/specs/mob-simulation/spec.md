# Spec Delta

## ADDED Requirements

### Requirement: Damage can hit every mob in a circle
The server SHALL be able to deal damage to every mob within a given radius of a point at once. Each affected mob SHALL be handled like any other damaged mob, including being removed at zero health, and the server SHALL report where each mob died.

#### Scenario: Mobs inside and outside
- **WHEN** area damage is dealt with three mobs inside the radius and one outside
- **THEN** the three take the damage and the one outside is unchanged

#### Scenario: Kills are reported
- **WHEN** area damage kills two mobs
- **THEN** both are removed and their death positions are reported to the caller

#### Scenario: Empty area
- **WHEN** no mob is within the radius
- **THEN** nothing changes
