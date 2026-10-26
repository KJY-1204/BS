# Proposal

## Why

Every player's camera follows their own character and shows about 12 by 21 units of a 32 by 20 arena, so a survivor cannot see a rescue point on the other side, an announced boss skill off to one side, a fallen teammate to rescue, or where the boss is coming from, and the boss player cannot see where the survivors are spread. The game now has several things that matter across the whole arena (rescue points, skill announcements, revival, summoning and device placement). It is decided that all players, the boss player included, play with the whole map on their screen.

## What Changes

- Every player's camera shows the whole arena at all times: the view is sized to fit the arena (all of its width and height) for the window's aspect ratio and is centred on the arena. The camera no longer follows a character.
- The zoom adapts to the window: a wide window is limited by the arena's height, a narrow one by its width, so the whole arena is always visible and nothing outside it more than the window's shape forces.
- Nothing else changes: no rule, value or message. World-space drawing and the overlays that project world positions onto the screen (health bars, circles, markers) follow the camera as before.

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
- `role-cameras`: the camera shows the whole arena for everyone instead of following the local character; the requirements about following and about staying within the arena are replaced.

## Impact

- `CameraFollow` (size to fit the arena, centred), `NetworkSceneSetup` (the starting size), tests of the sizing rule, README. No network, scene-data or wire-format change.
