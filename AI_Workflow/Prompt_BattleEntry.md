# Battle Entry Presentation

## 1. 실행 목표와 작업 경계

전투 진입 접근 연출을 구현하고 실제 production 경로에 연결한 뒤 검증까지 완료한다.  
계획 작성이나 공통 인터페이스 추가만 하고 멈추지 않는다.

목표 흐름:

```text
기존 화면
→ 검정 덮기 0.5초
→ 완전 검정: 전투 구성·배경 연결·초기 표시 상태 준비
→ 검정 걷기 0.5초: 배경과 작은 적 스프라이트만 표시
→ 접근 0.60초: 배경 확대 + 적 스프라이트 정상 크기로 확대
→ 기존 공통 프레임별 단계적 오픈
→ 전투 UI 내용물 + 적 HUD 표시
→ 기존 전투 시작 흐름·필수 초기 연출
→ 실제 조작
```

배경과 적이 가까워지는 느낌을 추가하되, 새로운 카메라 이동 시스템이나 전투 진행 시스템은 만들지 않는다.

기존 적 스프라이트·배치·UI 구성과 gameplay flow는 유지한다. 접근과 UI 오픈이 끝나기 전에 전투 시작/턴 연출이 뒤에서 소비되지 않도록 기존 Battle entry 경계를 사용한다.

### 요구사항

| ID | 실행 요구 |
|---|---|
| R01 | 검정이 걷힌 시점에는 배경과 적 스프라이트만 보인다. |
| R02 | 적 이름·HP·방어도·상태·행동 HUD와 모든 전투 UI는 접근 전에 노출하지 않는다. |
| R03 | 적의 시작 배율은 각 적의 정상 authored pose 기준 0.8–0.9 범위에서 시작한다. 초기값은 0.85를 사용한다. |
| R04 | 배경은 현재 정상 전체 화면 표시를 기준으로 가장 넓게 보이는 상태에서 시작한다. 빈 가장자리를 만들기 위해 축소하지 않는다. |
| R05 | 배경과 적은 같은 접근 시간축으로 확대한다. |
| R06 | 최종 전투 배경은 기존 100%보다 약간 확대된 상태를 전투 동안 유지한다. |
| R07 | 연출 의도는 고전 JRPG의 원거리→근거리 접근감이다. 특정 게임의 정확한 수치나 화면을 복제하지 않는다. |
| R08 | 접근 완료 후 기존 단계·가방·스펠 슬롯·버튼·플레이어 HUD를 공통 프레임 오픈으로 표시한다. |
| R09 | 아래 초기 튜닝으로 구현한 뒤 production 화면에서 검증하고 필요하면 목적을 해치지 않는 범위에서 조정한다. |

### 명시적 제외

- 전투 수치·피해·효과·RNG·경험치·위험도·보상·스테이지 규칙 변경.
- UI 전체 재배치/재구축.
- 적의 logical position, formation, hit area 변경.
- 원본 이미지·폰트·atlas·importer 변경.
- 새 Camera / Canvas / EventSystem / tween package / input package 도입.
- 화면 흔들림·섬광·입자·신규 효과음·추가 배너 등 별도 연출 확장.
- 본 작업과 무관한 UI/현지화 부채 수정.

---

## 2. 시작 기준과 Git 안전

작업 시작 시 다음을 먼저 확인한다.

```bash
pwd
git branch --show-current
git rev-parse HEAD
git status --short
git worktree list
```

- 요청 시작 시점의 최신 로컬 HEAD를 `BaselineCommit`으로 기록한다.
- dirty/untracked 파일은 사용자 소유 변경으로 취급하고 무단 삭제·stash·reset하지 않는다.
- 다른 worktree나 사용자가 실행 중인 Unity Editor를 임의로 수정하거나 종료하지 않는다.
- 이미 동일 기능을 작업 중인 branch가 있으면 상태를 확인하고 중복 구현하지 않는다.
- 관련 파일만 stage/commit하며 `git add .`로 사용자 변경을 함께 넣지 않는다.
- push / merge / rebase / reset / history rewrite는 수행하지 않는다.

