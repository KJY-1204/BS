# Design

## Context

소환과 공격(`add-boss-summoning`, `add-boss-attacks`) 이후 몹은 한 종류이고, 이 change가 의존하는 현재 구조는 다음과 같다(코드로 확인).

- `MobSim`은 몹마다 `types[]`(byte)를 갖는다. 값 0은 자동, 1은 소환이고 `TypeAt`, 스폰 이벤트, 전체 상태(늦게 들어온 사람), 클라이언트 `MobInterpolator`, `MobView`의 색 표(`TypeColors`)가 이 byte 하나를 그대로 나른다. 스냅샷에는 종류가 없다(위치만). 출처 질의는 `types[i] == TypeSummoned`로 하고 `summonedCount`를 스폰/제거에서 유지한다.
- 속도(`Speed`), 반지름(`Radius`), 최대 체력(`MaxHealth`)은 `MobSim`의 단일 필드다. `Chase`는 `Speed * dt`, `Separate`는 `Radius * 2`, 경계 처리 `ClampToArena`는 `Radius`, 투사체 판정(`TryHitSegment`, `CollectSegmentHits`)은 `radius + Radius`, `AnyWithin`은 호출자가 준 거리, 체력 비율은 `MaxHealth`에 대해 계산한다. `CombatServer.Awake`가 개발 인자(`-bsMobHealth`, `-bsMobSpeed`)로 이 필드를 바꾼다.
- 생존자 접촉 피해는 `SurvivorSim.ApplyContactDamage`가 `reach = Radius + mobs.Radius`로 `AnyWithin`을 묻고, 닿으면 `ContactDamage`(10)를 한 번 준다(보호 시간이 횟수를 제한).
- `SummonSim`은 `SummonConfig`(비용 5, 무리 4, 쿨타임, 고리, 안전 거리)로 `Summon(ctx, fromBoss, out created)`를 처리하고 몹을 `TrySpawn(position, TypeSummoned)`로 만든다. 결과는 `SummonResult` 열거형이고 `PlayerSummon.ResultRpc`가 보스에게 알림 문구로 돌려준다. 보스 입력은 `PlayerSummon`이 읽어 `SummonRpc()`로 서버에 보낸다(인자 없음). `SummonHud`는 보스에게만 기운 막대, 키 안내, 명령 문구를 그린다.
- 숫자 키 1/2/3은 `PlayerGrowth.ReadInput`에서 레벨업 선택 키이고 보스에게는 선택지가 열리지 않는다(구현 때 보스에서 `PlayerGrowth`가 입력을 읽지 않는지 확인한다).

동기: proposal.md 참고.

## Goals / Non-Goals

**Goals:**
- 종류별 능력치와 소환 선택 규칙을 서버 순수 C#(`MobKinds`, `MobSim`, `SummonSim`)에 두어 EditMode로 시험한다.
- 몹 스냅샷과 이벤트의 와이어 형식과 크기를 바꾸지 않는다. 몹 위치 일치와 전투 이벤트 일치 시험이 그대로 유효해야 한다.
- 기본 종류 몹은 이전과 수치와 동작이 같다(기존 테스트가 고치지 않고 통과한다).

**Non-Goals:**
- 행동이 다른 몹(원거리, 폭발, 분열), 종류별 경험치, 종류 4개 이상, 종류별 명령 대상(명령은 전체 소환 몹).
- 스킬(광선, 원형 피해, 밀어냄)의 판정에 몹 크기를 쓰는 것. 이 판정은 지금처럼 몹 중심점 기준이다.
- 에셋. 색과 크기만 다른 임시 도형이다.

## Decisions

