# aimed-attack Specification

## Purpose

Defines the survivor's basic attack: how the player aims and fires, how the server decides when a shot is fired, how projectiles fly and hit, and what they can and cannot damage.

## Requirements

### Requirement: The player aims and fires directly
A survivor's player SHALL aim with the mouse pointer, as the direction from their own character to the pointer's world position, or with the gamepad right stick, and SHALL fire while the fire button is held (mouse left button or gamepad right trigger).

#### Scenario: Mouse aim
- **WHEN** the player holds the left mouse button with the pointer to the right of the character
- **THEN** shots fly to the right

#### Scenario: Gamepad aim
- **WHEN** the player holds the right trigger and points the right stick up
- **THEN** shots fly up

#### Scenario: Release
- **WHEN** the player releases the fire button
- **THEN** no further shots are fired

### Requirement: The server decides when shots are fired
The server SHALL fire at most one attack per cooldown period for each survivor while that survivor's player is holding fire, in the direction of the latest aim it received, and one attack SHALL fire the number of shots its character defines. A client SHALL NOT be able to fire faster than the cooldown allows.

#### Scenario: Holding fire
- **WHEN** a player holds fire for one second and the cooldown is 0.25 seconds
- **THEN** the server fires four attacks

#### Scenario: Faster than the cooldown
- **WHEN** a client sends fire input far more often than the cooldown allows
- **THEN** the number of attacks stays the same as with normal input

#### Scenario: A multi-shot attack
- **WHEN** a character whose attack has five shots fires once
- **THEN** the server fires five shots for that one attack and starts one cooldown

### Requirement: Aim input traffic is limited
A client SHALL send aim and fire state to the server only when it changes, and aim updates SHALL NOT be sent more than 20 times per second.

#### Scenario: Moving the mouse quickly
- **WHEN** the player sweeps the mouse across the screen while firing
- **THEN** the server receives no more than 20 aim updates per second from that client

#### Scenario: Standing still
- **WHEN** neither aim nor fire state changes
- **THEN** the client sends nothing

### Requirement: Projectiles fly straight
A projectile SHALL start at the firing survivor's position and fly in a straight line at the speed of the shooter's character, and SHALL disappear after that character's lifetime or when it leaves the arena. Speed, lifetime and size SHALL follow the character that fired it.

#### Scenario: Open space
- **WHEN** a projectile is fired and hits nothing
- **THEN** it disappears after its lifetime and has travelled speed times lifetime in a straight line

#### Scenario: Arena edge
- **WHEN** a projectile reaches the arena boundary
- **THEN** it disappears there

#### Scenario: Different characters
- **WHEN** two survivors of different characters fire in open space
- **THEN** each projectile travels at its own character's speed for its own character's lifetime

### Requirement: A projectile hits the first mob in its path
A projectile that can hit one mob SHALL damage the first mob it touches and disappear, and SHALL NOT pass through that mob and hit another. A projectile that can hit several mobs SHALL follow the rules of piercing shots instead.

#### Scenario: Two mobs in a line
- **WHEN** a projectile that can hit one mob flies toward two mobs standing one behind the other
- **THEN** only the nearer mob takes damage and the projectile disappears

#### Scenario: Fast projectile
- **WHEN** a projectile moves farther in one simulation step than a mob's width
- **THEN** it still hits a mob that lies on its path

### Requirement: Projectiles only harm mobs
A projectile SHALL NOT damage the boss character or any survivor, and SHALL pass through them.

#### Scenario: Teammate in the way
- **WHEN** a projectile passes through another survivor's position
- **THEN** that survivor takes no damage and the projectile continues

### Requirement: Only living survivors can fire
A survivor SHALL be able to fire only while alive, and the boss SHALL NOT be able to fire survivor projectiles.

#### Scenario: Dead survivor holds fire
- **WHEN** a dead survivor's player holds fire
- **THEN** no projectile is created

### Requirement: The number of projectiles is capped
The system SHALL limit the number of simultaneous projectiles to a hard cap, and SHALL refuse new shots while the cap is reached.

#### Scenario: Cap reached
- **WHEN** a shot is requested while the projectile count equals the cap
- **THEN** no projectile is created and the count stays at the cap

### Requirement: Damage follows the shooter's own value
Each projectile SHALL deal the damage value its shooter had when it was fired, so a later change to the shooter's damage does not alter projectiles already in flight.

#### Scenario: Two survivors with different damage
- **WHEN** survivor A has damage 12 and survivor B has damage 10, and both hit a mob
- **THEN** the mob takes 12 from A's projectile and 10 from B's

#### Scenario: Damage changes in flight
- **WHEN** a survivor levels up while a projectile is in flight
- **THEN** that projectile still deals the damage it was fired with

### Requirement: The cooldown follows the shooter's own value
The firing cooldown SHALL follow each survivor's own value, which can be shortened by skills, and SHALL return to normal when the effect ends.

#### Scenario: A shorter cooldown
- **WHEN** a survivor's cooldown is halved by a skill and the survivor holds fire for one second
- **THEN** the survivor fires twice as many shots as an unmodified survivor

#### Scenario: Effect ends
- **WHEN** the skill effect ends
- **THEN** the survivor's cooldown is the normal one again
