# Spec Delta

## MODIFIED Requirements

### Requirement: Hits and endings are reliable
The server SHALL tell clients reliably when a projectile hits a mob, the boss or a survivor, and when it ends, so a projectile is never left on screen after it ended. A hit that stops the projectile SHALL end it on every client at the hit position. A hit by a piercing projectile that continues SHALL be marked as such and SHALL NOT end the projectile; such a projectile SHALL end with its last hit or with an ending event. A hit event SHALL say whether its target was a survivor, so a client can tell a hit on a survivor from a hit on a mob.

#### Scenario: Hit event
- **WHEN** a projectile hits a mob and stops
- **THEN** every client removes the projectile at the hit position and shows a short hit effect

#### Scenario: Piercing hit
- **WHEN** a piercing projectile hits a mob and keeps flying
- **THEN** every client shows the hit effect at that moment and the projectile keeps flying on screen

#### Scenario: Hit on a survivor
- **WHEN** a boss projectile hits a survivor
- **THEN** every client removes the projectile at the hit position, shows a hit effect there and treats the target as a survivor, not as a mob

#### Scenario: Lifetime end
- **WHEN** a projectile ends without hitting anything
- **THEN** every client removes it

#### Scenario: Missing end event
- **WHEN** a client has displayed a projectile for longer than its lifetime plus a margin
- **THEN** the client removes it

### Requirement: Combat traffic stays within budget
With four survivors firing continuously, the boss firing continuously at its fastest rate and 100 mobs alive, the combat events sent to each client SHALL average no more than 10 KB per second.

#### Scenario: Full fire
- **WHEN** four survivors and the boss hold fire for one minute with 100 mobs alive
- **THEN** the measured combat traffic to each client averages no more than 10 KB per second