### 1. 종류 byte는 출처(비트 0)와 종류(비트 1~)를 함께 담는다
`MobKinds.Encode(origin, kind) = kind << 1 | origin`, `OriginOf`, `KindOf`. 종류 0 + 자동 = 0, 종류 0 + 소환 = 1이므로 기존 값과 같고, 기존 테스트의 `TrySpawn(position, 0|1, ...)` 호출은 기본 종류로 그대로 동작한다. 스폰 이벤트와 전체 상태는 이미 byte 하나를 싣고 있으므로 형식과 크기가 같다. 클라이언트는 `MobKinds.KindOf(type)`/`OriginOf(type)`으로 푼다. `MobSim`의 `TypeAuto`/`TypeSummoned`는 "종류 0의 출처 값"으로 남기고 출처 질의(`IsSummoned`, 개수, 명령 대상)는 비트 0 검사로 바꾼다.
- 대안: 종류용 배열을 `MobSim`에 따로 두고 새 이벤트 필드를 더한다. 스폰 이벤트 항목이 커지고 코덱, 보간기, 시험이 전부 바뀐다. byte 하나에 둘을 담는 편이 작고, 종류가 128개까지 가능하므로 충분하다.

### 2. 종류 데이터는 `MobKinds` 순수 C# 정적 표다 (기본 몹 값에 대한 배율)
`MobKindDef { Name, SpeedMultiple, HealthMultiple, RadiusMultiple, ContactDamageMultiple, CostMultiple, GroupMultiple, Color }`와 `MobKinds.All`/`Get(kind)`/`IsValid(kind)`/`Count = 3`.

| 종류 | 이름 | 속도 | 체력 | 크기 | 접촉 피해 | 비용 | 무리 |
|---|---|---|---|---|---|---|---|
| 0 | 기본 | 1.0 | 1.0 | 1.0 | 1.0 | 1.0 | 1.0 |
| 1 | 빠른 몹 | 1.6 | 0.5 | 0.8 | 0.8 | 0.8 | 1.25 |
| 2 | 단단한 몹 | 0.6 | 4.0 | 1.25 | 1.5 | 3.0 | 0.5 |

기본 몹 값은 이전 그대로(속도 2.5, 체력 30, 반지름 0.4, 접촉 피해 10, 비용 5, 무리 4)이므로 파생 값은 빠른 몹 속도 4.0, 체력 15, 반지름 0.32, 접촉 피해 8, 비용 4, 무리 5(기운 100으로 25마리), 단단한 몹 속도 1.5, 체력 120, 반지름 0.5, 접촉 피해 15, 비용 15, 무리 2다. 전부 가정값이다. 배율이라서 `-bsMobHealth`, `-bsMobSpeed`가 기본 값을 바꾸면 다른 종류도 따라간다. 반올림은 체력 `max(1, round)`, 무리 `max(1, round)`, 비용은 float이다. 기본 종류의 배율은 모두 1이라 계산이 이전과 비트까지 같다.
- 반지름 상한: 몹의 최대 반지름은 0.5로 둔다(두 몹의 반지름 합 1.0). 서로 밀어내는 공간 격자의 칸 크기가 1이고 이웃 칸(±1)만 보기 때문에, 반지름 합이 1.0을 넘으면 겹친 쌍을 놓칠 수 있다. 이 상한을 `MobKinds` 테스트로 지킨다.

### 3. `MobSim`은 몹마다 종류의 값을 읽고 단일 필드는 "기본 값"으로 남긴다
`Speed`, `Radius`, `MaxHealth`, 새 `ContactDamage`(기본 몹 값)는 그대로 필드이고, 종류별 값은 `SpeedOf(kind)`, `RadiusOf(kind)`, `MaxHealthOf(kind)`처럼 필드 × 배율로 계산한다. 몹마다 종류는 `types[i]`에서 푼다(배열 추가 없음). 바뀌는 곳:
- `TrySpawn`: 체력을 종류 최대 체력으로 시작, 위치 보정은 종류 반지름으로.
- `Chase`: 종류 속도. `Separate`/`PushApart`: `radius[i] + radius[j]`. `Step` 끝의 경계 보정: 몹별 반지름. `Push`: 밀어낸 뒤 경계 보정도 몹별.
- `ApplyDamage`: 체력 비율을 그 몹의 종류 최대 체력에 대해 계산.
- `TryHitSegment`/`CollectSegmentHits`: `reach = radius + RadiusOf(kind of m)`. 격자 범위 질의는 반지름 상한(0.5)으로 넓혀 놓친 몹이 없게 한다.
- 새 `MaxContactMultipleWithin(center, survivorRadius, out multiple)`(닿은 몹 중 가장 큰 접촉 피해 배율): 격자에서 `survivorRadius + radius[m]` 이내인 몹 중 접촉 피해가 가장 큰 값을 돌려준다. 기존 `AnyWithin`은 그대로 둔다(기존 테스트와 호환).
- `ApplyAreaDamage`, `ApplyLineDamage`, `Push`의 포함 판정은 중심점 기준 그대로(스킬 판정에 크기를 쓰지 않는 것은 non-goal).
- 대안: 몹마다 반지름, 속도, 체력 최대 배열을 둔다. 배열이 늘고 `Remove`의 옮기기와 `Clear`를 모두 고쳐야 하며 종류에서 파생되는 값이라 중복이다. 종류에서 계산하는 비용은 작다(표 3칸 조회).

