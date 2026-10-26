# Spec Delta

## ADDED Requirements

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
