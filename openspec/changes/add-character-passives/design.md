# Design

## Context

생존자 성장(`add-survivor-growth`)과 캐릭터별 공격과 스킬(`add-character-attacks`) 이후 이 change가 의존하는 현재 구조는 다음과 같다(코드로 확인).

- `SkillCatalog`가 모든 스킬을 `SkillDef` 데이터(번호, 이름, 능동 여부, 소유 캐릭터 `Character`, 효과 종류, 최대 랭크, 선행 스킬과 랭크, 랭크별 값)로 갖고, 번호 1~12가 쓰인다. 공통 패시브 네 개(힘 1, 기민 2, 활력 3, 집중 4)는 `Character = None`이고 능동 스킬 여덟 개는 각자 `Character`가 있다.
- 선택지 규칙은 `SkillCatalog.IsAvailable(def, ranks, character)`가 한곳에서 정한다: 소유(`Character`가 None이거나 같음), 최대 랭크 미만, 선행 스킬 랭크 충족. `LevelingSim.OpenNextOffer`가 이 함수로 풀을 만들고 `GrowthConfig.OfferChoices`(3)개를 무작위로 고른다. 그래서 `Character`가 있는 패시브를 정의하기만 하면 그 캐릭터에게만 선택지로 나온다(선택 규칙, 복제, `GrowthHud`의 선택 패널은 바뀌지 않는다).
- 랭크는 소유자에게만 가는 `ulong` 하나에 스킬 번호당 4비트로 포장된다(`Pack`/`Unpack`, `MaxSkillId = 15`, 번호 `id`가 `id * 4` 비트 자리). 번호 0은 쓰지 않아 자리 0이 비어 있다.
- 패시브의 효과는 `StatCalc.Compute(level, ranks, character)`가 랭크에서 계산한다(힘은 투사체 피해, 기민은 기본 공격 쿨타임 배율, 활력은 최대 체력, 집중은 최대 마나와 마나 회복). 스킬 발동은 `SkillSim.TryActivate`가 마나, 쿨타임, `SkillActivation`(피해, 반지름, 지속 등 랭크별 값)을 정하고 `SkillExecutor.Apply`가 효과를 적용한다.
- 클라이언트의 슬롯 표시(`GrowthHud`)는 `SkillDef`의 `ManaCost`로 마나 부족을, `Cooldown`으로 쿨타임 막대 비율을 그린다. 소유자는 자기 랭크를 안다(`PlayerGrowth`).
- 스킬의 시각 효과(반지름, 지속 등)는 클라이언트가 `SkillDef`의 랭크별 값으로 그린다. 그래서 효과 크기를 바꾸는 패시브는 그림이 서버와 어긋난다.

동기: proposal.md 참고.

## Goals / Non-Goals

**Goals:**
- 전용 패시브 네 개를 순수 C# 데이터와 `SkillSim`/`StatCalc`의 작은 확장으로 만들고 EditMode로 시험한다.
- 새 네트워크 메시지나 형식이 없고, 선택 규칙과 복제 경로는 그대로다.
- 기존 공통 패시브, 능동 스킬, 선택 규칙의 동작은 달라지지 않는다.

**Non-Goals:**
- 캐릭터마다 선택지 여러 개(전체 트리).
- 새 서버 규칙이 필요한 효과(관통 수, 투사체 수, 이동 속도 성장).
- 효과 크기를 키우는 패시브(범위, 지속 시간): 시각 효과 그림이 서버와 어긋난다.

## Decisions

### 1. 전용 패시브는 `SkillDef`의 `Character`와 새 패시브 효과 필드로 정의한다
`SkillDef`에 `PassiveEffectKind Passive`(None, `SkillManaCost`, `SkillPower`, `SkillCooldown`, `ManaRegen`)와 `float PassivePerRank`를 더한다. 새 스킬 네 개(`IsActive = false`, `Character` 설정, `MaxRank = 3`, `RequiresSkill` = 그 캐릭터의 Q 스킬, `RequiresRank = 1`):

| 번호 | 이름 | 캐릭터 | 효과 종류 | 랭크당 | 선행 |
|---|---|---|---|---|---|
| 13 | 영력 절약 | 레이무 | 스킬 마나 소모 감소 | 10% | 몽상봉인 1 |
| 14 | 화력 제일 | 마리사 | 스킬 피해와 회복량 증가 | 15% | 마스터 스파크 1 |
| 15 | 시간 감각 | 사쿠야 | 스킬 쿨타임 감소 | 8% | 시간 정지 1 |
| 16 | 신앙 | 사나에 | 마나 회복 증가 | 초당 1.5 | 기적 1 |

값은 모두 가정값이다. 선택지 규칙(`IsAvailable`)은 이미 소유와 선행 조건을 처리하므로 `LevelingSim`은 바뀌지 않는다. 효과 종류를 열거형으로 둔 이유는 네 패시브가 서로 다른 스탯을 건드리고 값만 바꿔 조절하게 하기 위해서다.
- 대안: 패시브마다 `StatCalc`에 전용 분기를 넣는다. 열거형과 한 줄 합산 함수(`PassiveTotal`)가 더 작고, 나중에 전체 트리를 만들 때 효과 종류를 늘리기 쉽다.
- 효과 종류를 고른 이유: 마나 소모, 피해/회복량, 쿨타임, 마나 회복은 서버 계산만으로 끝나고 클라이언트의 효과 그림(반지름, 지속)에 영향이 없다. 범위와 지속 시간은 그림이 어긋나서 뺐다.