### 4. 접촉 피해: 닿은 몹 중 가장 센 한 번
`SurvivorSim.ApplyContactDamage`가 `MaxContactMultipleWithin`을 부르고, 닿은 몹이 있으면 그 최댓값(`ContactDamage × 배율`, 정수 반올림)으로 `ApplyDamage`를 한다. 이미 보호 시간이 횟수를 한 번으로 제한하므로 "한 번에 가장 센 피해"가 단순하고 예측 가능하다. 기본 종류만 있을 때는 이전과 같은 10이다. `SurvivorSim.ContactDamage` 필드(기본 몹 값)는 유지해서 기존 테스트가 통과한다.
- 대안: 닿은 몹의 피해를 합한다. 여러 몹이 닿을 때 한 방에 죽는 위험이 커지고 지금의 "무리여도 한 번" 규칙이 깨진다.

### 5. 자동 스폰은 기본 종류만, 출처 질의는 비트 0
`AutoSpawn`은 `Encode(Auto, 0)`으로 스폰한다. `summonedCount`, `IsSummoned`, `ApplyOrderToSummoned`는 `(types[i] & 1) != 0`으로 판단해 종류와 무관하게 동작한다(명령은 모든 소환 몹).

### 6. 소환은 종류를 인자로 받고 비용과 무리 크기를 종류에서 가져온다
`SummonSim.Summon(ctx, fromBoss, kind, out created)`를 더하고 기존 `Summon(ctx, fromBoss, out created)`는 종류 0으로 넘긴다(기존 테스트 호환). 순서: 보스 아님 → `Ignored`, 판이 안 돌거나 보스 쓰러짐 → `Unavailable`, 모르는 종류 → 새 `UnknownKind`(비용 없음, 쿨타임 안 씀), 쿨타임, 가득 참, 비용(`Config.CostPerMob × CostMultiple`)으로 살 수 있는 수, 무리 크기(`round(Config.GroupSize × GroupMultiple)`). 위치 보정은 그 종류의 반지름을 쓴다. 몹은 `Encode(Summoned, kind)`로 스폰하고 서 있는 명령을 받는다. 종류 확인을 쿨타임보다 앞에 두는 이유는, 잘못된 요청이 쿨타임을 쓰지 않게 하고 거절 이유를 정확히 알리기 위해서다.
- `SummonResult.UnknownKind = 8`을 열거형 끝에 더한다(기존 값 불변). `PlayerSummon.ResultRpc`의 알림 문구에 "알 수 없는 종류"를 더한다.

### 7. 선택은 보스 소유자 쪽 상태이고 요청마다 서버로 간다
`PlayerSummon`이 `SelectedKind`(byte)를 갖고 숫자 키 1/2/3으로 바꾼다. 서버에는 저장하지 않고 `SummonRpc(byte kind)`로 매 요청에 실어 보내서 서버가 `UnknownKind`를 검증한다(보스만 보냄). 서버가 선택을 들고 있지 않으므로 동기화, 초기화, 연결 끊김 처리가 필요 없다. 판이 시작될 때 캐릭터가 새로 생기고 `PlayerSummon`도 새로 만들어져 선택이 기본 종류로 시작한다. 선택 변경은 UI에서 즉시 반영된다(로컬 상태).
- 대안: 서버가 선택을 보관하고 선택 RPC를 따로 둔다. RPC와 상태가 하나 더 늘고 어긋날 수 있다. 요청에 싣는 쪽이 작다.
- 키 충돌: 숫자 키 1/2/3은 생존자의 레벨업 선택 키다. 보스에게는 선택지가 열리지 않으므로 겹치지 않는다. 구현 때 보스 아바타에서 `PlayerGrowth`가 이 키를 읽지 않는지 확인한다.
- 개발 인자 `-bsAutoSummonKind <종류>`로 자동 소환이 보내는 종류를 정한다(기본 0). 자동 소환 시험 스크립트는 이 인자가 없으면 이전과 같다.

