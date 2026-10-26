# Spec Delta

## ADDED Requirements

### Requirement: Every player sees the whole arena
The system SHALL give every player, the boss player included, a camera that shows the whole arena at all times: all of its width and all of its height, centred on the arena. The camera SHALL NOT follow any character. The view SHALL be sized from the window's aspect ratio so that the whole arena fits whatever the shape of the window, showing no more outside the arena than that shape forces.

#### Scenario: A survivor's view
- **WHEN** a survivor moves their character anywhere in the arena
- **THEN** the survivor's camera stays centred on the arena and every corner of the arena stays on the screen

#### Scenario: The boss player's view
- **WHEN** the boss player moves the boss
- **THEN** the boss player's camera shows the whole arena and every survivor in it

#### Scenario: A wide window
- **WHEN** the window is wider than the arena's shape
- **THEN** the whole height of the arena fills the height of the screen and the extra width shows space beside the arena

#### Scenario: A narrow window
- **WHEN** the window is narrower than the arena's shape
- **THEN** the whole width of the arena fills the width of the screen and the extra height shows space above and below the arena

## REMOVED Requirements

### Requirement: Camera follows the local character
**Reason**: Every player now sees the whole arena, so no camera follows a character.
**Migration**: The requirement that every player sees the whole arena replaces it.

### Requirement: Camera stays within the arena
**Reason**: A camera that shows the whole arena has no edge to stop at.
**Migration**: The requirement that every player sees the whole arena replaces it.

## MODIFIED Requirements

### Requirement: Cameras are independent
The system SHALL keep each player's camera separate, so one player's camera does not affect what others see.

#### Scenario: Two players on different positions
- **WHEN** two players' characters are in different parts of the arena
- **THEN** each player's screen shows the whole arena and neither player's camera moves because of the other
