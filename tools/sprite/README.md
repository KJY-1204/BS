# 스프라이트/이펙트 작업 인계 메모

## 상태 (2026-10-27)
- 생존자 4, 보스 3, 몹 6, 소환 장치: 애니메이션·스킬 시전 클립·투사체·이펙트 모두 Unity 연결 완료.
- 원본은 `ArtSource/` (`*_anim.aseprite` + `Exports/`), 유니티용은 `Assets/Art/Sprites/`, `Assets/Resources/SpriteLibrary.asset`.
- 유니티에서 다시 가져오기: 메뉴 `BS/Import Sprites` (또는 `BS.Editor.SpriteImporter.ImportAll()`).

## 규칙
- 동방 2차 창작 가이드라인: 공식 ZUN 그림을 그대로 변환하지 않는다. 사용자가 만든 AI 원본만 쓴다.
- 사용자가 만든 AI 시트는 `ArtSource/Reference/`에 둔다. 변환은 `charpipe.py` → `assemble_char.py` → `build.lua`(Aseprite CLI: `Aseprite.exe -b`).
- 코드로 그리는 이펙트: `make_shots.py`, `fx_sheets.py`, `marisa_fx.py`, `boss_rings.py`, `fx_pixel.py`, `fx_pixel2.py`, `fx_shockwave.py` → `ArtSource/Effects/*.png + .json`.
- 스크립트는 임시 폴더 경로(스크래치패드)를 인자로 받는다. 다른 컴퓨터에서는 경로를 바꿔서 실행.

## 직접 확인하지 못한 것
- 스킬 시전 장면 전체(특히 거인 포효, 도약 강타 일부 프레임, 사나에 이펙트 잘림).
- 보스 판 실제 연출 (생존자 1명 이상 필요).
- 사쿠야 Q(시간 정지)는 대체 프레임, 마리사/레이무 R은 `skill` 클립 재사용.

## 남은 선택 사항
- 어색한 프레임 수정, 파이널 스파크 R 전용 시전(투명 배경 시트 필요), 보스 dash 클립.
