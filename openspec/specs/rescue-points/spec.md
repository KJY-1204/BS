# rescue-points Specification

## Purpose
Defines the rescue point: a map objective that opens at intervals, is captured by survivors standing in it, slows the mobs inside it while open, and heals the survivors who captured it.

## Requirements

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
While a point is open, each mob whose position is inside its circle SHALL move at 60% of its speed. A mob outside, a stationary device, and the boss SHALL be unaffected. The presence of mobs SHALL not stop the progress.

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

### Requirement: The boss can contest the point
While a point is open and the boss stands (it exists and has not fallen) with its body overlapping the point's circle, the point SHALL be contested: its progress SHALL not rise, however many survivors stand inside, and the progress already made SHALL be kept. The point's life SHALL keep running while it is contested. A boss that has fallen, or a boss outside the circle, SHALL not contest. The boss SHALL not be slowed, hurt or blocked by the point.

#### Scenario: The boss stands in the circle
- **WHEN** a survivor stands in the circle and the boss's body overlaps it
- **THEN** the progress does not rise

#### Scenario: The boss leaves
- **WHEN** the boss leaves the circle while a survivor stands in it
- **THEN** the progress rises again from where it stopped

#### Scenario: The life keeps running
- **WHEN** the boss contests a point for 45 seconds
- **THEN** the point closes without a capture

#### Scenario: The boss is outside
- **WHEN** the boss stands next to the circle without overlapping it
- **THEN** the point is not contested

#### Scenario: A fallen boss
- **WHEN** the boss has fallen
- **THEN** it does not contest any point

### Requirement: Everyone sees that the point is contested
While a point is open, every player SHALL be able to tell whether it is contested: the progress bar SHALL change colour and its text SHALL say so, the boss player included.

#### Scenario: Contested
- **WHEN** the boss's body overlaps the open point
- **THEN** every client shows the bar as contested

#### Scenario: Not contested
- **WHEN** the boss leaves the circle
- **THEN** every client shows the normal bar again

### Requirement: The contest can be turned off
The harness SHALL accept an override `rescueContest`: 1 (the default) lets the boss contest, 0 lets it not, so that the boss's presence never stops a capture. Any other value SHALL be refused with a reason that names the override.

#### Scenario: Off
- **WHEN** a match runs with `rescueContest` 0 and the boss stands in the circle
- **THEN** the survivors inside still fill the progress

#### Scenario: Bad value
- **WHEN** `rescueContest` is 2
- **THEN** the settings are refused

### Requirement: Contesting a point speeds up the boss's skills
While a point is contested (the boss stands, its body overlaps the open point's circle and the contest is on), the cooldowns of all of the boss's skills SHALL run 50% faster than the real time. When the point is not contested, the cooldowns SHALL run at the normal rate. The warnings of announced skills, the damage and effects of skills, the boss's shots and the summon energy SHALL NOT be sped up. With the contest switched off, the cooldowns SHALL run at the normal rate wherever the boss stands.

#### Scenario: Holding the point
- **WHEN** the boss's body overlaps an open point and a skill has 6 seconds of cooldown left
- **THEN** after 4 seconds of match time the cooldown has run down

#### Scenario: Leaving the point
- **WHEN** the boss leaves the circle
- **THEN** its cooldowns run at the normal rate again

#### Scenario: Warnings are not sped up
- **WHEN** a skill is announced while the boss holds the point
- **THEN** its warning lasts as long as it does anywhere else

#### Scenario: No point
- **WHEN** no point is open, or the boss stands next to the circle without overlapping it
- **THEN** the cooldowns run at the normal rate

#### Scenario: The contest is off
- **WHEN** a match runs with `rescueContest` 0 and the boss stands in an open point
- **THEN** the cooldowns run at the normal rate
