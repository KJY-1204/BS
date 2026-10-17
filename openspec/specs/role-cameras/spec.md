# role-cameras Specification

## Purpose

Defines the camera each player sees: every player's view follows only their own character, independent of other players' views.

## Requirements

### Requirement: Camera follows the local character
The system SHALL give each player a camera that follows that player's own character.

#### Scenario: Survivor view
- **WHEN** a survivor moves their character
- **THEN** that survivor's camera follows the survivor's character

#### Scenario: Boss view
- **WHEN** the boss player moves the boss character
- **THEN** the boss player's camera follows the boss character, not any survivor

### Requirement: Cameras are independent
The system SHALL keep each player's camera separate, so one player's camera does not affect what others see.

#### Scenario: Two players on different positions
- **WHEN** two players' characters are in different parts of the arena
- **THEN** each player's screen is centered on their own character

### Requirement: Camera stays within the arena
The system SHALL limit the camera so its view does not show space beyond the arena bounds where the arena is larger than the view.

#### Scenario: Character near the edge
- **WHEN** a character is at the arena edge
- **THEN** the camera stops at the boundary so the area outside the arena is not shown
