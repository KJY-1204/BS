# Spec Delta

## MODIFIED Requirements

### Requirement: Combat traffic stays within budget
With four survivors firing continuously, the boss firing continuously at its fastest rate and using both of its skills whenever they are ready, 100 mobs alive and every ranged mob shooting at its own rate, the combat events sent to each client SHALL average no more than 10 KB per second, including the blasts of exploding mobs and the boss's shockwaves.

#### Scenario: Full fire
- **WHEN** four survivors and the boss hold fire for one minute with 100 mobs alive, among them ranged and exploding mobs, and the boss uses its skills as often as it can
- **THEN** the measured combat traffic to each client averages no more than 10 KB per second