---

## 3. Required Reading과 현재 구조 확인

최신 로컬에서 필요한 범위만 읽는다.

1. `AGENTS.md`, `docs/README.md`
2. `docs/tech/ui_visual_implementation_policy.md`
3. `docs/tech/ui_architecture.md`
4. `docs/tech/localization_policy.md`
5. `docs/design/battle_presentation_source.md`
6. `docs/design/battle_ui_source.md`
7. `docs/design/first_target_ui_ux.md`
8. `docs/tech/run_battle_contract.md`
9. `docs/design/act1_background_assignment_source.md`
10. 관련 UI / Battle presentation / testing Codex skill과 focused tests

전체 문서 vault를 재귀적으로 읽는 작업으로 확대하지 않는다.

### 구현 전 확인할 주요 코드 경계

| 파일/심볼 | 확인할 현재 책임 | 이번 작업의 연결 방향 |
|---|---|---|
| `ScreenTransitionPresentation` | 검정 cover/uncover와 frame open, 입력 fence | uncover 이후 frame open 이전에 finite approach 단계를 삽입할 수 있는지 확인 |
| `BattleApplicationService` | `Build → Entry Presentation → Start` | 기존 first-ready 경계를 재사용하고 두 번째 Battle start를 만들지 않음 |
| `BattleUIPresenter` | 초기 snapshot/layout과 Battle UI entry | actor/UI 초기 상태를 검정 상태에서 준비 |
| `PrefabRunBattleScopeHandle` | Battle scope 생성, camera/background 참조, 종료/cleanup | background zoom lifetime과 cleanup을 Battle lifetime에 연결 |
| `RunLocationBackgroundView` | 실제 배경 Image와 shade | 배경 Image만 확대하고 parent shake/canvas는 유지 |
| `BattleActorMotionRig` | actor visual/action/reaction/idle 계층 | logical root가 아니라 독립 visual pose 계층에 접근 배율을 합성 |
| `BattleUI` / Enemy HUD | enemy presentation / HUD / FX anchor | 기존 명시적 참조를 사용하고 전역 탐색으로 우회하지 않음 |

파일명이나 API가 현재 로컬과 달라졌다면 실제 타입과 serialized reference를 먼저 확인한다. 문서에 적힌 이름을 근거로 새 타입을 만들지 않는다.

---

## 4. 초기 연출 튜닝

아래 값은 구현 시작값이다. 실제 production 구도에서 확인 후 필요하면 같은 목적 안에서 제한적으로 조정한다.

| 속성 | 초기값 / 계약 |
|---|---|
| 접근 시간 | `0.60초`, unscaled |
| 접근 곡선 | `EaseOutCubic` |
| 배경 시작 | 정상 전체 화면 pose `×1.00` |
| 배경 끝 | 같은 기준 `×1.12`, 전투 동안 유지 |
| 적 시작 | 각 actor의 정상 authored visual pose `×0.85` |
| 적 끝 | 각 actor의 정상 pose `×1.00` |
| 배경 기준점 | 보이는 배경 이미지의 중앙 |
| 적 기준점 | 기존 ground/foot 또는 authored visual 기준점 |
| 추가 대기 | 없음 |
| UI 프레임 | 기존 공통 단계적 Linear frame-open 사용 |
| 접근 스킵 | 기본적으로 스킵 불가. 기존 transition input fence 안에서 처리 |
| 추가 장식 | 없음 |

`1.12`는 12% 확대를 의미한다. 원본 sprite의 여백·crop 방식에 따라 체감이 달라질 수 있으므로 실제 화면에서 검증한다.

최초 튜닝은 공통의 읽기 쉬운 한 곳에서 관리한다. 적 ID별 예외 테이블이나 새로운 데이터 schema를 만들지 않는다.

---

## 5. 실제 표시·입력 타임라인

