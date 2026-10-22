# Spec Delta

## Purpose
Defines the devices the boss places on the field: how and when the boss places one, what a device is (a still body with health and a lifetime that keeps making mobs), how the survivors destroy it, and what everyone sees of it.

## ADDED Requirements

### Requirement: The boss can place a device
While a match is running and the boss is standing, the boss player SHALL be able to place a device at the boss's position with one command. Only the boss player SHALL be able to place, and a request from anyone else SHALL be ignored. Placing SHALL cost summon energy and SHALL start a cooldown of its own that is separate from the summon cooldown. A device SHALL NOT be placed closer than the safe distance to a living survivor. The energy cost, the cooldown, the safe distance and the limit below SHALL be adjustable without changing rules elsewhere.

#### Scenario: A placement
- **WHEN** the boss player gives the place command with enough energy, no cooldown running, room under the limit and no living survivor within the safe distance
- **THEN** a device appears at the boss's position, the energy drops by the cost and the placing cooldown starts

#### Scenario: Not the boss
- **WHEN** a survivor's client sends a place request
- **THEN** the server ignores it and no device is created

#### Scenario: Not enough energy
- **WHEN** the boss has less energy than a device costs and gives the place command
- **THEN** no device is created, no energy is taken and the boss is told it failed

#### Scenario: Survivor too close
- **WHEN** a living survivor stands within the safe distance of the boss and the boss gives the place command
- **THEN** no device is created, no energy is taken and the boss is told it failed

#### Scenario: Summoning is separate
- **WHEN** the boss has just summoned mobs and then places a device
- **THEN** the summon cooldown does not refuse the placement, and the energy is shared between them

### Requirement: Placing is refused when it cannot happen
The server SHALL refuse a placement, take no energy and create no device, when the room is not playing, when the boss has fallen, when the placing cooldown has not passed, when the boss already has the maximum number of devices standing, or when the mob hard cap is reached. A device SHALL count as one toward the mob hard cap, like each mob.

#### Scenario: Match over
- **WHEN** the boss gives the place command after the match has ended
- **THEN** nothing is created

#### Scenario: Boss fallen
- **WHEN** the boss is at zero health and gives the place command
- **THEN** nothing is created

#### Scenario: Cooldown
- **WHEN** a second placement is requested before the cooldown has passed
- **THEN** it is refused and no energy is taken

#### Scenario: Limit reached
- **WHEN** the boss has the maximum number of devices standing and gives the place command
- **THEN** it is refused and no energy is taken, and the boss can place again once one of them is gone

#### Scenario: Field full
- **WHEN** the mob count, with devices counted, equals the hard cap and the boss gives the place command
- **THEN** nothing is created and no energy is taken

### Requirement: A device keeps making mobs
A device SHALL stand still. At a fixed interval, counted from its placing, it SHALL create a small fixed number of basic mobs of the summoned origin on a ring around itself, inside the arena and under the mob hard cap. A position closer than the safe distance to a living survivor SHALL NOT be used; fewer mobs SHALL be created if no position is allowed, and none if the hard cap leaves no room. These mobs SHALL cost no summon energy, SHALL obey the boss's standing order like any other summoned mobs, and SHALL be shown like any other summoned mobs. The interval, the number and the ring SHALL be adjustable without changing rules elsewhere.

#### Scenario: Production
- **WHEN** a device has stood for the production interval
- **THEN** it creates its group of mobs around itself and starts counting the next interval

#### Scenario: A survivor stands in the ring
- **WHEN** a survivor stands where a mob of the device would appear
- **THEN** that mob is placed elsewhere on the ring, or not created if no allowed position is found

#### Scenario: Mob cap
- **WHEN** the hard cap leaves room for fewer mobs than a device would create
- **THEN** only those that fit are created

#### Scenario: No energy used
- **WHEN** a device creates mobs
- **THEN** the boss's summon energy is unchanged

#### Scenario: Orders
- **WHEN** the boss has given an order and a device creates mobs
- **THEN** the new mobs follow the order like other summoned mobs

#### Scenario: Time stop
- **WHEN** a survivor's skill has stopped the mobs
- **THEN** a device keeps counting and keeps making mobs

