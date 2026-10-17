# Proposal

## Why

BS는 생존자 4명 대 보스 1명의 비대칭 멀티플레이 뱀서라이크다. 넷코드는 나중에 덧붙이면 구조를 거의 다시 짜야 하므로, 전투/몹/보스 규칙을 얹기 전에 서버 권한형 연결 뼈대를 먼저 세운다. 지금 프로젝트는 SampleScene 하나만 있는 빈 상태이고 `com.unity.multiplayer.center`만 들어 있어, 넷코드 패키지와 연결 흐름이 전혀 없다.

## What Changes

- Netcode for GameObjects(NGO), Unity Transport, Unity Services(Authentication/Relay/Lobby) 패키지를 도입한다.
- 호스트(리슨 서버)가 Unity Relay로 방을 만들고 접속 코드를 얻는 흐름을 추가한다. 클라이언트는 코드로 접속한다.
- 한 방은 최대 5명(생존자 4 + 보스 1)이며, 접속 순서대로 역할 슬롯(생존자 1~4, 보스)을 서버가 배정한다.
- 임시 네모 캐릭터를 서버 권한으로 이동시키고 모든 클라이언트에 동기화한다.
- 생존자는 자기 캐릭터를, 보스는 보스 캐릭터를 따라가는 카메라를 각자 따로 가진다.
- Unity 에디터에서 호스트/클라이언트 버튼과 접속 코드 입력만 있는 최소 테스트 UI를 둔다.
- 범위 밖: 몹, 전투, 레벨업, 보스 스킬/소환, 부활, 승패 조건, Aseprite 에셋, 정식 로비 UI, Steam 연동.

## Capabilities

### New Capabilities
- `multiplayer-session`: Relay 기반 호스트 방 생성, 접속 코드로 참가, 5인 슬롯과 역할 배정, 접속/이탈 처리.
- `player-movement-sync`: 서버 권한형 플레이어 입력 처리와 이동 상태의 전체 클라이언트 동기화.
- `role-cameras`: 로컬 플레이어의 캐릭터를 따라가는 역할별 독립 카메라.

### Modified Capabilities
<!-- 기존 spec이 없으므로 없음 -->

## Impact

- 패키지: `Packages/manifest.json`에 NGO, Unity Transport, Authentication, Relay, Lobby 추가. Unity 6000.3, URP 17.3, Input System 1.20은 기존 것을 사용한다.
- 프로젝트: Unity Dashboard에서 Unity Services 프로젝트 연결(Relay 활성화)이 필요하다. 코드 밖의 외부 설정이다.
- 코드: `Assets/` 아래에 네트워크 부트스트랩, 플레이어 프리팹, 카메라, 테스트 UI 신규 추가. 기존 코드는 없어 충돌 없음.
- 이후 change(생존자 코어 루프, 보스 역할)가 이 뼈대 위에 쌓인다.
