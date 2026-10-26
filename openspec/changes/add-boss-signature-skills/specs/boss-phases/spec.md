# Spec Delta

## MODIFIED Requirements

### Requirement: Phase 2 unlocks a third boss skill on the F key
Phase 2 SHALL unlock a third boss skill, used with the F key, whose effect depends on the boss kind and is one the kind's Q and E skills do not have: the roar for the standard kind (its rally, aimed at the target), the shot ring for the giant, the shockwave for the sniper. Before phase 2 pressing F SHALL do nothing and start no cooldown, and the skill SHALL be ready the moment it is unlocked.

#### Scenario: Locked
- **WHEN** the boss player presses F in phase 1
- **THEN** nothing happens and no cooldown starts

#### Scenario: Unlocked
- **WHEN** the match is in phase 2 and the boss player presses F
- **THEN** the skill is announced with a warning, its cooldown starts and its effect is released when the warning has passed

#### Scenario: The kind's effect
- **WHEN** the boss player plays the standard kind, the giant or the sniper and uses the skill
- **THEN** it is the roar, the shot ring or the shockwave
