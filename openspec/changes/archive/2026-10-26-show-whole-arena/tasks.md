# Tasks

## 1. The camera (role-cameras)

- [x] 1.1 `CameraFollow.SizeToFitArena(aspect)` and the camera that sets its size from the aspect each frame and sits at the arena's centre without following a character
- [x] 1.2 Keep `NetworkSceneSetup` and the serialized size as the fallback; update the class summary

## 2. Tests and documentation (role-cameras)

- [x] 2.1 Tests of the sizing rule: 16:9 is limited by the height, a square window by the width, an ultra-wide window by the height, and for every aspect the whole arena fits
- [x] 2.2 A windowed capture (a survivor and the boss player) shows the whole arena and the overlays in place
- [x] 2.3 README (the camera section and the test count), the whole EditMode suite passes
