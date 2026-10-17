# boss-orders Specification

## Purpose

Defines the boss player's orders for summoned mobs: choosing a survivor or a point as their target, clearing the order, which mobs obey it, and when it stops applying, so that the boss can steer the mobs it calls.

## Requirements

### Requirement: The boss can order summoned mobs to a survivor
The boss player SHALL be able to name a survivor as the target of its summoned mobs. While the order stands, every summoned mob SHALL chase that survivor instead of the nearest one, including mobs summoned after the order was given. The server SHALL decide which survivor was named from the position the boss player pointed at, choosing the nearest living survivor within the pick distance.

#### Scenario: Order a survivor
- **WHEN** the boss player points at a survivor and gives the target command
- **THEN** every summoned mob on the field, and every one summoned afterwards, chases that survivor even if another survivor is nearer

#### Scenario: Nothing near the pointer
- **WHEN** the boss player gives the target command at a place with no living survivor within the pick distance
- **THEN** the command is treated as an order to that place (see below), not to a survivor

#### Scenario: Automatic mobs are not affected
- **WHEN** an order stands
- **THEN** automatically spawned mobs keep chasing the nearest survivor

### Requirement: The boss can order summoned mobs to a point
The boss player SHALL be able to name a point in the arena as the target of its summoned mobs. Summoned mobs under such an order SHALL go to the point and, when they arrive, SHALL go back to chasing the nearest survivor. A point outside the arena SHALL be moved to the nearest point inside it.

#### Scenario: Order a point
- **WHEN** the boss player gives the target command at an empty place
- **THEN** every summoned mob heads for that point regardless of where the survivors are

#### Scenario: Arrival
- **WHEN** a mob under a point order reaches the point
- **THEN** it resumes chasing the nearest survivor and the point order no longer applies to it

#### Scenario: A later summon
- **WHEN** the boss summons after giving a point order and before the point is cleared or replaced
- **THEN** the new mobs also head for the point

#### Scenario: Outside the arena
- **WHEN** the boss player orders a point beyond the arena edge
- **THEN** the point used is the nearest one inside the arena

### Requirement: An order can be replaced or cleared
A new order SHALL replace the standing one for all summoned mobs. The boss player SHALL be able to clear the order, after which summoned mobs chase the nearest survivor again.

#### Scenario: Replace
- **WHEN** the boss player gives a survivor order and then a point order
- **THEN** all summoned mobs follow the point order only

#### Scenario: Clear
- **WHEN** the boss player gives the clear command
- **THEN** every summoned mob goes back to chasing the nearest survivor, as do mobs summoned afterwards

### Requirement: An order ends when its target can no longer be followed
If the ordered survivor falls, the summoned mobs SHALL chase the nearest living survivor meanwhile and SHALL return to the ordered survivor when that survivor is revived and the order still stands. If the ordered survivor leaves the room, or the boss leaves, the order SHALL be cleared. All orders SHALL be cleared when the match ends.

#### Scenario: Target falls
- **WHEN** the ordered survivor is knocked down
- **THEN** summoned mobs chase the nearest living survivor

#### Scenario: Target revived
- **WHEN** the ordered survivor is revived while the order still stands
- **THEN** summoned mobs chase that survivor again

#### Scenario: Target leaves
- **WHEN** the ordered survivor leaves the room
- **THEN** the order is cleared and summoned mobs chase the nearest survivor

#### Scenario: Match ends
- **WHEN** the match ends or the room returns to waiting
- **THEN** no order stands in the next match

### Requirement: Only the boss gives orders, and only during a match
The server SHALL ignore an order from anyone but the boss player, and SHALL ignore orders while the room is not playing or while the boss has fallen.

#### Scenario: A survivor sends an order
- **WHEN** a survivor's client sends an order
- **THEN** the server ignores it and every mob keeps its current target

#### Scenario: After the match
- **WHEN** the boss gives an order while the result is shown
- **THEN** nothing changes

### Requirement: The boss sees its order
The boss player SHALL see the standing order: a marker on the ordered survivor or at the ordered point, and a text of the order kind. The marker SHALL be removed when the order is cleared or ends. The order SHALL be sent only to the boss player.

#### Scenario: Survivor marker
- **WHEN** a survivor order stands
- **THEN** the boss player sees a marker on that survivor that follows them

#### Scenario: Point marker
- **WHEN** a point order stands
- **THEN** the boss player sees a marker at the point

#### Scenario: Cleared
- **WHEN** the order is cleared
- **THEN** the marker disappears

#### Scenario: Survivors do not see it
- **WHEN** an order stands
- **THEN** survivors see no marker and receive no order information
