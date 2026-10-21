# Spec Delta

## MODIFIED Requirements

### Requirement: A match with a boss has a time limit
When a match starts with a boss player, the server SHALL start a countdown from a configured time limit and SHALL tell every client the remaining time. The default time limit SHALL be fifteen minutes, and a development setting MAY replace it. The countdown SHALL run only while the match is playing. A match without a boss player SHALL have no countdown.

#### Scenario: Countdown
- **WHEN** a match with a boss runs for 30 seconds
- **THEN** every client shows 30 seconds less than the limit

#### Scenario: Practice match
- **WHEN** a match starts with no boss player
- **THEN** no countdown is shown and the match runs until the host ends it

#### Scenario: A fresh countdown
- **WHEN** a second match starts after the first ended
- **THEN** the countdown starts again from the full limit

#### Scenario: Default limit
- **WHEN** a match with a boss starts with no development setting
- **THEN** the countdown starts at fifteen minutes

#### Scenario: Replaced limit
- **WHEN** a match starts with the development setting for the time limit
- **THEN** the countdown starts from that value instead