| 상태 | 배경 / 적 | 전투 UI·적 HUD | 입력·진행 |
|---|---|---|---|
| Covering | 기존 화면 유지, black cover | 새 Battle UI 노출 없음 | transition fence 유지 |
| PreparingAtBlack | 배경 1.00, 적 0.85 준비 | HUD/frame/content/tooltip 은폐 | Battle Start 전 |
| Uncovering | 배경과 작은 적만 노출 | 계속 은폐 | 입력 폐기 |
| Approaching | 배경 1→1.12, 적 0.85→1 | 계속 은폐 | fence 유지 |
| OpeningFrames | 배경 1.12, 적 정상 pose | 기존 개별 frame만 open | frame/content restore 완료 대기 |
| ContentReady | 배경/적 유지 | 최신 model 기준 UI·적 HUD 공개 | entry 완료 후 기존 Battle flow 시작 |
| BattleRunning | 배경 1.12 유지 | 기존 정상 표시 | gameplay 규칙 그대로 |
| Exit | 결과 barrier/기존 hold 동안 확대 유지 → cover | 기존 종료 UI | 완전 검정 cleanup에서 배경 pose 복원 |

‘모든 UI 표시’는 현재 snapshot이 실제로 요구하는 요소만 의미한다. 비어 있는 상태나 조건부 object를 강제로 활성화하지 않는다.

---

## 6. 구현 책임과 구조

### 6.1 Scene transition owner

기존 scene transition owner를 확장해 Battle 진입에만 선택적인 finite approach presentation을 연결한다.

- `Uncover → Approach → OpenPreparedFrames` 순서를 하나의 transition lifetime 안에서 보장한다.
- callback/task는 scene과 Battle 양쪽 cancellation/generation에 묶는다.
- transition input fence는 Cover부터 ContentReady까지 끊기지 않는다.
- `Reveal 완료 → UI 다시 숨김 → Approach` 같은 역순 구현은 금지한다.
- non-Battle caller는 기존 동작을 유지한다.
- scene owner가 enemy list나 RunState를 전역 검색하지 않는다.
- cancel/fault는 기존 recovery 경계를 사용하고 gameplay command를 재호출하지 않는다.

### 6.2 Battle actor visual pose

기존 Battle entry presentation 경계 안에 책임을 둔다.

- Build 이후 실제 actor의 authored/runtime base pose를 한 번 캡처한다.
- logical root가 아니라 적절한 visual root에 접근 배율을 적용한다.
- 해당 Transform의 모든 writer를 확인해 idle/action/reaction과 충돌하지 않게 한다.
- `Vector3.one`으로 복구하지 않는다. 원래 position/rotation/scale을 정확히 복원한다.
- X flip, 비균등 scale, ground/body FX anchor를 보존한다.
- logical enemy root, formation anchor, hit/target area는 이동·확대하지 않는다.
- 접근 완료 후 temporary approach pose만 해제하고 이후 attack/hit/death/reflow는 기존 owner가 맡는다.

### 6.3 Shared background pose

`RunLocationBackgroundView`의 실제 background Image RectTransform을 명시적으로 사용한다.

- shake parent 전체를 zoom하지 않는다.
- sprite/material/shade identity는 그대로 유지한다.
- 확대 pose는 Approach task가 아니라 해당 Battle presentation lifetime 동안 유지한다.
- `Set(sprite)`나 HUD refresh가 scale을 1로 되돌리지 않도록 한다.
- 다음 전투에서 1.12를 새 baseline으로 잡아 누적 확대하지 않는다.
- 정상 승리/패배/취소/fault/NewRun/restore/dispose에서 복구한다.
- 정상 종료에서는 black 상태에서 복구해 화면에 축소 순간이 노출되지 않게 한다.
- 늦은 Dispose가 새 Battle의 background pose를 덮지 않도록 session/lease identity를 사용한다.

### 6.4 UI·Enemy HUD visibility

frame scale만으로 모든 HUD/tooltip이 숨겨진다고 가정하지 않는다.

