# Design

## Context

보스 소환(`add-boss-summoning`) 이후 보스는 이동, 접촉 피해, 몹 소환과 명령을 한다. 이 change가 의존하는 현재 구조는 다음과 같다(코드로 확인).

- `AttackSim`은 순수 C#이고 사수 등록(`Register(clientId, AttackDef)`), 조준/발사 상태(`SetAim`), 피해와 쿨타임 배율(`SetModifiers`), 한 틱 갱신(`Step`)을 가진다. 쿨타임이 끝나고 `Firing`이면 `ProjectileSim.TryFire(spec)`을 부르고, 쏜 발수가 0이 아니면 `AttackDef.CooldownSeconds * 배율`로 쿨타임을 시작한다. 사수는 `ShooterBody.CanFire`로 켜고 끈다.
- `ProjectileSim`은 한 배열 묶음에 투사체를 갖고 `Step(dt, mobs, boss)`에서 몹과 보스를 쓸어 맞힌다(`HitTargetsOnTheWay`). 맞힌 순서는 쓸기 구간의 `T`로 정렬하고, 명중은 `Hit` 이벤트(`MobId`, 체력 비율, 죽임, 계속 날아감)로 남는다. 보스는 예약된 mob id 65535로 표시된다.
- `CombatCodec`의 `Hit` 이벤트는 11바이트이고 flags 바이트에 비트 0(죽임), 비트 1(계속 날아감)만 쓴다. `Fire` 이벤트는 사수의 칸(`slot`, 보스 0, 생존자 1~4)을 싣는다. 클라이언트는 칸에서 캐릭터를 찾아 투사체 모양과 색을 정한다(`ProjectileReplay.ShapeOfSlot`, `CombatView`).
- `SurvivorSim`은 `ApplyDamage(clientId, damage)`로 체력, 보호 시간, 죽음을 한곳에서 처리한다. 접촉 피해(몹, 보스)가 이미 이 경로를 쓴다. 쓰러졌거나 보호 시간 중이면 `false`를 돌려준다.
- `CombatServer`는 `FixedUpdate`에서 `Survivors.Step` → `Attacks.Step` → `Projectiles.Step` 순으로 돈다. `RegisterBoss`가 보스 아바타를 알고 `Boss.Exists`/`CanBeHit`로 보스 상태를 안다. `SetAim`은 `Survivors.IsAlive`일 때만 발사를 허용한다.
- 보스 아바타는 `PlayerCombat`을 갖지만 `Update`에서 보스면 바로 돌아간다(입력 없음). `PlayerSummon`은 왼쪽 클릭/Space를 소환으로 읽는다.

동기: proposal.md 참고.

## Goals / Non-Goals

**Goals:**
- 보스 발사, 적대 투사체의 생존자 명중, 체력 비례 강화를 모두 서버 순수 C#(`AttackSim`, `ProjectileSim`, `BossAttack`)에 두어 EditMode로 시험한다.
- 새 네트워크 메시지를 만들지 않는다. 기존 전투 이벤트 스트림과 `SetAimRpc`를 재사용하고, 명중 이벤트의 크기는 그대로 둔다.
- 생존자의 기존 전투(투사체 모양, 피해, 쿨타임, 스킬)와 이전 시험의 이벤트가 달라지지 않는다.

**Non-Goals:**
- 보스 스킬, 다발/관통/유도 발사, 보스 종류별 공격, 보스 투사체와 생존자 투사체의 충돌.
- 보스 투사체가 몹을 맞히는 것, 생존자 스킬이 보스 투사체에 영향을 주는 것(시간 정지 등). 시간 정지는 지금 보스 접촉 피해에도 걸리지 않으므로 보스 투사체도 걸리지 않는다.
- 강화의 시각 표현(색이 변하는 보스 등)과 강화 단계 표시.

## Decisions

### 1. 보스는 `AttackSim`의 또 하나의 사수이고, 투사체에는 "적대" 표시가 붙는다
보스 아바타가 서버에 등록될 때 `Attacks.Register(bossOwner, BossAttack.Def, hostile: true)`로 사수가 된다. 사수 상태는 이미 조준 각도, 발사 여부, 쿨타임, 피해/쿨타임 배율을 갖고 있어 새 상태가 거의 없다. `ProjectileSpec`에 `Hostile` 값을 더해 `AttackSim`이 사수의 `Hostile`을 그대로 옮긴다. `ProjectileSim`은 투사체별 `hostile` 배열을 갖고, 적대 투사체는 생존자만, 아닌 것은 지금처럼 몹과 보스만 맞힌다.
- 대안: `BossAttackSim`을 따로 만들고 투사체도 따로 둔다. 발사, 쿨타임, 상한, 이벤트, 복제를 모두 다시 만들어야 하고 생존자 전투와 중복된다.
- 한계: 한 `ProjectileSim`을 공유하므로 상한(256)과 아이디 공간을 나눠 쓴다. 보스는 최대 초당 2.5발이라 상한에는 영향이 없다.

