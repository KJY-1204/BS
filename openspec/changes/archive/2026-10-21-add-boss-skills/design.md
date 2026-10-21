# Design

## Context

보스 공격(`add-boss-attacks`)과 몹 행동(`add-mob-behaviors`) 이후 이 change가 의존하는 현재 구조는 다음과 같다(코드로 확인).

- `BossAttack`은 보스 투사체 값(모양, 기본 피해 15, 쿨타임 0.8)과 체력 비례 강화 함수(`CooldownMultiplier(fraction)`: 1 -> 0.5, `Damage(base, fraction)`: 기본 -> 최대 1.5배)를 가진 순수 C# 정적 클래스다. 보스는 `AttackSim`의 적대 사수(`Register(owner, def, hostile: true)`)로 쏘고, `CombatServer.FixedUpdate`가 매 틱 보스 체력으로 `SetModifiers`를 부른다.
- 적대 투사체는 `ProjectileSpec.Hostile`로 `ProjectileSim.TryFire`에 들어가고 `SurvivorTargets`(`ISurvivorTargets`)가 살아 있고 보호 시간이 아닌 가장 가까운 생존자만 맞힌다. 발사 이벤트의 발사자 칸은 `slotOf(owner)`로 정해지고(보스 0) 클라이언트가 칸 0이면 보스 탄 모양과 색으로 그린다. 상한 256은 모든 투사체가 나눠 쓴다.
- `SurvivorSim.ApplyBlast(center, radius, damage, bodies)`가 반지름 안의 살아 있는 생존자에게 `ApplyDamage`로 피해를 주고(보호 시간, 쓰러짐 거절) 맞은 수를 돌려준다. `CombatEvent.Explosion`(6바이트: 위치, 반지름 1/10 단위)이 폭발을 전하고 `ExplosionReplay`와 `CombatView`가 표시 시점에 고리를 그린다. `MobActionRunner`가 몹의 행동 목록을 이 둘로 바꾼다.
- 보스 입력은 `PlayerCombat`(조준, 왼쪽 클릭 발사)과 `PlayerSummon`(Space 소환, 1~5 종류, 오른쪽 클릭 명령, C 해제)이 읽는다. 키 입력은 "지난 프레임 상태와 비교"로 한 번만 처리한다(에디터에서 포커스가 없어도 동작하도록). `PlayerSummon`은 보스 소유자만 읽는 `NetworkVariable`(기운, 명령)을 갖고 `SummonHud`(IMGUI)가 보스에게만 그린다. 서버는 `SummonServer`가 이 값을 쓴다.
- Q/E는 생존자의 스킬 키(`PlayerGrowth`)이고 보스 아바타는 이 입력을 읽지 않는다(보스는 선택지도 스킬도 갖지 않는다).
- `RoleTable.MatchRunning`이 서버 갱신의 정지 스위치이고 판이 시작되거나 대기방으로 돌아갈 때 `CombatServer.ResetAll`이 상태를 비운다.

동기: proposal.md 참고.

## Goals / Non-Goals

**Goals:**
- 스킬 규칙(쿨타임, 발동 조건, 탄 고리, 범위 피해, 강화)을 순수 C#(`BossSkillSim`)에 두어 EditMode로 시험한다.
- 새 이벤트 형식이나 새 네트워크 메시지 종류를 만들지 않는다. 탄 고리는 기존 발사 이벤트, 충격파는 기존 폭발 이벤트를 쓴다.
- 기존 동작(보스 쏘기, 소환, 몹 행동)은 달라지지 않는다.

**Non-Goals:**
- 다른 스킬(돌진, 방어막, 몹 강화), 충격파로 생존자를 밀어내는 것, 몹 폭발 신호의 경고 표시, 충격파와 몹 폭발의 효과 구분(같은 고리를 쓴다).
- 보스 마나. 스킬은 쿨타임만 가진다.

## Decisions

### 1. 스킬 값은 `BossSkills`(순수 C# 정적 값)에 모으고 강화는 `BossAttack`의 함수를 그대로 쓴다
`BossSkills`: 슬롯 0 탄막(발수 12, 기본 피해 10, 쿨타임 10초, 탄 모양은 `BossAttack.Shape`), 슬롯 1 충격파(반지름 4.0, 기본 피해 30, 쿨타임 8초), 이름("탄막", "충격파"). 모두 가정값이다. 강화는 새 계산을 만들지 않고 `BossAttack.Damage(skillBase, healthFraction)`와 `baseCooldown * BossAttack.CooldownMultiplier(healthFraction)`를 부른다. 그래서 "체력이 줄수록 더 아프고 더 자주"가 보스의 쏘기와 같은 규칙이고, 값이 바뀌어도 한곳만 고치면 된다. 개발 인자 `-bsBossSkillDamage`는 두 스킬의 기본 피해를 바꾼다(강화 전 값).
- 대안: 스킬에는 강화를 쓰지 않는다. 체력이 낮은 보스가 쏘기만 세지고 스킬이 그대로라 "위험한 보스" 느낌이 어긋난다.

