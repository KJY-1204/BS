# Tasks

## 1. 배율과 패시브 데이터 (skill-effect-scale, character-passives, skill-tree-selection)

- [x] 1.1 랭크 포장을 `(id - 1) * 3` 비트, `MaxPackedRank = 7`, `MaxSkillId = 21`로 바꾼다(`LevelingSim`의 랭크 배열 크기 확인 포함). EditMode 테스트로 번호 1~21과 랭크 0~7의 왕복(번호끼리 섞이지 않음), 모든 `SkillDef`의 `MaxRank <= MaxPackedRank`를 확인하고, 포장 값을 4비트로 가정한 기존 테스트(`CharacterPassiveTests`의 포장 테스트 등)를 새 규칙에 맞게 고친다
- [x] 1.2 `PassiveEffectKind`에 `SkillRange`, `SkillDuration`을 더하고 전용 패시브 두 개(17 결계 연장/레이무, 지속 랭크당 20%, 선행 결계 1; 18 넓은 바람/사나에, 범위 랭크당 15%, 선행 바람의 장막 1; 최대 랭크 3)를 `SkillCatalog`에 정의한다. `RangeScalePercent`/`DurationScalePercent`(`clamp(round(100 x (1 + 합)), 100, 250)`)를 더한다. EditMode 테스트로 두 패시브의 소유 캐릭터, 선행 조건, 최대 랭크, 배율 계산(랭크별, 상한, 패시브 없음 100, 남의 캐릭터 무변화)을 확인하고, "캐릭터당 전용 패시브 정확히 하나"를 가정한 기존 테스트를 "하나 이상, 효과 종류가 서로 다름"으로 고친다
- [x] 1.3 선택지 규칙: 새 두 패시브가 자기 캐릭터에게만, 선행 스킬 랭크 1 뒤에만, 최대 랭크 전까지만 나오는지 EditMode 테스트로 확인한다(결계 없이 지속 패시브 불가, 바람의 장막 없이 범위 패시브 불가, 기존 전용 패시브와 함께 풀에 들어감, 마리사와 사쿠야에게는 나오지 않음). `LevelingSim` 코드는 바뀌지 않아야 한다

## 2. 서버 판정과 이벤트 (skill-effect-scale, attack-replication, character-skills)

- [x] 2.1 `SkillActivation`에 `RangeScale`, `DurationScale`을 더하고 `SkillSim.TryActivate`가 반지름, 길이, 폭, 밀어내는 거리에 범위 배율을, `BuffSeconds`(보호, 공격 속도, 시간 정지)에 지속 배율을 `표 값 x 배율 / 100f`로 곱하게 한다. EditMode 테스트로 배율 100이면 모든 기존 스킬의 값이 이전과 같은 것, 결계 지속과 바람의 장막 반경/거리와 기적 회복 반경이 배율대로 커지는 것, 그 값을 쓰지 않는 스킬은 배율에 무변화인 것, 서버 실행(`SkillExecutor`)이 큰 반경에서 더 먼 몹과 아군에 닿는 것을 확인한다
- [x] 2.2 스킬 이벤트에 두 배율 바이트를 더한다(`CombatEvent`, `CombatCodec`: `SkillSize` 12, 조준 각도 뒤). `GrowthServer`가 활성화의 배율을 이벤트에 싣고, 보스 스킬 경고 이벤트는 100을 싣는다. EditMode 테스트로 부호화 왕복(배율 100, 140, 250), 청크 분할(스킬 이벤트가 많이 섞인 묶음), 경고 이벤트의 100, 기존 이벤트 종류 무변화를 확인한다
- [x] 2.3 서버 로그 `skill fired`에 `rangeScale`, `durationScale`을 남기고, 전투 이벤트 덤프의 `Skill` 줄에 두 배율을 넣어 `tools/compare_combat.py`가 서버와 클라이언트의 배율을 비교하게 한다(배율이 다르면 MISMATCH). 기존 로그 파서와 이전 스크립트가 깨지지 않는지 확인한다

## 3. 클라이언트 그림 (skill-effect-scale)

- [x] 3.1 `SkillEffectReplay`/`CombatClient`가 이벤트의 두 배율을 `SkillEffect`로 전달하게 하고, `CombatView.OnSkill`의 크기 계산을 순수 정적 함수(표 값 x 배율 / 100f)로 빼서 반지름 고리, 광선 길이와 폭, 오라 시간에 배율을 적용한다. EditMode 테스트로 배율 100이면 이전과 같은 값, 140이면 1.4배인 것, 서버 활성화의 값과 같은 것(같은 입력에서 `SkillActivation.Radius`와 그림용 반지름이 일치)을 확인한다
- [x] 3.2 개발 인자 `-bsDevPassive`가 새 전용 패시브도 그 랭크로 주는지 확인하고(전용 패시브를 모두 그 랭크로), 에디터 Game View로 레이무의 결계 지속 패시브 후 오라가 길어지는 것과 사나에의 바람의 장막 고리가 커지는 것을 눈으로 확인한다

## 4. 통합 시험과 문서 (전체)

- [x] 4.1 (통합) 보스 + 생존자 3명(레이무, 사쿠야, 사나에, 대기방을 거침): `-bsDevSkills 3 -bsDevPassive 3`으로 서버 로그의 `rangeScale`/`durationScale`이 패시브 랭크에 맞게 나오고(결계 지속 160, 바람의 장막과 기적 범위 145, 그 외 100), 서버와 모든 클라이언트의 전투 이벤트와 배율이 일치하고, 패시브 없이는 100인 것, 몹 위치가 일치하는 것을 확인한다. 큰 반경의 바람의 장막이 더 먼 몹을 밀어내는 것을 로그의 `touched`로 확인한다
- [x] 4.2 (통합, 수동) 실제 Relay로 보스(호스트) + 생존자 3명이 레벨업과 새 패시브 선택을 포함해 한 판을 끝까지(결과, 복귀, 둘째 판) 하는 흐름과 전투 트래픽(클라이언트당 10 KB/s 이하), 몹 트래픽(50 KB/s 이하), 서버 틱 시간을 측정한다
- [x] 4.3 기존 EditMode 테스트 전체와 이전 통합 로그 비교(몹 위치, 전투 이벤트, 부활, 성장 선택, 대기방 흐름, 캐릭터별 공격과 스킬, 전용 패시브, 보스 승패, 소환과 명령, 보스 공격과 스킬, 몹 종류와 행동)를 다시 돌려 회귀가 없는 것을 확인하고, README(스킬 이벤트 크기, 배율 규칙, 새 패시브 표, 포장 변경, 테스트 수, 측정 결과, 알려진 한계)를 갱신한 뒤 문서의 명령을 그대로 따라 해서 재현되는지 확인한다

## Workflow follow-up

- 구현이 끝나면 커밋과 푸시 후 `/opsx:archive add-skill-effect-sync`로 spec을 동기화한다(새 capability `skill-effect-scale`, MODIFIED 네 건: attack-replication, character-passives, character-skills, skill-tree-selection).
- 다음 change: 마리사와 사쿠야의 범위/지속 패시브와 캐릭터마다 선택지 여러 개(전체 트리), 보스 종류 2종, 자동 소환 장치, 균형 정리, 마지막에 Aseprite 에셋과 동방 가이드라인 재확인(팬 작품 표기).
