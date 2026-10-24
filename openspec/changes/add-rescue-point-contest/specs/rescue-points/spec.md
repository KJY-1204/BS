# Spec Delta

## ADDED Requirements

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

## MODIFIED Requirements

### Requirement: Mobs inside an open point are slowed
While a point is open, each mob whose position is inside its circle SHALL move at 60% of its speed. A mob outside, a stationary device, and the boss SHALL be unaffected. The presence of mobs SHALL not stop the progress.

#### Scenario: A mob in the circle
- **WHEN** a mob chases through an open point
- **THEN** it moves at 60% of its speed while inside and at full speed outside

#### Scenario: After closing
- **WHEN** the point closes
- **THEN** mobs move at full speed again