- Battle frame/content, player HUD, counters, rune rail, spell panel, controls, targeting, enemy HUD/status/intent/tooltip을 실제 inventory한다.
- 필요한 기존 visibility suppression을 좁게 사용한다.
- presenter/layout/logical object는 살아 있어야 한다.
- 숨김 상태에서도 snapshot/locale/follower position을 최신으로 유지한다.
- frame open 단계에서는 frame만 보이고, ContentReady에서 최신 조건부 content와 enemy HUD를 공개한다.
- broad `SetActive(false)`나 `Canvas.enabled=false`가 lifecycle을 중단하는지 확인한다.
- tooltip은 이전 pointer 위치 때문에 공개 직후 즉시 재생성되지 않게 기존 interaction guard를 따른다.

---

## 7. Entry / Restore / Headless 정책

| 경로 | 접근 연출 |
|---|---|
| 일반 / 위험 / 보스 / Event Battle의 새 전투 | 동일 approach 1회 |
| Content QA/Lab에서 production interactive entry를 사용하는 새 전투 | 동일 approach 1회 |
| retry로 새 Battle scope 생성 | 새 presentation session으로 1회 |
| 이미 시작된 Battle checkpoint restore | 최종 background/actor pose로 바로 복원. 새 encounter approach와 Battle Start 재실행 금지 |
| headless/replay/domain test | graphical wait 없이 기존 경로 유지 |
| locale/수치 refresh, Inspect/Modal open-close | approach 재생 없음 |
| 접근 중 resize/locale | clock 유지, 필요한 최종 layout만 갱신 |
| hide/dispose/NewRun/새 restore | stale callback 무시, 자기 pose/visibility/input만 정리 |
| prepare/approach/opening fault | 영구 black/hidden/input-lock 없이 기존 recovery 경계로 복구 |

같은 Encounter ID라는 이유만으로 동일 presentation session이라고 판단하지 않는다. 새 scope의 같은 Encounter는 새 진입이며, 동일 scope의 UI refresh는 새 진입이 아니다.

---

## 8. Visual Work Contract

```text
TargetScreen:
  Production Battle entry / exit presentation

TaskDefaultVisualStrategy:
  PolishExisting

Preserve:
  - existing gameplay/domain flow
  - Battle/Run lifetime
  - enemy formation/logical/hit roots
  - current UI layout
  - fonts / sprites / materials
  - reward / RNG / restore state
  - existing screen transition / input / frame-open behavior

AllowedReuse:
  - existing screen transition/input owner
  - Battle entry presenter
  - RunLocationBackgroundView background image
  - BattleActorMotionRig visual roots

InspectOnly:
  - legacy builders
  - retired visual fixtures
  - old generated alternatives

DeleteAfterMigration:
  - task-owned temporary migration/debug tools only

New Camera / Canvas / EventSystem:
  none
```

코드 변경만으로 구현 가능하면 serialized asset은 변경하지 않는다.

serialized reference가 꼭 필요하면 저장 전 정확한 asset/component/field를 선언하고, 해당 reference 추가 외 hierarchy/geometry/font/material/sprite 변경이 없는지 비교한다.

Unity asset 수정은 Editor API를 통해 수행하고 YAML / `.meta`를 수동 편집하지 않는다.

---

## 9. Localization

`Localization impact: Yes` — 문구 변경이 아니라 visibility / layout / reveal timing에 영향이 있다.

- 기존 KO/EN/Pseudo provider, typed keys, formatted strings를 유지한다.
- 새 player-facing 문구는 기본적으로 추가하지 않는다.
- locale rebind가 approach/frame clock을 재시작하거나 UI를 조기 공개하지 않아야 한다.
- 기존 font/material/inline icon 설정을 변경하지 않는다.
- 1920×1080 / 1280×720의 실제 production CanvasScaler에서 KO/EN/Pseudo를 확인한다.
- 기존 프로젝트의 unrelated localization debt와 이번 변경의 회귀를 구분한다.

---

## 10. 구현·검증 순서

### A. 기준선 확보

