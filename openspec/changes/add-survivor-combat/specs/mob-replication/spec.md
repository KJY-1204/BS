# Spec Delta

## ADDED Requirements

### Requirement: A removed mob disappears at its display time
A client SHALL stop displaying a removed mob at the moment on its display clock that matches when the server removed it, not earlier, so a mob that was killed is still seen until the shot that killed it arrives.

#### Scenario: Kill timing
- **WHEN** the server removes a mob at server time T
- **THEN** each client keeps showing the mob until its display clock reaches T and then stops

#### Scenario: Late removal event
- **WHEN** a removal event arrives after the display clock has already passed its time
- **THEN** the client removes the mob immediately
