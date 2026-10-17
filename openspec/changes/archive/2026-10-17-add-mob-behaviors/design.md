# Design

## Context

몹 종류(`add-mob-kinds`)와 보스 공격(`add-boss-attacks`) 이후 이 change가 의존하는 현재 구조는 다음과 같다(코드로 확인).

- `MobKinds`는 종류 표(`MobKindDef`: 이름, 속도/체력/크기/접촉 피해/비용/무리 배율, 색)이고 종류 번호는 스폰 이벤트의 type byte(비트 0 출처, 위 비트 종류)로 모든 클라이언트에 간다. 스냅샷은 위치만 싣고 종류는 싣지 않는다. 최대 반지름은 0.5(두 반지름의 합 1.0 이하)이고, 공간 격자 칸이 1이라 이웃 칸만 보는 밀어내기가 이를 전제한다.
- `MobSim`은 순수 C#이고 `Step`이 자동 스폰, 추적 이동(`Chase`: 몹별 속도, 목표 덮어쓰기, 도착 해제), 밀어내기, 경계 보정을 한다. `Freeze` 중에는 `Step`이 바로 돌아간다. 제거는 마지막 몹을 빈 칸으로 옮기는 방식이라 몹별 배열(`ids`, `types`, `health`, `positions`, `override*`)을 `Remove`와 `Clear`가 같이 옮긴다. 몹의 스폰과 제거는 이벤트로 쌓이고 `MobServer.FlushEvents`가 보낸다.
- 서버 틱은 `MobServer.FixedUpdate`(순서 -100: `Sim.Step`, 이벤트 보냄, 스냅샷) 다음에 `CombatServer.FixedUpdate`(순서 -50: `Survivors.Step`, `Attacks.Step`, `Projectiles.Step`, 이벤트 발행)가 돈다. 둘 다 `RoleTable.MatchRunning`이 아니면 돌지 않는다.
- 보스의 투사체는 `ProjectileSpec.Hostile`로 나가고 `ProjectileSim.Step(dt, mobs, boss, survivors)`가 `ISurvivorTargets`(어댑터 `SurvivorTargets`)로 살아 있고 보호 시간이 아닌 가장 가까운 생존자만 맞힌다. 발사 이벤트의 발사자 칸은 `slotOf(owner)`로 정해지고(보스 0, 생존자 1~4) 클라이언트는 `ProjectileReplay.ShapeOfSlot`(`CombatClient.ShapeOfShooterSlot`)로 모양을 정하고 `CombatView`가 칸으로 색을 정한다.
- 전투 이벤트(`CombatCodec`)는 Fire, Hit, End, Skill 네 종류이고 `TryReadEvent`가 알려진 종류만 받는다. `CombatClient.Apply`가 종류별로 리플레이에 넘기고 `CombatView`는 `AddRing`으로 임시 고리 효과를 그린다.
- `SurvivorSim.ApplyDamage`가 체력, 보호 시간, 쓰러짐을 한곳에서 처리하고 접촉 피해(`MaxContactMultipleWithin`)와 보스 투사체(`SurvivorTargets`)가 이미 이 경로를 쓴다.
- 소환은 `SummonSim.Summon(ctx, fromBoss, kind, out created)`가 종류 표의 비용 배율과 무리 배율을 읽고, 선택은 `PlayerSummon`의 숫자 키 1~3과 HUD 3칸이다.

동기: proposal.md 참고.

## Goals / Non-Goals

**Goals:**
- 종류에 행동을 더하는 구조(행동 데이터, 행동 상태, 행동의 결과를 서버가 처리)를 순수 C#으로 만들어 EditMode로 시험한다.
- 몹 스냅샷과 몹 이벤트의 와이어 형식은 그대로 두고 전투 이벤트에 폭발 하나만 더한다.
- 기존 세 종류, 자동 스폰, 보스 공격, 소환 규칙은 이전과 같이 동작한다.

**Non-Goals:**
- 다른 행동(분열, 치유, 방어), 종류별 경험치, 몹의 탄을 생존자가 막는 것, 폭발 신호의 클라이언트 표시.
- 행동에 따른 새 네트워크 메시지나 스냅샷 필드. 클라이언트는 폭발 몹이 멈춘 것과 터지는 순간의 효과만 본다.