### 2. 판정은 순수 C# `BossSkillSim`, 서버 연결은 `CombatServer`가 한다
`BossSkillSim`은 슬롯별 쿨타임(초)과 예고 중인 스킬 목록을 갖고 `Reset()`(둘 다 준비, 예고 취소), `TryUse(slot, fromBoss, 상태)`와 `Step(dt, 세계)`를 가진다. `TryUse`의 결과는 `BossSkillResult`: `Used`(예고 시작, 쿨타임 시작), `Cooldown`, `Unavailable`(판이 안 돌거나 보스가 쓰러짐), `Ignored`(보스가 아님), `UnknownSlot`. 순서: 보스 아님 -> `Ignored`, 판/보스 상태 -> `Unavailable`, 모르는 슬롯 -> `UnknownSlot`, 쿨타임 -> `Cooldown`, 예고 시작. 쓰는 순간 보스 위치, 조준 각도, 체력 비율로 정한 강화된 피해와 쿨타임을 저장한다. `Step(dt, matchRunning, 세계)`는 쿨타임과 예고 타이머를 줄이다가(판이 도는 동안만) 예고가 끝난 스킬을 실행하고, 실행 시점에 판이 안 돌거나 보스가 쓰러졌으면 취소한다. 세계(`BossSkillWorld`)는 `ProjectileSim`, `SurvivorSim`, 생존자 몸 목록, 보스 소유자 id, 보스가 서 있는지, 결과 이벤트 목록이다(`MobActionRunner`처럼 평범한 클래스).
- 탄막 실행: `i = 0..11`에 대해 저장한 위치에서 각도 `aim + i * 65536 / 12`(16비트 각도, 오버플로 무시)로 `ProjectileSpec { Owner = 보스 소유자, Hostile, 저장한 피해, BossAttack.Shape }`를 `TryFire`하고 실패하면 멈춘다(0발이어도 쿨타임은 이미 시작됐다).
- 충격파 실행: `SurvivorSim.ApplyBlast(저장한 위치, 반지름, 저장한 피해, bodies)`를 부르고 `Explosion` 이벤트(`Radius10 = QuantizeRadius(반지름)`)를 이벤트 목록에 더한다. 몹과 보스는 건드리지 않는다.
- 예고: `TryUse`가 성공하면 예고 이벤트(스킬 이벤트에 보스 스킬 번호 200/201)를 만들어 이벤트 목록에 더한다. 예고 시간은 `BossSkills.WarningSeconds`(0.8초)다.
- 시간 정지(`MobSim.Freeze`)는 이 시뮬레이션이 모르므로 영향이 없다.

### 3. 서버와 입력: RPC 하나, 쿨타임은 보스에게만 복제
`PlayerSummon`에 `SkillRpc(byte slot)`을 더하고(보스 소유자만 보냄, 서버에서 보낸 사람이 보스인지 `BossSkillSim`이 `fromBoss`로 확인) Q/E 키를 읽는다(지난 프레임 상태와 비교, 0.1초 최소 간격). 호스트가 보스면 RPC 없이 직접 부른다. `CombatServer.OnBossSkillRequest(clientId, slot)`가 보스 소유자와 비교해 `BossSkillSim.TryUse`를 부르고, `FixedUpdate`가 `BossSkillSim.Step`을 돌리고 쿨타임을 `PlayerSummon`의 보스 소유자 읽기 전용 `NetworkVariable<byte>` 둘(남은 쿨타임, 1/10초 단위, 최대 25.5초)에 쓴다(변할 때만). 충격파 이벤트는 `CombatServer`의 폭발 이벤트 목록(`blastEvents`)에 쌓여 다음 틱의 이벤트 발행에 실려 나가고, 탄 고리의 발사 이벤트는 `ProjectileSim`의 이벤트로 같은 발행에 나간다.
- 대안: 스킬 RPC를 새로 만들지 않고 `SetAimRpc`에 비트를 얹는다. 조준 전송 제한(초당 20회)에 스킬 입력이 묻히고 의미가 섞인다. RPC 하나가 더 분명하다.
- 대안: 쿨타임을 클라이언트가 계산한다. 서버 쿨타임과 어긋날 수 있어 복제 값을 쓴다(값 둘, 변화가 느려 트래픽은 무시할 수준).

