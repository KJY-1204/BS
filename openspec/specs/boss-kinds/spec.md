# boss-kinds Specification

## Purpose
Defines the kinds of boss the boss player can play: each kind as data (stats, basic attack and two skills), which kinds exist, how the boss player chooses one in the waiting room, and how the chosen kind applies to the match and is known to everyone.

## Requirements

### Requirement: Boss kinds are defined as data
A boss kind SHALL be defined in data with an identifying number, a display name, a maximum health, a movement speed, a body radius, a contact damage value, a color, a basic attack (damage, cooldown and the shape of its projectile) and two skills for the Q and E keys (each with an effect, a cooldown, a damage value, a warning time and the values its effect needs). The data SHALL be changeable without changing the rules that use it.

#### Scenario: Complete definitions
- **WHEN** every boss kind is examined
- **THEN** each has a name, a color, positive stats, a basic attack and two skills with their values

### Requirement: Each kind has its own skills
Each kind SHALL give its Q and E skills one of the available effects: the shot ring, the shockwave, the beam or the roar. The standard kind SHALL have the shot ring on Q and the shockwave on E, the giant the shockwave on Q and the roar on E, and the sniper the shot ring on Q and the beam on E. Each skill's cooldown, damage, warning time and effect values SHALL come from the kind's data.

#### Scenario: The giant's skills
- **WHEN** the boss player plays the giant and presses Q and then E
- **THEN** Q announces and releases the shockwave and E announces and releases the roar

#### Scenario: The sniper's skills
- **WHEN** the boss player plays the sniper and presses Q and then E
- **THEN** Q announces and releases the shot ring and E announces and releases the beam

#### Scenario: Values from the kind
- **WHEN** a skill is used
- **THEN** its cooldown, damage and warning time are those of the playing kind's skill

### Requirement: The boss player chooses the kind in the waiting room
While the room is waiting, the player who holds the boss role SHALL be able to choose which kind of boss to play and to change the choice, and every player SHALL see the chosen kind. A request from anyone who is not the boss player, a request for a kind that does not exist, and a request while the match is playing SHALL be ignored. A boss player who has chosen nothing SHALL play the standard kind.

#### Scenario: Choosing
- **WHEN** the boss player asks for the sniper in the waiting room
- **THEN** every client shows that the boss will be the sniper

#### Scenario: Changing the choice
- **WHEN** the boss player has chosen the giant and then asks for the sniper
- **THEN** the sniper is the chosen kind

#### Scenario: Not the boss
- **WHEN** a survivor asks for a boss kind
- **THEN** the server ignores the request and nothing changes

#### Scenario: Unknown kind
- **WHEN** the boss player asks for a kind number that does not exist
- **THEN** the server ignores the request and the earlier choice stays

#### Scenario: During a match
- **WHEN** the boss player asks for another kind while the match is playing
- **THEN** the request is ignored and the match keeps its kind

#### Scenario: Nothing chosen
- **WHEN** the host starts a match and the boss player has chosen no kind
- **THEN** the boss is the standard kind

### Requirement: The match uses the chosen kind
When a match with a boss starts, the chosen kind SHALL decide the boss's maximum health, speed, size, contact damage, color, basic attack and skills for that match. A second match SHALL use the kind chosen for it, with nothing carried over from the first.

#### Scenario: Stats of the kind
- **WHEN** a match starts with the giant
- **THEN** the boss has the giant's maximum health, moves at the giant's speed, is as large as the giant and hurts on contact as much as the giant does

#### Scenario: A second match
- **WHEN** a second match starts after the first ended and the boss player chose another kind
- **THEN** the second match uses the new kind and none of the first match's cooldowns or health

#### Scenario: Practice match
- **WHEN** a match starts with no boss player
- **THEN** no boss kind is used and nothing about kinds is shown

### Requirement: Everyone knows the kind of the match
Every client SHALL learn the kind when a match with a boss starts, so everyone draws and expects the same boss: its name, color and size, its speed for the boss player's own movement, the cooldown and projectile shape of its basic attack, the warnings of its skills and the skill slots shown to the boss player.

#### Scenario: Seen by everyone
- **WHEN** a match starts with the sniper
- **THEN** every client draws the sniper's color and size and shows its name in the boss health display

#### Scenario: The boss player's own view
- **WHEN** the boss player plays the giant
- **THEN** the boss player moves at the giant's speed, fires at the giant's cooldown and sees the giant's two skills on the skill slots

### Requirement: There are three boss kinds that last for minutes
There SHALL be three kinds: a standard kind, a giant that has more health, moves slower, is larger and hits harder on contact than the standard kind, and a sniper that has less health, moves faster, is smaller and fires its basic attack faster than the standard kind. Every kind SHALL have a great deal more health than a survivor, so that a match with a boss lasts for minutes and not seconds.

#### Scenario: The kinds differ
- **WHEN** the three kinds are listed
- **THEN** each has its own name, color and numbers, and the giant and the sniper differ from the standard kind in the ways above

#### Scenario: Health for a long match
- **WHEN** the health of each kind is compared with the damage one survivor deals with a basic attack in one second
- **THEN** it is at least a thousand times as large for every kind

#### Scenario: Changing a value
- **WHEN** one value of one kind is changed in the data
- **THEN** the next match with that kind uses it and no rule needs to change

### Requirement: No boss is as fast as a survivor
Every boss kind SHALL move slower than the slowest survivor character, so that a survivor who runs from the boss is never caught by the boss moving after it. The order of the kinds by speed SHALL stay giant, standard, sniper.

#### Scenario: Slower than every survivor
- **WHEN** the speed of each boss kind is compared with the speed of each survivor character
- **THEN** every boss kind is slower than every survivor character

#### Scenario: The order of the kinds
- **WHEN** the three kinds are compared by speed
- **THEN** the giant is slower than the standard kind and the standard kind is slower than the sniper

#### Scenario: A survivor outruns the boss
- **WHEN** a survivor walks straight away from a boss that walks straight after it
- **THEN** the distance between them grows

### Requirement: The kind choice shows each kind's trait
The list the boss player chooses a kind from in the waiting room SHALL show, for each kind, the trait's name beside the kind's skills.

#### Scenario: Reading the choices
- **WHEN** the boss player looks at the kinds in the waiting room
- **THEN** every kind shows its trait name