## Decisions

### 1. 행동은 종류 정의의 필드이고 값은 한곳에 모은다
`MobKindDef`에 `Behavior`(추적, 원거리, 폭발)와 행동 값을 더한다: `HoldDistance`, `ShootRange`, `ShotCooldownSeconds`, `ShotDamage`(원거리), `TriggerDistance`, `FuseSeconds`, `BlastRadius`, `BlastDamage`(폭발). 기존 세 종류는 `Behavior = 추적`이고 다른 값은 쓰이지 않는다. 몹 탄 모양(속도 8, 수명 1.6초, 반지름 0.2)은 `MobShot.Shape`(`BossAttack.Shape`와 같은 위치와 방식의 순수 C# 정적 값)에 둔다. 값은 모두 가정값이다.

| 종류 | 이름 | 속도 | 체력 | 크기 | 접촉 피해 | 비용 | 무리 | 행동 값 |
|---|---|---|---|---|---|---|---|---|
| 3 | 원거리 몹 | x1.0 | x0.8 | x1.0 | x0.6 | x2.0 (10) | x0.5 (2마리) | 멈춤 거리 6, 사거리 9, 쿨타임 2.5초, 탄 피해 8 |
| 4 | 폭발 몹 | x1.4 | x0.6 | x1.0 | x0.5 | x1.6 (8) | x0.75 (3마리) | 기폭 거리 1.6, 신호 0.7초, 폭발 반지름 2.2, 폭발 피해 25 |

- 반지름 상한: 새 종류의 크기 배율은 1.0(반지름 0.4)로 상한 0.5 안에 있다.
- 대안: 행동을 클래스나 인터페이스로 만든다. 종류 5개와 행동 3개에는 과하고, 몹이 배열 기반이라 몹별 객체를 만들면 구조가 어긋난다. 열거형 분기가 작고 시험하기 쉽다.

### 2. 몹의 행동 상태는 몹별 배열 둘이고 `Step`이 "행동 목록"을 만든다
`MobSim`에 `float[] timer`(원거리는 다음 발사까지, 폭발은 신호가 끝날 때까지)와 `bool[] fuseLit`(폭발 몹의 신호 중)를 더하고 `TrySpawn`/`Remove`(마지막 몹 옮기기)/`Clear`가 같이 다룬다. 원거리 몹의 첫 발까지 시간은 쿨타임의 40~100%를 `rng`로 정한다(몹마다 다름, 시드가 같으면 같음). `Step`의 순서는 자동 스폰 -> 추적 이동(행동에 따라 멈춤) -> 행동 갱신(쿨타임, 신호) -> 밀어내기 -> 경계 보정이다. 행동 갱신은 두 가지 결과를 목록에 쌓는다.
- `MobShot { MobId, Origin, Angle(ushort, 가장 가까운 생존자 방향) }`
- `MobBlast { MobId, Position, Radius, Damage }` — 폭발한 몹은 `Remove`되어 제거 이벤트가 나간다(경험치와 상관없는 제거).
`DrainActions(List<MobShot>, List<MobBlast>)`로 서버가 가져간다. `MobSim`은 탄이나 피해를 직접 만들지 않는다(`ProjectileSim`/`SurvivorSim`을 모른다). 쏘려 했는데 탄 상한에 걸리면 서버가 `RefundShot(mobId)`로 그 몹의 쿨타임을 0으로 돌린다(상한 때문에 거절된 발사는 쿨타임을 쓰지 않는다는 규칙).
- 정지: `Freeze` 중 `Step`은 일찍 돌아가므로 이동, 쿨타임, 신호가 모두 멈추고 풀리면 이어진다. 판이 끝나면 `MobServer`가 `Step`을 부르지 않는다. `Clear`가 행동 목록과 배열 상태를 지운다.
- 대안: `MobSim`이 `ProjectileSim`과 `SurvivorSim`을 받아 직접 쏘고 피해를 준다. 순수 시뮬레이션끼리 엉키고 `MobServer`와 `CombatServer`의 틱 순서(몹 -100, 전투 -50)가 이미 분리돼 있어 결과 목록 전달이 더 자연스럽다.

### 3. 원거리 몹의 이동과 사격
목표는 지금처럼 `TryGetTargetPosition`(명령이 있으면 명령 대상, 없으면 가장 가까운 생존자)이고, 그 목표와의 거리가 `HoldDistance` 이하면 이동하지 않는다. 사격은 이동과 별개로 "가장 가까운 살아 있는 생존자가 `ShootRange` 안이고 쿨타임이 지났을 때" 그 생존자를 향해 쏜다(명령으로 지점으로 가는 중에도 쏜다). 생존자가 없으면 쏘지 않는다. 탄 방향은 발사 순간의 위치 기준이다(예측 사격 없음).
- 쏜 순간 서버는 `ProjectileSpec { Owner = MobShooterOwner, Origin = 몹 위치, Angle, Damage = 종류의 탄 피해, Shape = MobShot.Shape, Hostile = true }`로 `ProjectileSim.TryFire`한다. 몹의 탄은 이미 있는 적대 투사체 규칙(생존자만, 보호 시간과 쓰러진 생존자 통과, 상한 공유)을 그대로 쓰므로 새 명중 코드는 없다.

### 4. 폭발 몹의 신호와 폭발
추적 이동 뒤, 살아 있는 생존자 중 하나라도 `TriggerDistance`(몹 중심과 생존자 중심) 안이면 `fuseLit = true`, `timer = FuseSeconds`가 되고 이후 이동하지 않는다. 신호가 끝나면(`timer <= 0`) `MobBlast`를 만들고 그 몹을 제거한다. 신호 중에는 생존자가 멀어져도 신호는 계속된다(피할 수 있는 것이 핵심이다). 서버는 폭발 반지름 안(생존자 중심과 몹 중심의 거리 <= 반지름)의 살아 있는 생존자에게 `SurvivorSim.ApplyDamage`로 피해를 준다. 보호 시간 중이거나 쓰러진 생존자는 `ApplyDamage`가 거절하므로 규칙이 한곳에서 정해진다. 보스와 다른 몹은 다치지 않는다. 신호 중 몹이 투사체로 죽으면 `Remove`되므로 폭발이 없다.
- 폭발 몹이 소환 명령(생존자 지정 등)을 받아도 신호는 "어느 생존자든 기폭 거리 안"으로 시작한다.

### 5. 전투 이벤트 "폭발"(종류 5, 6바이트)
`CombatEventKind.Explosion = 5`, 형식은 `kind u8, x i16, y i16, radius u8`(반지름은 1/10 단위, 최대 25.5). 위치는 다른 이벤트처럼 1/1000 단위로 양자화한다. 이유: 기존 이벤트에는 표적 없는 위치+크기 정보를 싣는 칸이 없고(`Skill`은 스킬 번호로 클라이언트의 스킬 표를 조회하므로 몹 폭발을 억지로 넣으면 표가 어긋난다), 이벤트 종류를 하나 더하는 쪽이 코덱 한 곳(`EventSize`, 쓰기, 읽기, 종류 검사)만 고친다. 같은 메시지와 같은 배치 크기 제한(1000바이트)을 쓰고 새 메시지 종류는 없다. 서버 덤프에는 `Explosion id=<반지름*10>` 줄이 남고 `tools/compare_combat.py`가 이를 인식하도록 정규식을 넓힌다.
- 시점: 폭발 이벤트는 몹의 제거 이벤트와 같은 서버 틱에 같은 시각으로 나간다(`CombatServer`가 행동 목록을 처리하고 발행한 뒤 `MobServer.FlushEvents`를 부른다). 클라이언트는 이미 제거를 서버 시각에 맞춰 보여 주므로 효과와 몹의 사라짐이 같은 순간이다.

### 6. 클라이언트 표시: 몹 사수 칸 5와 폭발 효과
몹의 탄은 `slotOf(MobShooterOwner) = 5`인 발사 이벤트로 가고(`MobShooterOwner`는 보스 자리 표시값 `ulong.MaxValue`와 다른 상수), 클라이언트의 `ShapeOfShooterSlot`이 칸 5이면 `MobShot.Shape`를, `CombatView`가 칸 5이면 원거리 몹의 종류 색을 쓴다. 폭발은 `ExplosionReplay`(순수 C#, `SkillEffectReplay`와 같은 모양: 서버 시각과 함께 쌓고 표시 시계가 시각에 닿으면 꺼냄)로 시점을 맞추고 `CombatView`가 `AddRing(위치, 반지름, 폭발 색, 짧은 시간)`으로 그린다. 신호 표시(터지기 전 깜빡임)는 non-goal이다. 폭발 몹이 멈춰 선 것으로 보인다.
- 대안: 스냅샷의 예약된 flags 바이트로 신호 상태를 알린다. 형식은 그대로지만 `IMobSource`, 보간기, 코덱 시험이 모두 바뀐다. 이번에는 하지 않는다.

### 7. 소환 선택은 5종류, 종류 표만 늘면 나머지는 따라온다
`SummonSim`은 종류 표의 비용/무리 배율을 이미 읽으므로 코드 변경이 없다(기운 100이면 원거리 몹은 한 번에 2마리에 20, 폭발 몹은 3마리에 24). `PlayerSummon`에 키 4, 5를 더하고 `SummonHud`의 종류 줄을 5칸(칸 너비 100)으로 늘린다. 소환 요청의 종류 byte가 3, 4를 보내는 개발 인자 `-bsAutoSummonKind`는 이미 임의의 번호를 받는다.

### 8. 정리와 초기화는 기존 스위치를 따른다
`CombatServer.ResetAll`이 `Projectiles.Clear()`로 몹의 탄도 지우고 `MobServer.ResetMatch`가 `Sim.Clear()`로 행동 상태를 지운다. 클라이언트는 `Waiting` 전환에서 이미 리플레이를 비우므로 `ExplosionReplay`도 같이 비운다(`CombatClient.OnPhaseChanged`, 서버 종료, 클라이언트 종료 경로).

## Risks / Trade-offs

- [원거리 몹이 너무 아프거나 폭발이 한 번에 너무 많은 피해를 줌] → 값은 모두 종류 표와 `MobShot` 한곳에 있다. 폭발 몹 3마리가 한꺼번에 터지면 각각 25이지만 생존자의 보호 시간(0.6초)이 같은 순간의 중복 피해를 한 번으로 줄인다. 균형은 플레이로 정한다.
- [몹의 탄이 상한 256을 채움] → 원거리 몹은 최대 몇십 마리이고 쿨타임 2.5초, 수명 1.6초라 동시에 많아야 수십 발이다. 상한에 걸리면 쿨타임을 쓰지 않고 다음 틱에 다시 시도한다.
- [몹 위치 일치가 깨짐] → 행동은 `MobSim`의 결정적 규칙이고 위치는 같은 경로(스냅샷)로 간다. 폭발 몹이 멈춘 것도 서버 위치에 그대로 반영된다. 다중 프로세스 시험으로 위치 차이를 확인한다.
- [전투 트래픽 증가] → 몹의 발사 이벤트 몇 개와 폭발 6바이트가 늘 뿐이다(원거리 몹 20마리면 초당 약 8발, 이벤트당 10~17바이트). Relay로 10 KB/s 기준을 다시 잰다.
- [탄 방향이 발사 순간 위치라 가만히 서 있는 생존자에게 항상 맞음] → 의도다. 움직이면 피할 수 있고 균형은 사거리, 탄 속도, 쿨타임으로 조절한다.
- [새 이벤트 종류를 구버전이 모름] → 같은 빌드끼리만 접속한다는 전제다(빌드 버전 검사는 없음).

## Migration Plan

- 저장 데이터나 새 메시지가 없다. 전투 이벤트에 종류 하나가 늘고 같은 빌드끼리만 접속한다. 씬과 프리팹 변경은 없다(새 컴포넌트가 없다). 개발 빌드는 다시 만든다.
- 되돌리기: 종류 표에서 종류 3, 4를 빼면 이전 동작이다. 행동 갱신은 `Behavior`가 추적이 아닌 몹에만 작동한다.

## Open Questions

- 폭발 신호의 화면 표시(깜빡임)와 몹의 탄을 막는 수단은 다음에 정한다. 지금 규칙에는 영향이 없다.
