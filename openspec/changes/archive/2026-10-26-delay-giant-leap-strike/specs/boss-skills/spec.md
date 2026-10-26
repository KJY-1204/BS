# Spec Delta

## ADDED Requirements

### Requirement: A skill can start the match on cooldown
A boss skill SHALL be able to carry an opening cooldown in its data: the cooldown it starts a match with, 0 meaning it starts ready. The cooldown SHALL run down with the match like any other, a request for the skill before it has run down SHALL be refused as a cooldown, and a new match SHALL start the opening cooldown again from its full length. The giant's leap strike SHALL have an opening cooldown of 20 seconds; every other skill of every kind SHALL have none.

#### Scenario: The giant's first seconds
- **WHEN** a match with the giant has just started and the boss player presses Q
- **THEN** the leap strike is refused as being on cooldown and the boss's screen shows the seconds left

#### Scenario: After the opening cooldown
- **WHEN** 20 seconds of the match have passed
- **THEN** the leap strike can be used

#### Scenario: The other skills
- **WHEN** a match with the giant has just started and the boss player presses E
- **THEN** the roar is used at once, and the skills of the standard boss and the sniper are ready at once too

#### Scenario: A second match
- **WHEN** a second match starts after one in which the boss used the leap strike
- **THEN** the leap strike is on its full opening cooldown again

## MODIFIED Requirements

### Requirement: Cooldowns run with the match and start ready
Each skill's cooldown SHALL run down only while the match is running, and a new match SHALL start with every skill ready except one that has an opening cooldown (the giant's leap strike), which starts on that cooldown. The two cooldowns SHALL be independent: using one SHALL NOT start or change the other.

#### Scenario: A new match
- **WHEN** a match starts after one in which the boss used both skills
- **THEN** both skills are ready at once, except a skill with an opening cooldown, which is on it

#### Scenario: Independent skills
- **WHEN** the boss uses Q and then E
- **THEN** each has its own cooldown and E is not refused because of Q
