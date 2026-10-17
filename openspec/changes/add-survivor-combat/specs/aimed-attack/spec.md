# Spec Delta

## Purpose

Defines the survivor's basic attack: how the player aims and fires, how the server decides when a shot is fired, how projectiles fly and hit, and what they can and cannot damage.

## ADDED Requirements

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
The server SHALL fire at most one shot per cooldown period for each survivor while that survivor's player is holding fire, in the direction of the latest aim it received. A client SHALL NOT be able to fire faster than the cooldown allows.

#### Scenario: Holding fire
- **WHEN** a player holds fire for one second and the cooldown is 0.25 seconds
- **THEN** the server fires four shots

#### Scenario: Faster than the cooldown
- **WHEN** a client sends fire input far more often than the cooldown allows
- **THEN** the number of shots stays the same as with normal input

### Requirement: Aim input traffic is limited
A client SHALL send aim and fire state to the server only when it changes, and aim updates SHALL NOT be sent more than 20 times per second.

#### Scenario: Moving the mouse quickly
- **WHEN** the player sweeps the mouse across the screen while firing
- **THEN** the server receives no more than 20 aim updates per second from that client

#### Scenario: Standing still
- **WHEN** neither aim nor fire state changes
- **THEN** the client sends nothing

### Requirement: Projectiles fly straight
A projectile SHALL start at the firing survivor's position and fly in a straight line at a fixed speed, and SHALL disappear after a fixed lifetime or when it leaves the arena.

#### Scenario: Open space
- **WHEN** a projectile is fired and hits nothing
- **THEN** it disappears after its lifetime and has travelled speed times lifetime in a straight line

#### Scenario: Arena edge
- **WHEN** a projectile reaches the arena boundary
- **THEN** it disappears there

### Requirement: A projectile hits the first mob in its path
A projectile SHALL damage the first mob it touches and disappear. It SHALL NOT pass through that mob and hit another.

#### Scenario: Two mobs in a line
- **WHEN** a projectile flies toward two mobs standing one behind the other
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
