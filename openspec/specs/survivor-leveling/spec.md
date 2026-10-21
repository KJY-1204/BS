# survivor-leveling Specification

## Purpose

Defines how survivors grow: experience is earned by killing mobs and shared with nearby teammates, experience turns into levels, and each level raises a few stats without healing.

## Requirements

### Requirement: Killing a mob gives experience at once
When a mob is killed, the server SHALL give experience immediately, without any item to pick up. The base amount SHALL be the same for every recipient of that kill; a recipient with an experience passive SHALL receive that base amount raised by the passive, and nobody else's amount SHALL change.

#### Scenario: A mob dies
- **WHEN** a projectile or a skill kills a mob
- **THEN** the experience is granted in the same server step as the kill

#### Scenario: A recipient with the experience passive
- **WHEN** a mob dies near two survivors and only one has the experience passive
- **THEN** that one receives the base amount raised by the passive and the other receives the base amount

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

### Requirement: Each level raises stats automatically
Each level gained SHALL raise the survivor's maximum health, maximum mana and projectile damage by fixed amounts from a growth table, and the survivor's mana recovery SHALL also include what the common passives and the character's own passive add. A level up SHALL NOT restore current health or current mana.

#### Scenario: Stats after a level up
- **WHEN** a survivor gains a level
- **THEN** the maximum health, maximum mana and projectile damage are higher by the table amounts

#### Scenario: No healing
- **WHEN** a survivor with 40 health out of 100 gains a level
- **THEN** current health is still 40 and the maximum is higher

#### Scenario: No mana refill
- **WHEN** a survivor with 20 mana gains a level
- **THEN** current mana is still 20 and the maximum is higher

#### Scenario: Mana recovery from a passive
- **WHEN** a survivor learns the mana-recovery passive of its own character
- **THEN** its mana recovery per second is higher by the passive's amount for its rank

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

### Requirement: Base values come from the character
A survivor's level-1 maximum health, maximum mana and mana recovery SHALL come from that survivor's character data. The per-level and per-skill increases SHALL be the common amounts and SHALL add to the character's base values, with the same no-heal rules.

#### Scenario: Level up on a character with a larger base
- **WHEN** two survivors with different base maximum health both gain one level
- **THEN** both maximums rise by the same amount, and the survivor with the larger base is still larger by the same difference

#### Scenario: Leaving the match resets growth
- **WHEN** the room returns to waiting
- **THEN** every survivor's level, experience, mana and learned skills are cleared, and the next match starts again from the character's base values
