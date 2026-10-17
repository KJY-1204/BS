# mob-replication Specification

## Purpose

Defines how the server's mobs are shown to every player: each client sees the same mobs moving smoothly, newly joined players see the current state, and the traffic stays within a small bandwidth budget.

## Requirements

### Requirement: Clients see the server's mobs
Every client SHALL display exactly the mobs that exist on the server, at positions that follow the server's positions.

#### Scenario: Matching mob set
- **WHEN** the server has N mobs alive
- **THEN** each client displays N mobs, one for each server mob

#### Scenario: Positions follow the server
- **WHEN** a mob is stationary or moving on the server
- **THEN** each client shows it within 0.5 world units of the server position, allowing for the display delay

### Requirement: Mob motion looks smooth
Clients SHALL show mob movement as continuous motion by blending between received positions with a short display delay of about 130 ms.

#### Scenario: Normal conditions
- **WHEN** a mob moves continuously and snapshots arrive at the normal rate
- **THEN** its displayed path is continuous with no jumps

#### Scenario: One snapshot is lost
- **WHEN** a single snapshot is lost
- **THEN** the displayed motion does not jump and continues from the next received snapshot

### Requirement: Mob appearance and removal are reliable
The server SHALL tell clients reliably when a mob appears and when it is removed, so a mob is never left on screen after it is gone and never missing after it appears.

#### Scenario: Mob spawns
- **WHEN** the server spawns a mob
- **THEN** every client shows a new mob at its spawn position

#### Scenario: Mob removed
- **WHEN** the server removes a mob
- **THEN** every client stops displaying that mob

#### Scenario: Stale mob
- **WHEN** a client has not received any position for a displayed mob for 2 seconds
- **THEN** the client removes that mob from the display

### Requirement: Late joiners see current mobs
A player who joins a room in progress SHALL see all existing mobs at their current positions without waiting for new spawns.

#### Scenario: Join mid-game
- **WHEN** a player joins while mobs are alive
- **THEN** within 1 second the player's screen shows all current mobs at their current positions

### Requirement: Traffic stays within budget
With 100 mobs alive, the mob data sent to each client SHALL average no more than 50 KB per second, and no single network packet SHALL exceed the transport's packet size limit.

#### Scenario: Full cap
- **WHEN** 100 mobs are alive and chasing for one minute
- **THEN** the measured mob traffic to each client averages no more than 50 KB per second

#### Scenario: Large mob count
- **WHEN** the mob count is at the hard cap
- **THEN** all mob positions are delivered each update without any packet being dropped for size

### Requirement: Mob display does not affect the simulation
Showing, hiding or failing to show a mob on a client SHALL NOT change the server's mob state.

#### Scenario: Client stops rendering
- **WHEN** a client stops displaying mobs, for example while minimized
- **THEN** the server's mob simulation and the other clients are unaffected

### Requirement: A removed mob disappears at its display time
A client SHALL stop displaying a removed mob at the moment on its display clock that matches when the server removed it, not earlier, so a mob that was killed is still seen until the shot that killed it arrives.

#### Scenario: Kill timing
- **WHEN** the server removes a mob at server time T
- **THEN** each client keeps showing the mob until its display clock reaches T and then stops

#### Scenario: Late removal event
- **WHEN** a removal event arrives after the display clock has already passed its time
- **THEN** the client removes the mob immediately
