# Spec Delta

## MODIFIED Requirements

### Requirement: Mobs chase the nearest survivor
Each mob SHALL move in a straight line toward the nearest living survivor, at its kind's speed, unless its kind's behavior says to stop (a ranged mob within its hold distance, an exploding mob with its fuse running). The boss character SHALL NOT be chosen as a default target.

#### Scenario: Single survivor
- **WHEN** one survivor is in the room
- **THEN** every mob moves toward that survivor

#### Scenario: Nearest changes
- **WHEN** another survivor becomes closer to a mob than its current target
- **THEN** the mob switches to the closer survivor

#### Scenario: Kinds move at their own speeds
- **WHEN** mobs of different kinds chase the same survivor
- **THEN** each moves at the speed of its own kind

#### Scenario: A mob that stops
- **WHEN** a ranged mob reaches its hold distance or an exploding mob starts its fuse
- **THEN** it stops moving until its behavior lets it move again
