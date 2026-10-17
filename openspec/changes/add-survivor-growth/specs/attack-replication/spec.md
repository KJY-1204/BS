# Spec Delta

## ADDED Requirements

### Requirement: Skill events share the combat event stream
Skill activations SHALL travel as combat events of their own kind in the same reliable stream as shots and hits, carrying the skill, the survivor's slot, the place and the rank, and the same server time, so every effect is ordered with the shots and kills around it.

#### Scenario: Ordering
- **WHEN** a survivor fires a shot and then a skill in the same step
- **THEN** every client receives the events in that order

### Requirement: Skill effects are drawn at the display time
A client SHALL show a skill effect when its display clock reaches the server time stamped on the event, not when the event arrives.

#### Scenario: Early arrival
- **WHEN** a skill event arrives before the display clock reaches its time
- **THEN** the effect starts only when the display clock reaches that time

#### Scenario: Late arrival
- **WHEN** a skill event arrives after the display clock has passed its time
- **THEN** the effect starts at once

### Requirement: Skill effects do not affect the simulation
Showing or failing to show a skill effect on a client SHALL NOT change the server's state.

#### Scenario: Client stops rendering
- **WHEN** a client stops drawing effects
- **THEN** the server and the other clients are unaffected
