# skill-effect-scale Specification

## Purpose

Defines how the size of a skill's effect (its range and its duration) is decided once by the server when the skill fires, carried with the skill event, and used by both the server's rules and every client's drawing so what is drawn matches what is hit.

## Requirements

### Requirement: The server decides a range scale and a duration scale for every skill use
When a survivor's skill fires, the server SHALL decide two whole-number percentages for that use: a range scale that applies to the skill's radius, length, width and push distance, and a duration scale that applies to how long a timed effect lasts (protection, attack speed boost, time stop). Without a passive that changes them, both SHALL be 100, which leaves the skill exactly as it is defined. The scales SHALL come from the passives of the survivor using the skill, SHALL NOT go below 100, and SHALL NOT go above a fixed upper limit that fits in one byte.

#### Scenario: No passive
- **WHEN** a survivor with no range or duration passive uses any skill
- **THEN** both scales are 100 and every size and time is the skill's defined value

#### Scenario: A range passive
- **WHEN** a survivor who has a range passive uses a skill that has a radius
- **THEN** the range scale is above 100 by the passive's amount for its rank and the radius used is the defined radius multiplied by it

#### Scenario: A duration passive
- **WHEN** a survivor who has a duration passive uses a skill with a duration
- **THEN** the duration scale is above 100 by the passive's amount for its rank and the duration used is the defined duration multiplied by it

#### Scenario: Upper limit
- **WHEN** the passives would take a scale above the upper limit
- **THEN** the scale is the upper limit

### Requirement: The server uses the same scale it sends
The server SHALL apply the whole-number scale it puts in the skill event to its own rules, so the size it judges with is the size it tells the clients. A skill without a range or duration SHALL be unaffected by the matching scale.

#### Scenario: Judging with the sent scale
- **WHEN** the server fires a skill with a range scale of 130
- **THEN** the mobs hit, pushed or healed are those within 1.3 times the defined range, and the event carries 130

#### Scenario: A skill with no duration
- **WHEN** a skill that has no duration is used with a duration scale above 100
- **THEN** nothing about the skill changes

### Requirement: Clients draw the effect at the size the server used
Every client SHALL draw a skill's ring, beam and aura using the defined values multiplied by the scales carried with the skill event, so the picture matches the range and time the server judged with. Showing the effect SHALL NOT need the owner's passive ranks, which are not sent to other players.

#### Scenario: A larger ring
- **WHEN** a survivor uses an area skill with a range scale of 130
- **THEN** every client draws the ring 1.3 times the defined radius

#### Scenario: A longer aura
- **WHEN** a survivor uses a protection skill with a duration scale of 140
- **THEN** every client shows the protection mark for 1.4 times the defined time

#### Scenario: Another player's passives stay private
- **WHEN** a survivor's skill effect is drawn on another player's screen
- **THEN** that client only receives the scales in the event and not the survivor's skill ranks

### Requirement: Scales do not change other skills or boss skills
The scales SHALL apply only to the use they were decided for. Boss skill warnings SHALL carry scales of 100.

#### Scenario: Next use
- **WHEN** a survivor uses a skill and then another skill
- **THEN** each use carries the scales decided for it

#### Scenario: Boss warning
- **WHEN** the boss announces a skill
- **THEN** its event carries range and duration scales of 100