### 2. 효과 계산은 `SkillCatalog`의 작은 함수들이다
`PassiveTotal(ranks, character, kind)`는 그 캐릭터가 소유한 패시브 중 `kind`인 것의 `랭크 x 랭크당 값`을 더한다(모든 캐릭터에서 같은 코드, 남의 패시브는 소유 규칙으로 랭크 0). 이를 쓰는 함수:
- `EffectiveManaCost(def, ranks, character) = max(1, round(def.ManaCost x (1 - total)))`, 감소율 총합은 0.9로 제한.
- `EffectiveCooldown(def, ranks, character) = def.Cooldown x max(MinSkillCooldownMultiplier, 1 - total)`, 하한 `MinSkillCooldownMultiplier = 0.4`.
- 피해와 회복량: `activation.Damage`와 `activation.Amount`를 `round(x (1 + total))`로 키운다(0이면 그대로).
`SkillSim.TryActivate`가 이 함수들로 마나 확인과 소모, 쿨타임 시작, `SkillActivation`의 피해와 회복량을 정한다. 마나 회복은 `StatCalc.Compute`가 `PassiveTotal(ManaRegen)`을 `ManaRegenPerSecond`에 더한다(`GrowthServer`가 이미 랭크가 바뀔 때 `StatCalc`를 다시 부르고 `SkillSim.SetStats`로 알린다).
- 쿨타임은 발동 순간에 정해지므로 진행 중인 쿨타임은 그대로이고 다음 것부터 짧아진다(spec).

### 3. 랭크 포장은 자리를 한 칸 당겨 번호 16까지 담는다
`Pack`/`Unpack`을 번호 `id`가 `(id - 1) * 4` 비트 자리로 바꾸고 `MaxSkillId = 16`으로 한다(번호 0은 쓰지 않으니 자리 0을 비울 이유가 없다). 64비트에 번호 16개 x 4비트가 정확히 들어간다. 소유자에게만 가는 `ulong`의 배치만 바뀌므로 네트워크 형식과 크기는 그대로다(같은 빌드끼리만 접속한다는 전제). `LevelingSim`의 랭크 배열 크기가 `MaxSkillId + 1`에서 나오는지 확인한다.
- 대안: 3비트로 줄인다. 최대 랭크 5까지는 되지만 이후 확장(랭크 8 이상)이 막힌다. 한 칸 당기기가 간단하다.

### 4. 소유자 화면은 같은 함수로 맞춘다
`GrowthHud`가 슬롯의 마나 부족 판단과 쿨타임 막대 비율에 `SkillCatalog.EffectiveManaCost`와 `EffectiveCooldown`을 쓴다(소유자는 `PlayerGrowth`가 아는 자기 랭크와 캐릭터로 계산). 서버가 보내는 값(쿨타임 남은 시간)은 그대로이고 막대의 분모만 실제 쿨타임이 된다. 다른 플레이어에게는 아무것도 가지 않는다. 패시브 설명(`Description`)은 선택 패널에 그대로 보인다(예: "스킬 마나 소모 -10%").

### 5. 시험은 순수 C#과 기존 개발 인자로 한다
선택지 목록은 서버 로그(`growth: offer opened ... choices=`)에 이미 나오고 `-bsDevSkills`(시작할 때 자기 Q/E 스킬을 랭크로 습득), `-bsAutoPick`(선택 번호 자동)으로 전용 패시브를 고르게 할 수 있다. 전용 패시브를 선택지에 강제로 올리는 개발 방법이 필요하면 `-bsDevSkills`가 전용 패시브도 랭크 1로 주는 확장을 쓴다(구현 때 정한다: `-bsDevSkills`가 모든 소유 스킬의 랭크를 올리는지 확인).

## Risks / Trade-offs

- [전용 패시브가 약하거나 너무 강함] → 값은 `SkillCatalog` 한곳이고 가정값이다. 균형은 플레이로 정한다.
- [레벨업 선택지 풀이 커져 기존 시험의 선택지 구성이 달라짐] → 기존 테스트가 고치지 않고 통과하는지로 확인한다. 풀에 전용 패시브 하나가 더해질 뿐이다.
- [랭크 포장 변경으로 오래된 값과 섞임] → 같은 빌드끼리만 접속한다. 포장/해제 왕복 테스트로 번호 1~16과 랭크 0~15를 확인한다.
- [마리사만 스킬 하나(마스터 스파크)에 피해 증가가 닿음] → 마리사의 스타더스트는 피해가 없으므로 영향이 없다. 의도이며 값은 가정값이다.
- [클라이언트 표시와 서버 계산이 어긋남] → 같은 함수를 쓰고 EditMode 테스트로 서버 값과 표시용 값이 같은지 확인한다.

## Migration Plan

- 저장 데이터나 새 메시지가 없다. 소유자에게만 가는 랭크 포장의 배치가 바뀌고 같은 빌드끼리만 접속한다. 씬 설정은 필요 없다. 개발 빌드는 다시 만든다.
- 되돌리기: 새 스킬 네 개를 카탈로그에서 빼면 이전 동작이다(포장 자리 이동은 그대로 두어도 호환된다).

## Open Questions

- 효과 크기(범위, 지속)를 키우는 패시브는 스킬 효과 그림이 서버 값을 따르도록(스킬 이벤트에 크기 배율을 싣거나 소유자 랭크를 모두에게 알림) 바꾼 뒤에 다룬다. 지금 규칙에는 영향이 없다.
