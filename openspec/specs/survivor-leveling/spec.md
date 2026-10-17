# survivor-leveling Specification

## Purpose

Defines how survivors grow: experience is earned by killing mobs and shared with nearby teammates, experience turns into levels, and each level raises a few stats without healing.

## Requirements

### Requirement: Killing a mob gives experience at once
When a mob is killed, the server SHALL give experience immediately, without any item to pick up. The amount SHALL be the same for every recipient of that kill.

#### Scenario: A mob dies
- **WHEN** a projectile or a skill kills a mob
- **THEN** the experience is granted in the same server step as the kill

### Requirement: Nearby living survivors share each kill
Every living survivor within the share distance of the place where the mob died SHALL receive the full experience for that kill, whoever dealt the damage. Dead survivors and the boss SHALL NOT receive experience.

#### Scenario: Two survivors near the kill
- **WHEN** a mob dies with survivor A 4 units away and survivor B 6 units away, and the share distance is 10
- **THEN** both A and B receive the full amount

#### Scenario: A survivor too far away
- **WHEN** a mob dies and survivor C is farther than the share distance
- **THEN** C receives nothing for that kill

#### Scenario: The killer is far from the mob
- **WHEN** a survivor's shot kills a mob that is beyond the share distance from that survivor
- **THEN** that survivor receives nothing and anyone near the mob does

#### Scenario: Dead and boss
- **WHEN** a mob dies next to a dead survivor and the boss
- **THEN** neither receives experience

### Requirement: Experience turns into levels
Each survivor SHALL have their own level, starting at the first level. When a survivor's experience reaches the amount needed for the next level, the survivor SHALL gain a level and keep the experience beyond that amount.

#### Scenario: Reaching the threshold
- **WHEN** a survivor's experience reaches the amount needed for the next level
- **THEN** the survivor's level rises by one and the surplus carries over

#### Scenario: A large grant
- **WHEN** one grant is large enough for two levels
- **THEN** the survivor gains two levels, one after the other

### Requirement: Levels have a cap
A survivor SHALL NOT go above the level cap. At the cap, further experience SHALL be ignored.

#### Scenario: At the cap
- **WHEN** a survivor at the level cap receives experience
- **THEN** the level and the stats stay unchanged

### Requirement: Each level raises stats automatically
Each level gained SHALL raise the survivor's maximum health, maximum mana and projectile damage by fixed amounts from a growth table. A level up SHALL NOT restore current health or current mana.

#### Scenario: Stats after a level up
- **WHEN** a survivor gains a level
- **THEN** the maximum health, maximum mana and projectile damage are higher by the table amounts

#### Scenario: No healing
- **WHEN** a survivor with 40 health out of 100 gains a level
- **THEN** current health is still 40 and the maximum is higher

#### Scenario: No mana refill
- **WHEN** a survivor with 20 mana gains a level
- **THEN** current mana is still 20 and the maximum is higher

### Requirement: Levels are visible to everyone, progress only to the owner
Every player SHALL see every survivor's level over the character. Only the owner SHALL see their own experience progress toward the next level.

#### Scenario: Seeing a teammate's level
- **WHEN** a survivor gains a level
- **THEN** every client shows the new level over that survivor

#### Scenario: Progress is private
- **WHEN** a survivor earns experience
- **THEN** only that survivor's player sees the experience bar change

### Requirement: Growth lasts for the room only
All levels, experience and skill choices SHALL be reset when the room ends, and a new room SHALL start every survivor at the first level.

#### Scenario: New room
- **WHEN** the host closes the room and opens a new one
- **THEN** every survivor starts at the first level with no experience and no skills
