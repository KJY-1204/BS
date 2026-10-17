# survivor-revival Specification

## Purpose

Defines how a fallen survivor is brought back: a living teammate stands still next to them while a gauge fills, moving resets the gauge, and a full gauge revives the survivor.

## Requirements

### Requirement: A living survivor can revive a dead one
A living survivor who stays within the revive range of a dead survivor without moving SHALL fill that dead survivor's revive gauge at a steady rate, and the gauge SHALL be full after the revive time.

#### Scenario: Standing next to a teammate
- **WHEN** a living survivor stands still within range of a dead survivor for the whole revive time
- **THEN** the gauge fills completely

### Requirement: Moving or leaving resets the gauge
The revive gauge SHALL return to zero when no living survivor is both within range and standing still.

#### Scenario: Rescuer moves
- **WHEN** the rescuer moves while the gauge is partly full
- **THEN** the gauge returns to zero and must be filled again from the start

#### Scenario: Rescuer walks away
- **WHEN** the rescuer leaves the revive range
- **THEN** the gauge returns to zero

#### Scenario: Rescuer dies
- **WHEN** the rescuer dies or disconnects while the gauge is partly full
- **THEN** the gauge returns to zero

### Requirement: A full gauge revives the survivor
When the gauge becomes full, the dead survivor SHALL come back alive with a fixed fraction of maximum health and a short protection time, at the place where they fell.

#### Scenario: Revival
- **WHEN** the gauge becomes full
- **THEN** the survivor is alive with the configured fraction of maximum health, cannot be damaged for the protection time, and can move and fire again

#### Scenario: Mobs return to the revived survivor
- **WHEN** a survivor is revived
- **THEN** mobs may choose that survivor as a target again

### Requirement: Revive progress is visible to everyone
Every player SHALL see a dead survivor's revive gauge, and SHALL see that the survivor is down.

#### Scenario: Gauge display
- **WHEN** the gauge is partly full
- **THEN** every client shows the gauge at that fill level over the fallen survivor

### Requirement: Only living survivors revive
The boss SHALL NOT revive survivors, and a dead survivor SHALL NOT revive others. Attacking while reviving SHALL NOT reset the gauge.

#### Scenario: Boss next to a fallen survivor
- **WHEN** the boss stands still next to a dead survivor
- **THEN** the gauge does not fill

#### Scenario: Firing while reviving
- **WHEN** the rescuer fires without moving
- **THEN** the gauge keeps filling

### Requirement: Revival needs a living survivor
If no survivor is alive, nobody SHALL be revived and the game SHALL NOT end because of it in this version.

#### Scenario: Everyone down
- **WHEN** every survivor is dead
- **THEN** the gauges stay at zero and the room continues to run