### 8. 화면은 종류 byte만으로 규칙대로 그린다
`MobView`는 `type`에서 종류와 출처를 풀어 색과 크기를 정한다. 기본 종류는 출처별 색(자동 주황, 소환 자홍)을 그대로 쓰고, 다른 종류는 종류 색(빠른 몹 청록, 단단한 몹 짙은 갈색)이며 크기는 `RadiusMultiple × 기본 지름`이다. 몹 풀의 스프라이트 크기는 `Update`에서 몹마다 설정한다. `SummonHud`는 기운 막대 위에 "1 기본(5)  2 빠른 몹(4)  3 단단한 몹(15)" 줄을 그리고 선택된 것을 강조한다. 비용은 종류 비용 × `SummonConfig.CostPerMob`에서 계산한다(HUD는 서버 설정을 모르므로 기본 설정값 표시이고 개발 인자로 비용을 바꿀 일은 없다).

### 9. 로그와 시험 지원
서버 몹 통계 줄에 종류별 개수(`kinds=a/b/c`)를 더하고, `[BS] summon:` 줄에 종류와 만든 수를 더한다. 소환 통계의 평균 거리는 그대로 소환 몹 전체 기준이다.

## Risks / Trade-offs

- [종류 값이 균형이 안 맞음(너무 세거나 약함)] → 모두 `MobKinds` 표의 배율과 소환 설정에 모여 있다. 균형은 플레이로 정하고 이번 change는 규칙과 동작을 갖추는 것이 목표다.
- [크기 차이로 공간 격자가 겹친 쌍을 놓침] → 반지름 상한 0.5와 `MobKinds` 테스트, 격자 질의 범위를 상한으로 넓힌다. 겹침 시험에 큰 몹끼리를 포함한다.
- [기본 몹 동작이 달라짐] → 기본 종류의 배율이 모두 1이어서 계산이 같다. 기존 EditMode 테스트가 고치지 않고 통과하는 것과 이전 통합 로그 비교(몹 위치 차이 0.000, 전투 이벤트 일치)로 확인한다.
- [서버 틱 시간 증가] → 몹마다 표 조회가 늘 뿐이다. Relay 측정에서 서버 틱 시간을 이전과 비교한다.
- [숫자 키를 누르다 의도치 않게 선택이 바뀜] → 보스에게만 의미가 있고 바뀐 선택은 화면에 바로 표시된다.
- [출처와 종류를 byte 하나에 담아 나중에 종류가 많아지면 모자람] → 종류 127개까지 되므로 충분하다. 더 늘 때는 이벤트 형식을 바꾸는 별도 change에서 다룬다.

## Migration Plan

- 저장 데이터나 새 메시지가 없다. 소환 RPC에 인자 하나(종류)가 더해지고 같은 빌드끼리만 접속한다. 씬과 프리팹 변경은 없다(새 컴포넌트가 없다). 그래도 개발 빌드는 다시 만든다.
- 되돌리기: `MobKinds` 표를 기본 종류 하나로 두면 이전 동작이 된다. 종류를 고르는 코드는 요청마다 종류 0을 보내는 것과 같다.

## Open Questions

- 종류마다 처치 경험치가 다를지는 다음에 정한다(지금은 모두 같고 규칙에 영향이 없다).
- 행동이 다른 몹을 넣을 때 종류 표에 행동 필드를 더할지는 그 change에서 정한다.