### Requirement: A device has health and a limited life
A device SHALL have a maximum health and a lifetime, both adjustable without changing rules elsewhere. When its health reaches zero it SHALL be destroyed. When its lifetime has passed it SHALL disappear by itself. A device SHALL NOT be repaired or healed. A device SHALL NOT hurt a survivor by touch or by any other means, and survivors SHALL NOT be blocked by it. A device SHALL NOT be moved or stopped by any skill. All devices SHALL be removed when the match ends or the room returns to waiting, and a new match SHALL start with none.

#### Scenario: Destroyed
- **WHEN** a device's health reaches zero
- **THEN** it is removed and stops making mobs

#### Scenario: Expiry
- **WHEN** a device's lifetime has passed without it being destroyed
- **THEN** it is removed and stops making mobs

#### Scenario: Touching a device
- **WHEN** a survivor stands against a device
- **THEN** the survivor takes no damage and can walk through

#### Scenario: Pushed or stopped
- **WHEN** a survivor's skill pushes the mobs away or stops them
- **THEN** every device stays where it is and keeps working

#### Scenario: Match ends
- **WHEN** the match ends or the room returns to waiting
- **THEN** no device remains and the next match starts with none

### Requirement: Survivors can destroy a device
A survivor's projectile SHALL damage a device when it touches the device body, by the projectile's own damage, and a projectile that cannot pierce SHALL stop at it. A piercing projectile SHALL count a device as one of the targets it may hit and SHALL hit it at most once. The area burst, the beam and the ring of knives SHALL damage a device under the same conditions as they damage mobs. The skills that stop mobs or push them away, the heal and the barrier SHALL NOT affect a device. Boss projectiles and boss skills SHALL NOT damage a device.

#### Scenario: A shot reaches a device
- **WHEN** a projectile with damage 14 touches a device
- **THEN** the device loses 14 health and the projectile ends there

#### Scenario: A piercing shot
- **WHEN** a piercing projectile that can hit four targets passes a mob and then a device
- **THEN** it damages both and keeps flying with two hits left

#### Scenario: Area and line skills
- **WHEN** the area burst fires with a device inside its radius, or the beam fires along a line that crosses one
- **THEN** the device takes the skill's damage

#### Scenario: Not hurt by the boss
- **WHEN** a boss projectile or a boss skill reaches a device
- **THEN** the device takes no damage

#### Scenario: Wind and time
- **WHEN** a survivor pushes the mobs around her or stops time while a device stands inside the radius
- **THEN** the device is not moved and keeps working

### Requirement: Everyone sees the devices
Every player SHALL see each device where it stands, in a look and size different from every mob kind, together with a health bar over it that shows the health it has left. A device SHALL be shown on every client from the moment it is placed until it is destroyed or expires, and a destroyed device SHALL be shown hit like a mob hit. A player who joins a room in progress SHALL see the devices that stand. Showing or not showing a device on a client SHALL NOT change the simulation, and no new kind of network message SHALL be needed for it.

#### Scenario: A device appears
- **WHEN** the boss places a device
- **THEN** every client shows it at that place with a full health bar

#### Scenario: A device is hurt
- **WHEN** a survivor's shot damages a device
- **THEN** every client shows a hit effect and a lower health bar

#### Scenario: A device goes away
- **WHEN** a device is destroyed or expires
- **THEN** every client stops showing it

#### Scenario: Late joiner
- **WHEN** a player joins while a device stands
- **THEN** that player sees the device at its place, with a full health bar that shows the true health from the next hit on it

### Requirement: The boss sees its device key and count
The boss player's screen SHALL show the key for placing a device, how many devices the boss has standing out of the limit, and either "ready" or the remaining seconds of the placing cooldown. A refused placement SHALL give a short visible sign. The server SHALL send this only to the boss player, and survivors SHALL NOT receive or show it.

#### Scenario: Boss screen
- **WHEN** a match is running and the boss player looks at the screen
- **THEN** the place key, the count of standing devices out of the limit and the cooldown state are shown with the other boss information

#### Scenario: Survivor screen
- **WHEN** a survivor looks at the screen
- **THEN** no device key, count or cooldown is shown and none reached the survivor's client

#### Scenario: Refused
- **WHEN** the boss gives the place command and it is refused
- **THEN** a short notice or flash tells the boss