### 2. 생존자 표적은 작은 인터페이스(`ISurvivorTargets`)로 `ProjectileSim`에 넘긴다
`ProjectileSim.Step(dt, mobs, boss, survivors)`에 선택 인자로 `ISurvivorTargets`를 더한다(`null`이면 지금과 같음). 인터페이스는 `Count`, `SlotAt(i)`, `PositionAt(i)`, `Radius`, `CanBeHit(i)`(살아 있고 보호 시간이 아님), `TakeDamage(i, damage, out healthPercent, out fell)`를 가진다. `CombatServer`가 `SurvivorSim`과 이번 틱의 몸 목록을 감싼 어댑터를 넘긴다. 적대 투사체는 `CanBeHit(i)`인 생존자만 후보로 모아 쓸기 구간 `T` 순으로 정렬하고 가장 가까운 후보를 맞힌다. 보호 시간이나 쓰러진 생존자는 후보에서 빠지므로 투사체가 지나간다(spec의 "보호된 생존자는 투사체를 막지 않는다").
- 이유: `ProjectileSim`이 `SurvivorSim`에 직접 의존하면 두 순수 시뮬레이션이 엉킨다. `IBossTarget`처럼 인터페이스로 받는 방식이 이미 있어 같은 모양을 따른다.
- 대안: 보스 투사체 명중을 `SurvivorSim.Step`에서 처리한다. 투사체를 `SurvivorSim`이 알아야 하고 쓸어 맞히기(빠른 투사체) 코드를 중복한다.

### 3. 명중 이벤트: flags 비트 2가 "표적은 생존자", `MobId` 필드는 생존자의 칸
적대 투사체가 생존자를 맞히면 `Hit` 이벤트를 `SurvivorTarget = true`, `MobId = 생존자 칸(1~4)`, `MobHealthPercent = 맞은 뒤 체력 비율`, `Killed = 쓰러짐`으로 만든다. 와이어 형식은 flags 바이트의 비트 2(값 4)만 늘고 크기는 11바이트 그대로다. 구버전과 섞일 일은 없다(같은 빌드끼리만 접속한다). 클라이언트의 `HitEffect`에 `Survivor` 값이 생겨 `CombatView.OnHit`이 몹 체력 막대를 건드리지 않고 번쩍임만 보인다. 생존자 체력은 이미 복제되는 값이 막대로 보여 준다.
- 대안: 새 이벤트 종류(`SurvivorHit`)를 더한다. 이벤트 종류가 늘고 코덱과 덤프와 비교 도구가 모두 바뀐다. 비트 하나가 더 작다.

### 4. 보스 칸 0의 모양과 색은 `BossAttack`이 정의하고 클라이언트가 규칙대로 그린다
`ProjectileReplay.ShapeOfSlot`은 지금 `RoleTable.CharacterOfRole(slot)`로 캐릭터를 찾는다. 칸이 `BossRole`이면 `BossAttack.Shape`(속도, 수명, 반지름, 관통 1)를 돌려주도록 `CombatClient.ShapeOfShooterSlot`을 고친다. `CombatView`도 칸 0이면 `BossCatalog.Default.Color`를 쓰고 크기는 실제 반지름대로 그린다. 서버와 클라이언트가 같은 `BossAttack` 정의를 공유하므로 위치는 발사 이벤트 하나로 규칙대로 재현된다. 명중과 끝은 서버가 알려 준 위치와 시간에 맞춘다.
- 고정 값: 클라이언트는 개발 인자(`-bsBossShotDamage`)를 모른다. 이 인자는 피해량만 바꾸고 모양/속도는 바꾸지 않으므로 클라이언트가 알 필요가 없다.

### 5. 강화는 연속 비율이고 서버가 매 틱 `SetModifiers`로 적용한다
`BossAttack.Multipliers(healthFraction)`는 순수 함수이다. 체력 비율 `f`(0~1)에 대해 쿨타임 배율 = `Lerp(MinCooldownFraction, 1, f)`, 피해 배율 = `Lerp(MaxDamageMultiple, 1, f)`. `CombatServer`가 매 틱 보스 체력에서 `f`를 구해 `Attacks.SetModifiers(bossOwner, round(BaseDamage * damageMultiple), cooldownMultiplier)`를 부른다. `AttackSim`은 쿨타임을 쏘는 순간 배율로 정하므로 spec의 "쿨타임이 시작되는 시점의 체력"과 같다. 피해는 발사할 때 투사체가 가져가므로 날아가는 투사체는 바뀌지 않는다(이미 `ProjectileSim`의 규칙).
- 대안: 단계(66%, 33%에서 끊기). 구간 경계에서 갑자기 바뀌어 지각하기 쉽고 단계 표시가 필요하다. 연속이 구현도 작다.
- 대안: 서버가 강화 값을 복제해 보스 화면에 표시한다. 이번엔 새 복제 값을 만들지 않는다(spec: 추가 트래픽 없음).

