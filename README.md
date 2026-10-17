# BS

Unity 6 (6000.3) 2D 뱀서라이크. 생존자 4명 대 보스 1명의 비대칭 멀티플레이.

이 프로젝트는 동방 Project의 팬 2차창작을 목표로 하는 비공식 개인 프로젝트다. 원작 게임의 소재(이미지, 음악 등)는 사용하지 않는다.
현재 구현 범위는 네트워크 연결 뼈대(change `add-network-skeleton`)까지다. 몹, 전투, 레벨업, 보스 스킬은 아직 없다.

## 구조

- 서버 권한형 리슨 서버(호스트-클라이언트), Netcode for GameObjects
- Relay 방: Unity Multiplayer Services (접속 코드로 참가). 직접 IP 모드는 로컬 테스트용
- 한 방 최대 5명: 보스 1 + 생존자 슬롯 1~4. 역할은 서버의 `RoleTable`이 배정
- 이동: 소유자가 입력을 서버로 보내고, 서버가 위치를 계산해 복제. 소유자는 같은 규칙으로 먼저 움직이고 서버와 어긋나면 보정
- 카메라: 각 클라이언트가 자기 캐릭터만 따라가며 아레나(32 x 20 유닛) 밖은 보이지 않음

```
Assets/Scripts/
  Network/  SessionBootstrap, RoleTable, ArenaConfig, ArenaVisual, DevArgs
  Player/   PlayerAvatar
  Camera/   CameraFollow
  UI/       NetworkMenu (IMGUI 테스트 메뉴)
  Editor/   NetworkSceneSetup (BS > Setup Network Scene)
```

## 실행 방법

1. Unity 6000.3으로 프로젝트를 열고 `Assets/Scenes/SampleScene`을 연다.
2. 씬이 비어 있으면 메뉴 `BS > Setup Network Scene`을 실행한다 (여러 번 실행해도 안전).
3. 플레이 모드에서 왼쪽 위 메뉴를 쓴다.
   - **Host**: Relay 방을 만들고 접속 코드를 표시. "보스로 시작"을 켜면 호스트가 보스
   - **Join**: 접속 코드를 입력해 참가
   - **Host (직접) / Join (직접)**: Unity 서비스 없이 같은 PC/LAN에서 테스트
4. 이동은 WASD 또는 방향키.

Relay를 쓰려면 Unity 계정으로 로그인하고 프로젝트가 Unity 클라우드 프로젝트에 연결돼 있어야 한다.

## 여러 명으로 테스트하기

개발 빌드(Development Build)와 에디터에서만 다음 실행 인자가 동작한다.

| 인자 | 동작 |
|---|---|
| `-bsHost [boss]` | 직접 IP 호스트로 시작 (`boss`를 주면 보스) |
| `-bsJoin <ip>` | 직접 IP로 참가 |
| `-bsJoinCode <코드>` | Relay 접속 코드로 참가 |
| `-bsMove <x> <y>` | 고정 이동 입력 |
| `-bsLog` | 1초마다 캐릭터/카메라 위치 로그 |
| `-bsDisplaceAt <초>` | 소유자 위치를 5유닛 밀어 서버 보정을 확인 |
| `-bsQuit <초>` | 지정 시간 후 종료 |

예: 에디터에서 Host (직접)을 누른 뒤
`BS.exe -batchmode -nographics -logFile client.log -bsJoin 127.0.0.1 -bsMove 1 0 -bsLog -bsQuit 60`

에디터에서 `F9`는 소유자 위치를 강제로 밀어 보정 동작을 확인하는 디버그 키다.

## 알려진 한계

- 호스트가 나가면 방이 끝난다 (호스트 이전 없음). 클라이언트는 접속 화면으로 돌아간다.
- 호스트 플레이어는 지연이 없어 약간 유리하다. 보스 밸런스 단계에서 다시 평가한다.
- 소유자 보정 임계값(`PlayerAvatar.correctionThreshold`, 기본 2.5유닛)은 이동 속도 6유닛/초에서 약 400ms 지연까지 평소엔 보정이 일어나지 않도록 잡은 값이다. Relay로 측정했을 때 소유자와 서버의 차이는 최대 약 1유닛이었다. 지연이 더 큰 환경에서는 올려야 할 수 있다.
- 완전한 롤백 예측은 없다. 조작된 위치 주장은 서버 값으로 되돌려진다.
- 테스트 메뉴는 IMGUI이고 정식 로비 UI와 Steam 연동은 범위 밖이다.
- 몹 동기화는 아직 설계하지 않았다. 몹 수백 마리는 `NetworkObject`를 개별로 쓰면 부담이 크므로, 코어 루프 change에서 로직과 렌더링을 분리해 설계한다.
