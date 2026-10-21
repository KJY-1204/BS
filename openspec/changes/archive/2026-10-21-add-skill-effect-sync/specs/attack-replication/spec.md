# Spec Delta

## MODIFIED Requirements

### Requirement: Skill events share the combat event stream
Skill activations SHALL travel as combat events of their own kind in the same reliable stream as shots and hits, carrying the skill, the survivor's slot, the place, the aim direction, the rank, the range scale and the duration scale, and the same server time, so every effect is ordered with the shots and kills around it.

#### Scenario: Ordering
- **WHEN** a survivor fires a shot and then a skill in the same step
- **THEN** every client receives the events in that order

#### Scenario: Direction carried
- **WHEN** a survivor fires a skill that has a direction
- **THEN** every client receives the direction the server used

#### Scenario: Scales carried
- **WHEN** a survivor fires a skill with a range scale of 130 and a duration scale of 140
- **THEN** every client receives 130 and 140 for that use