### 6. 값은 가정값이고 한 곳에 둔다 (`BossAttack` 상수)
기본 피해 15, 쿨타임 0.8초, 속도 11, 수명 1.6초(사거리 17.6), 반지름 0.3(지름 0.6, 생존자 투사체의 두 배). 강화는 체력 0일 때 쿨타임 0.5배(0.4초), 피해 1.5배(22). 전부 가정값이고 균형은 플레이로 정한다. 최악 초당 2.5발 × (발사 10B + 명중 11B 또는 끝 7B) ≈ 50B/s 정도로, 전투 10 KB/s 예산에 영향이 거의 없다. 참고: 생존자 접촉 피해는 10, 보스 접촉 피해는 25이고 보호 시간 0.6초라서 보스 투사체는 한 발에 15~22로 보스의 몸 부딪힘보다 약하지만 멀리서 닿는다.

### 7. 보스 입력은 `PlayerCombat`이 읽고 서버는 기존 `SetAimRpc`로 받는다
`PlayerCombat.Update`의 "보스면 반환"을 "보스는 발사 전용 입력을 읽는다"로 바꾼다. 조준 계산(포인터/스틱/개발 조준), `AimSender`, `SetAimRpc`를 그대로 재사용하고, 발사 입력은 왼쪽 클릭/오른쪽 트리거다. 개발 자동 발사는 생존자용 `-bsFire`와 분리한 `-bsBossFire`만 보스에 적용한다(기존 시험의 공통 인자 `-bsFire`가 보스까지 쏘게 하면 이전 회귀 비교가 깨진다). 조준 표시 점은 보스 소유자에게도 보인다. 쿨타임 번쩍임은 생존자용 상수를 쓰지 않고 보스 쿨타임의 근삿값(고정 값)으로 둔다. 순수 표시이므로 규칙에는 영향이 없다.
서버 `CombatServer.SetAim`은 보내는 사람이 보스 소유자면 `Boss.CanBeHit`을 발사 조건으로, 생존자면 `Survivors.IsAlive`를 조건으로 한다. 생존자가 보스 쪽 사수를 건드릴 방법은 없다(사수는 보낸 사람의 `clientId`로만 찾는다).
`PlayerSummon`에서는 왼쪽 클릭 소환을 지우고 Space만 남긴다. `SummonHud`의 키 안내 문구를 바꾼다.

### 8. 정지와 초기화는 기존 스위치를 따른다
`CombatServer.FixedUpdate`가 이미 `RoleTable.MatchRunning`이 아니면 돌지 않으므로, 판이 끝나면 새 발사도 새 명중도 없다. 날아가던 투사체는 `ResetAll`/`ResetMatch`에서 `Projectiles.Clear()`로 사라지고 클라이언트 쪽은 `Waiting` 전환에서 `Replay.Clear()`를 한다(기존 동작). 보스가 쓰러지면 `CanFire`가 꺼져 새 발사가 없다. `UnregisterBoss`가 보스 사수를 등록 해제한다. 둘째 판의 보스는 `RegisterBoss`에서 다시 등록되어 쿨타임과 배율이 처음 값이다.

## Risks / Trade-offs

- [보스 투사체가 너무 아프거나 약함] → 값은 `BossAttack` 상수에 모여 있고 `-bsBossShotDamage`로 시험에서 바꾼다. 균형은 플레이로 정한다.
- [적대 투사체가 명중해도 보호 시간 때문에 지나가서 "맞은 것 같은데 안 아픔"] → spec이 정한 동작이다. 로그에서 보호 시간 통과 사례를 센다.
- [클라이언트가 보스 투사체 모양을 잘못 찾음] → 칸 0 분기를 `CombatClient`와 `CombatView`에 같은 함수(`BossAttack.Shape`)로 둔다. 다중 프로세스 시험에서 클라이언트 투사체의 생존자 명중 효과 수와 서버의 `target=survivor` 명중 수를 비교한다.
- [`-bsFire` 분리로 보스가 다른 시험에서 쏘지 않음] → 의도다. 기존 시험은 보스가 쏘지 않는 이전 동작 그대로 회귀 비교한다.
- [생존자가 한꺼번에 죽어 판이 너무 빨리 끝남] → 보호 시간 0.6초와 투사체 피해 15로 생존자 100 체력은 최소 7발 필요하다. 균형 값은 플레이로 조정한다.
- [빠른 투사체의 터널링] → 몹/보스와 같은 쓸기 판정(구간과 원의 거리)을 쓰므로 한 틱에 지나치지 않는다.

## Migration Plan

- 저장 데이터나 새 메시지가 없다. 명중 이벤트의 flags 비트 하나만 늘고 같은 빌드끼리만 접속한다. `NetworkSceneSetup` 변경은 없다(새 컴포넌트가 없다) — 그래도 개발 빌드는 다시 만든다.
- 되돌리기: 보스 사수 등록만 빼면 보스는 이전처럼 쏘지 않는다. `ProjectileSim`의 적대 분기는 `survivors == null`이면 작동하지 않는다.

## Open Questions

- 보스 투사체와 생존자 투사체가 서로 상쇄하는 규칙은 이번엔 두지 않는다. 생존자가 막을 수단을 주고 싶으면 보스 스킬 change 때 같이 다룬다. 지금 규칙에는 영향이 없다.