- 정상 Battle 진입의 cover/uncover, background/actor pose, UI visibility와 first-ready 시점을 기록한다.
- 기존 input fence와 Battle entry call order를 확인한다.

### B. finite approach 구현

- black 상태 준비
- uncover
- approach
- shared frame open
- content ready

순서로 연결한다.

### C. lifecycle / restore / error 통합 검증

- normal / danger / boss / EventBattle
- retry
- cancel / NewRun
- checkpoint restore
- headless
- locale / resize
- 연속 Battle

을 확인한다.

### D. 실제 frame / regression 검증

접근 시작, 중간, 끝, frame open, content ready, 종료 black, 다음 화면까지 연속 상태를 확인한다.

임시 migration/debug tool을 만들었다면 production reference가 남지 않는지 확인 후 제거한다.

---

## 11. Acceptance Matrix

| ID | 필수 검증 |
|---|---|
| A01 | baseline/user overlay와 좁은 write set을 확인 |
| A02 | cover → uncover → approach → frame-open 순서 |
| A03 | uncover 전체에서 UI/Enemy HUD 조기 노출 0 |
| A04 | approach duration/curve/start/end, unscaled time, 누적 scale 없음 |
| A05 | background 1.12, actor final authored pose, center/foot 기준 유지 |
| A06 | frame-only open 후 조건부 content와 enemy HUD 공개 |
| A07 | `Build → Entry → Start` 및 초기 Battle cue 순서 보존 |
| A08 | transition 동안 입력 차단, release/neutral 후 Draw/Pass/Inspect 정상 |
| A09 | zoom + 기존 shake + actor idle/action/hit/death/reflow 동시 정상 |
| A10 | target / hover / HUD follower / FX anchor가 실제 actor 위치와 일치 |
| A11 | Battle 종료까지 확대 pose 유지, black cleanup에서 복원, 누적 없음 |
| A12 | cancel/NewRun/dispose/fault에서 black·hidden·input state 누수 없음 |
| A13 | 모든 새 Battle entry에 적용되고 same-session refresh에서는 재생 안 됨 |
| A14 | checkpoint restore는 final pose로 복원하며 gameplay/RNG/Start 재실행 없음 |
| A15 | headless/replay가 graphics dependency 없이 기존대로 동작 |
| A16 | KO/EN/Pseudo × 두 해상도, 단일/다수/대형/부유 적 대표 화면 검증 |
| A17 | 기존 screen transition / popup / Event / settlement 회귀 |
| A18 | known-bad 상태 하나 이상을 독립 검사가 실제로 검출 |
| A19 | 승인 범위 외 serialized/data/copy 변경 0 |
| A20 | 요구사항 → diff → production path → evidence 역대조 |

### Known-bad 예시

- Enemy HUD를 approach 전에 강제로 표시.
- Approach를 frame-open 이후에 실행.
- 매 entry마다 현재 scale에 다시 1.12를 곱함.
- actor logical root 자체를 확대해 target/hit area가 움직임.

이 중 최소 하나는 검사에서 실제로 실패해야 한다.

---

## 12. 최종 보고

최종 보고에는 다음을 포함한다.

1. 시작/최종 branch와 full HEAD, task commit IDs, 사용자 overlay 보존 상태.
2. 실제 적용된 duration/ease/background/actor pose.
3. entry / restore / headless 차이와 lifetime ownership.
4. 코드/serialized asset 변경 목록과 DI/lifetime 변경 여부.
5. R01–R09 및 A01–A20의 `Verified / ImplementedUnverified / NotImplemented / ExplicitlyExcluded`.
6. 실행한 EditMode/PlayMode/렌더 검증의 정확한 범위와 실패/미실행.
7. 실제 frame/trace와 재현 방법.
8. physical device와 사용자 visual acceptance는 실행하지 않았다면 `NotRun`으로 분리.

자동 검증을 사람의 시각 승인으로 올리지 않는다.  
기반 코드만 추가하고 production Battle entry에 실제 연결되지 않은 상태를 완료로 보고하지 않는다.
