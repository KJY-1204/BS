# Spec Delta

## MODIFIED Requirements

### Requirement: Experience turns into levels
Each survivor SHALL have their own level, starting at the first level. When a survivor's experience reaches the amount needed for the next level, the survivor SHALL gain a level and keep the experience beyond that amount. The amount needed SHALL NOT become smaller as the level rises.

#### Scenario: Reaching the threshold
- **WHEN** a survivor's experience reaches the amount needed for the next level
- **THEN** the survivor's level rises by one and the surplus carries over

#### Scenario: A large grant
- **WHEN** one grant is large enough for two levels
- **THEN** the survivor gains two levels, one after the other

#### Scenario: Later levels never get cheaper
- **WHEN** the amounts needed for every level from the first up to the cap are compared in order
- **THEN** each amount is at least as large as the one before it

### Requirement: Levels have a cap
A survivor SHALL NOT go above the level cap, which is the twentieth level. At the cap, further experience SHALL be ignored.

#### Scenario: At the cap
- **WHEN** a survivor at the level cap receives experience
- **THEN** the level and the stats stay unchanged

#### Scenario: Reaching the cap
- **WHEN** a survivor is one level below the cap and gains enough experience for two levels
- **THEN** the survivor ends at the cap, with a skill choice for each level gained, and the extra experience is dropped

#### Scenario: Nothing left to learn
- **WHEN** a survivor gains a level while no skill is left to offer
- **THEN** the level up goes through without holding up later choices
