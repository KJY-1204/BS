# Spec Delta

## Purpose
Defines the rescue point: a map objective that opens at intervals, is captured by survivors standing in it, slows the mobs inside it while open, and heals the survivors who captured it.

## ADDED Requirements

### Requirement: A rescue point opens on a schedule
In a match with a boss, one rescue point SHALL open at 90 seconds of match time and, after each one ends, the next SHALL open 120 seconds later. At most one SHALL be open at a time. A point SHALL close when it is captured or when it has been open for 45 seconds. A match without a boss, a boss that has fallen, and a match that has ended SHALL open none.

#### Scenario: The first point
- **WHEN** 90 seconds of a match have passed
- **THEN** a rescue point is open

#### Scenario: The next point
- **WHEN** a point has been captured at 100 seconds
- **THEN** no point is open until 220 seconds

#### Scenario: Not captured
- **WHEN** a point has been open for 45 seconds without being captured
- **THEN** it closes and the next opens 120 seconds later

#### Scenario: No boss
- **WHEN** a match is played without a boss
- **THEN** no point ever opens

### Requirement: The place comes from the match seed
A point's centre SHALL be chosen from the match seed inside the arena, with its whole circle inside the arena and its centre at least 12 units from the boss's start position. The same seed SHALL give the same points in the same order.

#### Scenario: Repeatable
- **WHEN** two matches use the same seed
- **THEN** their points open at the same places

#### Scenario: Inside the arena
- **WHEN** any point opens
- **THEN** its circle lies inside the arena

### Requirement: Survivors standing in the point capture it
While a point is open, its progress SHALL rise each step by the number of living survivors inside its circle divided by 12 seconds. A survivor who has fallen SHALL not count. The progress SHALL not fall while nobody is inside. At full progress the point is captured.

#### Scenario: One survivor
- **WHEN** one survivor stands in the circle for 12 seconds
- **THEN** the point is captured

#### Scenario: Four survivors
- **WHEN** four survivors stand in the circle
- **THEN** the point is captured in 3 seconds

#### Scenario: Leaving
- **WHEN** a survivor stands in the circle for 6 seconds and leaves
- **THEN** the progress stays at half

#### Scenario: A fallen survivor
- **WHEN** a fallen survivor lies in the circle
- **THEN** the progress does not rise

### Requirement: A capture heals the survivors inside
When a point is captured, every living survivor inside its circle at that moment SHALL be healed by 50% of their maximum health, up to the maximum. Nobody SHALL be revived and no experience SHALL be given.

#### Scenario: Healing
- **WHEN** a point is captured with two hurt survivors inside and one outside
- **THEN** the two inside are healed by half their maximum and the third is not

#### Scenario: No revival
- **WHEN** a fallen survivor lies in the circle at the capture
- **THEN** they stay fallen

### Requirement: Mobs inside an open point are slowed
While a point is open, each mob whose position is inside its circle SHALL move at 60% of its speed. A mob outside, a stationary device, and the boss SHALL be unaffected. The presence of the boss or of mobs SHALL not stop the progress.

#### Scenario: A mob in the circle
- **WHEN** a mob chases through an open point
- **THEN** it moves at 60% of its speed while inside and at full speed outside

#### Scenario: After closing
- **WHEN** the point closes
- **THEN** mobs move at full speed again

### Requirement: Everyone sees the point
While a point is open, every player SHALL see its circle at the same place and a progress bar that shows its progress and the seconds left before it closes, the boss player included.

#### Scenario: Seen by all
- **WHEN** a point opens
- **THEN** every client draws the circle and the bar

### Requirement: The points can be turned off
The harness SHALL accept an override `rescuePoints`: 1 (the default) opens points, 0 opens none. Any other value SHALL be refused with a reason that names the override.

#### Scenario: Off
- **WHEN** a match runs with `rescuePoints` 0
- **THEN** no point opens and mobs are never slowed by one

#### Scenario: Bad value
- **WHEN** `rescuePoints` is 2
- **THEN** the settings are refused