### 4. 이벤트와 표시는 재사용한다 (예고도 스킬 이벤트)
예고는 기존 스킬 이벤트(`CombatEventKind.Skill`: 스킬 번호, 칸, 위치, 순위, 조준 각도)에 보스 스킬 번호(`BossSkills.WarningIdBarrage = 200`, `WarningIdShockwave = 201`)를 실어 발행한다. 클라이언트의 `CombatView.OnSkill`은 스킬 표에서 번호를 찾기 전에 이 번호를 알아보고 경고 고리를 그린다: 충격파는 충격파 반지름까지 `WarningSeconds` 동안 커지는 붉은 고리(`AddRing(위치, 반지름, 붉은색, 예고 시간, 0.1)`), 탄막은 보스 둘레 반지름 2의 주황 고리다. 번호 200 이상은 생존자 스킬 표에 없으므로 기존 생존자 스킬 처리와 겹치지 않고, 이벤트 종류와 크기가 그대로이므로 코덱은 바뀌지 않는다. 표시 시점은 기존 `SkillEffectReplay`가 맞춘다(스킬 이벤트는 표시 시계가 서버 시각에 닿으면 꺼냄).
탄 고리의 탄은 발사 이벤트 12개(각 10바이트, 발사자 칸 0)로 나가고 클라이언트가 칸 0이면 보스 탄으로 그린다. 충격파는 `Explosion` 이벤트 하나(6바이트)이고 같은 고리 효과가 보스 위치에 그려진다(몹 폭발과 같은 노란 고리, 효과 구분은 non-goal). 트래픽: 12발 + 폭발 하나가 10초와 8초에 한 번씩이라 평균 초당 약 20바이트이며, 한꺼번에 나가는 12발(약 130바이트)은 배치 한 메시지에 들어간다.

### 5. `SummonHud`가 쿨타임을 보여 준다
`SummonHud`에 스킬 줄을 더한다: 종류 줄 옆(위)에 "Q 탄막 준비 / E 충격파 3.2초" 두 칸, 준비되면 밝게, 쿨타임이면 흐리게와 남은 초. 키 안내 문구에 "Q/E 스킬"을 더한다. 보스에게만 보이고 생존자와 연습 판(보스 없음)에서는 숨긴다.

### 6. 정지와 초기화
`BossSkillSim.Step`은 `RoleTable.MatchRunning`일 때만 줄고(예고 타이머 포함), `TryUse`가 `Unavailable`로 판 종료와 보스 쓰러짐을 거절하며, 예고 중에 판이 끝나거나 보스가 쓰러지면 그 스킬은 취소된다. `CombatServer.ResetAll`과 `RegisterBoss`가 `Reset()`을 불러 새 판이 둘 다 준비된 채 시작한다. 개발 인자 `-bsAutoBossSkill <q|e|qe> <초>`는 보스 소유자가 그 간격으로 스킬 키를 누른 것처럼 보낸다(결과 상태에서도 계속 보내서 서버 거절을 볼 수 있게 한다, `-bsAutoSummon`과 같은 방식).

## Risks / Trade-offs

- [예고 0.8초가 너무 짧거나 길음] → `BossSkills.WarningSeconds` 한곳의 값이다. 생존자 이동 속도 5.5~7이면 0.8초에 4~5.6을 움직여 충격파 반지름 4를 벗어날 수 있다.
- [예고는 보이지만 보스가 움직여 자리가 어긋남] → 효과는 쓴 순간의 자리에서 일어나고 경고 고리도 그 자리에 뜬다(둘이 같다).
- [스킬이 너무 세거나 약함] → 값은 모두 `BossSkills` 한곳에 있고 `-bsBossSkillDamage`로 시험에서 바꾼다. 균형은 플레이로 정한다.
- [탄 고리가 상한 256을 채움] → 12발이 최대 10초에 한 번이고 수명 1.6초라 상한에 영향이 작다. 걸리면 나간 만큼만 나간다.
- [Q/E가 보스에게 의도치 않게 눌림] → 쿨타임이 막고 헛발도 화면에 보이는 효과뿐이다.
- [충격파와 몹 폭발이 같은 효과라 구분이 안 됨] → 위치와 시점으로 알 수 있고 구분은 다음에 한다(이벤트 형식 변경이 필요해서 이번에는 뺀다).
- [쿨타임 복제 값이 한 틱 늦게 보임] → 표시용 값이라 규칙에는 영향이 없다.

## Migration Plan

- 저장 데이터와 새 이벤트 형식이 없다. 새 RPC 하나와 보스 읽기 전용 복제 값 둘이 `PlayerSummon`(Player 프리팹)에 더해지므로 씬 설정(`BS > Setup Network Scene`)은 필요 없다(컴포넌트는 이미 있고 필드만 늘어남). 같은 빌드끼리만 접속한다. 개발 빌드는 다시 만든다.
- 되돌리기: `CombatServer`가 스킬 요청을 무시하게 하면 이전 동작이다.

## Open Questions

- 충격파와 몹 폭발의 효과 구분과 몹 폭발 신호의 경고 표시는 다음에 정한다. 규칙에는 영향이 없다.
