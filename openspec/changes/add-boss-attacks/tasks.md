# Tasks

## 1. 순수 C# 규칙 (boss-projectiles, boss-enrage, survivor-health)

- [x] 1.1 `BossAttack`(순수 C#)을 만든다: 보스 공격 정의(`AttackDef` 호환: 한 발, 쿨타임 0.8, 속도 11, 수명 1.6, 반지름 0.3, 관통 1)와 기본 피해 15, 강화 값(쿨타임 최소 0.5배, 피해 최대 1.5배), `Multipliers(healthFraction)`(연속 보간, 피해는 반올림하고 1 미만이 되지 않음)를 둔다. EditMode 테스트로 가득 찬 체력의 기본 값, 절반 체력의 중간 값, 체력 1의 최저/최고 근처와 범위를 넘지 않는 것, 체력 비율 0과 1 밖의 입력 보정, 피해가 정수이고 1 이상인 것을 확인한다
- [x] 1.2 `ProjectileSpec`/`AttackSim`에 "적대" 표시를 더한다: `AttackSim.Register(clientId, attack, hostile)`로 등록한 사수의 투사체가 적대로 나가고, 기존 `Register` 두 가지는 비적대 그대로이다. 보스가 쏘는 한 발의 간격, 놓았다 눌러도 더 빨라지지 않는 것, 상한에서 거절돼도 쿨타임이 쓰이지 않는 것, `SetModifiers`(강화 배율)가 다음 쿨타임에만 적용되는 것을 EditMode 테스트로 확인한다. 기존 `AttackSim` 테스트가 그대로 통과한다
- [x] 1.3 `ProjectileSim`에 생존자 표적을 더한다: `ISurvivorTargets` 인터페이스와 `Step(dt, mobs, boss, survivors)`, 투사체별 적대 표시. 적대 투사체는 몹과 보스를 지나가고 맞힐 수 있는 생존자(살아 있고 보호 시간이 아님)의 쓸어 맞히기에서 가장 가까운 하나를 맞혀 끝나며, 못 맞히는 생존자(쓰러졌거나 보호 중)는 지나간다. 생존자 투사체는 생존자를 지나간다. 명중 이벤트는 `SurvivorTarget`, 생존자 칸, 맞은 뒤 체력 비율, 쓰러짐을 싣는다. EditMode 테스트로 맞힘, 몹/보스 통과, 보호 중/쓰러진 생존자 통과, 한 틱에 큰 이동(터널링 없음), 둘이 겹칠 때 가까운 쪽, 가득 찬 상한, 생존자 투사체가 생존자를 안 맞히는 것, 피해가 쏜 시점 값을 유지하는 것을 확인한다
- [x] 1.4 `CombatCodec`의 `Hit`에 flags 비트 2(생존자 표적)를 더한다. 크기는 그대로(11바이트)이고 `CombatEvent.SurvivorTarget`이 쓰고 읽을 때 왕복하며, 기존 비트(죽임, 계속 날아감)와 독립이고, 기존 코덱 테스트가 그대로 통과하는 것을 EditMode 테스트로 확인한다
- [x] 1.5 `SurvivorSim` 피해 경로가 투사체 피해를 같은 규칙으로 처리하는 것(체력 하한 0, 보호 시간, 쓰러짐, 부활 보호 시간 중 통과)을 EditMode 테스트로 확인한다(`ApplyDamage` 재사용 어댑터의 `CanBeHit`/`TakeDamage` 포함). 기존 생존자 테스트가 그대로 통과한다

## 2. 서버 연결 (boss-projectiles, boss-enrage)

- [x] 2.1 `CombatServer`: `RegisterBoss`에서 보스를 적대 사수로 등록하고(`UnregisterBoss`/`ResetAll`에서 해제), 보스 슬롯 0을 `slots`에 넣는다. 매 틱 보스 체력으로 `BossAttack.Multipliers`를 구해 `SetModifiers`를 부르고, 보스의 `ShooterBody`(`CanFire = Boss.CanBeHit`)를 사수 목록에 더한다. `SetAim`은 보내는 사람이 보스 소유자면 `Boss.CanBeHit`로 발사를 허용한다. `Projectiles.Step`에 생존자 어댑터를 넘기고, 적대 명중 이벤트의 `Killed`가 몹 처치 위치 목록(경험치)에 들어가지 않게 한다. 에디터 `eval`로 보스 발사, 생존자 피해, 보호 시간, 쓰러진 보스/끝난 판에서 발사가 안 되는 것, 둘째 판 초기화를 확인한다
- [x] 2.2 `BossAttack`의 `-bsBossShotDamage <n>` 덮어쓰기를 `DevArgs`에 더하고 서버가 쓰게 한다. `-bsBossFire`(보스가 처음부터 발사)도 `DevArgs`에 더한다. 서버의 `[BS] combatdump`에 표적 종류(`target=mob|survivor|boss`)를 더하고 5초 전투 통계에 보스 발사/생존자 명중 수를 더한다

## 3. 입력과 화면 (boss-projectiles)

- [x] 3.1 `PlayerCombat`: 보스 소유자도 조준과 발사 입력을 읽는다(포인터, 스틱, 왼쪽 클릭, 오른쪽 트리거, 개발 조준 인자). 자동 발사는 `-bsBossFire`만 보스에 적용하고 `-bsFire`는 계속 생존자 전용으로 둔다. 보스 입력은 판이 진행 중일 때만 보낸다. 생존자 쪽 동작은 그대로이다. 에디터에서 보스 소유자 입력이 서버에 도착하는 것을 `eval`/로그로 확인한다
- [x] 3.2 `PlayerSummon`에서 왼쪽 클릭 소환을 지우고 Space만 남긴다(오른쪽 클릭 명령과 C 해제는 그대로). `SummonHud`의 키 안내를 "왼쪽 클릭 발사   Space 소환   오른쪽 클릭 목표 지정   C 지정 해제"로 바꾼다. 소환 자동 시험 인자는 그대로 동작한다
- [x] 3.3 클라이언트 표시: `CombatClient.ShapeOfShooterSlot`이 칸 0이면 `BossAttack.Shape`를 돌려주고, `CombatView`가 칸 0 투사체를 보스 색과 실제 크기로 그리며, 명중 효과의 `Survivor` 값이면 몹 체력 막대를 건드리지 않고 번쩍임만 보인다. `ProjectileReplay.OnHit`이 생존자 표시를 `HitEffect`에 실어 보낸다. EditMode 테스트로 리플레이의 보스 모양과 생존자 명중 표시를 확인하고, 에디터 Game View 캡처로 보스 시점과 생존자 시점의 보스 투사체를 확인한다

## 4. 통합 시험 (전체)

- [x] 4.1 (통합) 보스 + 생존자 3명(대기방을 거침): `-bsBossFire`와 조준 인자로 보스가 쏘고, 서버 로그에서 생존자 피해와 보호 시간 통과, 쓰러짐/부활이 규칙대로이고, `tools/compare_combat.py`로 전투 이벤트가 서버와 모든 클라이언트에서 일치하며, 클라이언트 투사체 수 및 생존자 명중 효과가 서버의 보스 발사/생존자 명중과 맞는 것을 확인한다. `tools/compare_mobdumps.py`로 몹 위치도 일치한다
- [x] 4.2 (통합) 강화: `-bsBossHealth`를 낮게 하고 생존자가 보스를 깎는 판에서 체력 구간별 발사 간격과 투사체 피해가 늘어나는 것(서버 로그)을 확인하고, 생존자 클라이언트에 강화 전용 값이 없는 것을 확인한다
- [x] 4.3 (통합) 정지와 정리: 결과 상태/보스 쓰러진 뒤 보스가 발사를 시도해도 투사체가 안 생기는 것, 생존자 요청이 보스 발사에 영향이 없는 것, 대기방 복귀 뒤 둘째 판의 보스 쿨타임/강화가 처음과 같은 것, 연습 판(보스 없음)의 동작이 이전과 같은 것을 로그로 확인한다
- [x] 4.4 (통합, 수동) 실제 Relay로 보스(호스트) + 생존자 3명이 소환, 명령, 발사를 쓰며 한 판을 끝까지(결과, 복귀, 둘째 판) 하는 흐름과 전투 트래픽(클라이언트당 10 KB/s 이하, 보스가 가장 빠르게 쏠 때 포함), 몹 트래픽(50 KB/s 이하)을 측정한다
- [x] 4.5 기존 EditMode 테스트 전체와 이전 통합 로그 비교(몹 위치, 전투 이벤트, 부활, 성장 선택, 대기방 흐름, 캐릭터별 공격과 스킬, 보스 승패, 소환/명령)를 다시 돌려 회귀가 없는 것을 확인하고, README(보스 공격 설명, 키 표(왼쪽 클릭 발사, Space 소환), 값 표, 개발 인자, 테스트 수, 측정 결과, 알려진 한계)를 갱신한 뒤 문서의 명령을 그대로 따라 해서 재현되는지 확인한다

## Workflow follow-up

- 구현이 끝나면 커밋과 푸시 후 `/opsx:archive add-boss-attacks`로 spec을 동기화한다(새 capability 둘, MODIFIED 세 건).
- 다음 change: 보스 스킬과 종류 2종, 자동 소환 장치, 몹 종류 다양화, 캐릭터별 스킬 트리, 마지막에 Aseprite 에셋과 동방 가이드라인 재확인(팬 작품 표기).
