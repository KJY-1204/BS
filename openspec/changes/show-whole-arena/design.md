# Design

## Context

`CameraFollow` is an orthographic camera with a fixed size of 6 (the half-height of the view) that follows the local player's object and clamps to the arena. The arena is `ArenaConfig.Width` by `ArenaConfig.Height` (32 by 20). The scene setup gives the main camera the component and the same starting size. The HUDs project world positions with `Camera.WorldToScreenPoint`, so they follow whatever the camera shows.

## Goals / Non-Goals

**Goals:** the whole arena on every screen, for every window shape, with no per-frame cost worth measuring.
**Non-Goals:** a zoom or a minimap, a camera mode switch, any change to the characters' size or to the arena.

## Decisions

1. **A pure sizing rule:** `CameraFollow.SizeToFitArena(float aspect)` returns `max(Height / 2, Width / 2 / aspect)` (the half-height that fits both the height and the width of the arena for that aspect ratio). It is static so a test can check it without a scene.
2. **The camera:** `LateUpdate` sets `orthographicSize` from the current aspect (the window can be resized) and puts the camera at the arena's centre (0, 0); it no longer reads the local player. The class keeps its name to avoid a scene or prefab change; its summary says what it does now.
3. **The starting size** in `NetworkSceneSetup` and the serialized field stay, as only a fallback before the first frame.
4. **Drawing:** the characters, mobs and projectiles become smaller on the screen (the view is about 1.7 times larger than before at 16:9). That is the point of the change and is accepted; nothing is rescaled.

## Risks / Trade-offs

- Small things (shots, mobs) are harder to read. If that is a problem later the answer is the art and sizes, not this rule.
- The IMGUI overlays are placed in screen space and do not depend on the zoom except where they project a world position, which follows the camera.
