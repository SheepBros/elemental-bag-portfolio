# Relic Acquisition Transaction Recovery

## 1. 작업 목적

사용자 플레이에서 다음 문제가 보고되었다.

> 위험 전투 보상으로 유물을 획득하려 할 때 버그 발생.

현재 보고만으로는 정확한 유물, 예외, 실패 시점, 재현 단계가 충분하지 않다.

따라서 원인을 미리 가정하지 말고 다음 순서로 진행한다.

```text
실제 production 경로에서 재현
→ 최초 실패 지점과 transaction 상태 확인
→ 기존 수정으로 이미 해결됐는지 확인
→ 재현된 원인만 최소 수정
→ 취소 / stale state / 중복 입력 / async lifetime 검증
→ 실제 유물 획득과 후속 진행 회귀 검증
```

어떤 결함 하나를 고쳤다는 이유만으로 원래 증상을 해결했다고 처리하지 않는다.

이미 선행 변경으로 같은 증상이 해결되어 있다면 재구현하지 않고 실제 경로의 검증 증거만 연결한다.

---

## 2. 시작 기준과 보존 계약

작업 시작 시 다음을 확인한다.

```bash
pwd
git branch --show-current
git rev-parse HEAD
git status --short
git worktree list
```

기록할 항목:

- `BaselineCommit`
- `UserOwnedOverlay`
- `ApprovedWriteSet`
- 현재 실행 중인 Unity / worktree

### Git 안전

- 요청 시작 dirty/untracked는 사용자 소유 변경으로 취급한다.
- `git add .`로 무관한 변경을 함께 commit하지 않는다.
- 다른 worktree나 사용자가 실행 중인 Unity Editor를 임의로 변경하거나 종료하지 않는다.
- push / merge / rebase / reset / clean / 강제 stash를 하지 않는다.
- 동일 기능의 기존 작업 branch가 있으면 상태를 확인하고 중복 구현하지 않는다.

### Required Reading

필요한 범위만 최신 로컬에서 읽는다.

- `AGENTS.md`, `docs/README.md`
- 유물 획득 / 제물 / 흡수 / 정화 관련 design source
- `player_information_ui_source.md`
- `confirmation_dialog_ui_source.md`
- `post_battle_ui_source.md`
- `information_panel_ui_source.md`
- `ui_architecture.md`
- `ui_visual_implementation_policy.md`
- `localization_policy.md`
- 관련 focused verification과 tests

문서 전체를 재귀적으로 읽는 작업으로 확대하지 않는다.

### 반드시 보존할 것

- 기존 유물 후보 생성과 frozen opportunity identity.
- 룬 제물 / 유물 흡수 / 정화의 기존 gameplay 규칙.
- 실제 룬 runtime instance identity.
- 유물 고유 효과 / 추가 불안정 / 즉시 선택 등 기존 transaction 의미.
- PlayerInformation / Confirmation modal의 현재 input/lifetime 계약.
- 자식 정보 modal dismiss와 부모 보상 진행의 분리.
- 입력당 한 번만 처리하는 existing fence와 fresh press/release/neutral 정책.
- 기존 데이터, RNG, 희귀도, 획득 비용, 보상 수치.

### 범위 밖

- 유물 보상 UI 전체 리디자인.
- 유물 등급 표시 정책 변경.
- 거래 설명 패널·아이콘·새 hover 정보 추가.
- 룬/스펠/유물 효과나 수치 변경.
- 새로운 전역 input framework.
- 새로운 거래 service / save format / recovery framework.
- 무관한 성장·Stage·이벤트 규칙 수정.

---

## 3. 기존 코드 경로에서 확인할 지점

실제 로컬 코드에서 아래 경계를 우선 추적한다.

| 경계 | 확인할 역할 | 조사 포인트 |
|---|---|---|
| 유물 획득 요청 | `RelicRewardSelectionScreenView` 등 | processing 시작/종료와 callback 오류 시 상태 |
| 제물 화면 진입 | `RunScreenCoordinator`의 relic acquisition path | option 선택과 실제 거래 commit을 혼동하지 않는지 |
| 룬 선택/확인 | `ConfirmRelicRuneAsync` 또는 동등 경계 | runtime instance ID, revision, cancel/re-entry |
| 실제 지급 | `CompleteRelicAcquisition` → application/service | 실제 commit이 어느 시점에 발생하는지 |
| 룬 정체성 | instance ID → 현재 bag/index 재검증 | 동일 Rune ID 여러 장일 때 정확한 instance 소비 |
| confirmation | offer / relic / option / bag revision 검증 | 오래된 동의를 새 transaction에 적용하지 않는지 |
| 취소/예외 | catch/finally/Refresh/Resume selection | command 전 실패와 commit 후 display 실패 구분 |
| action availability | PlayerInformation row/model | 다른 mode의 flag가 sacrifice/purify action을 막지 않는지 |
| common input | modal owner / fresh-input fence | 중복 click/Submit/held input 처리 |

이 표는 수정 대상 목록이 아니다. 실제 결함이 없는 코드까지 예방적으로 갈아엎지 않는다.

---

## 4. 요구사항과 수용 기준

| ID | 완료 계약 | 금지 |
|---|---|---|
| A01 | 위험전 승리→정산/학습→룬 보상→유물 후보→획득→제물→확인→지급→후속까지 실제 production path로 연결 | service 직접 호출만으로 UI 완료 판정 |
| A02 | 취소하면 transaction 전 상태와 동일한 frozen opportunity로 돌아감 | 후보 재추첨, 기회 소모, 비용 선적용 |
| A03 | 사용자가 선택한 정확한 Rune runtime instance 하나만 소비 | 같은 Rune ID 전체 삭제, 잘못된 index 소비 |
| A04 | 불가 대상/조건 부족은 정상 차단하지만 취소·다른 유효 조작은 가능 | validator 제거, 영구 blocker |
| A05 | stale option / instance / revision / opportunity 변경은 mutation 없이 거절하고 현재 상태를 다시 표시 | 과거 확인으로 변경된 transaction 실행 |
| A06 | command commit 전 실패와 commit 후 display 실패를 구분해 복구 | 성공 transaction을 다시 실행하여 중복 획득/재희생 |
| A07 | double click / Submit / held input에도 transaction과 후속 진행은 각각 exactly-once | 화면 건너뛰기, 취소가 확인으로 전달 |
| A08 | 추가 즉시 선택이 필요한 relic은 기존 추가 선택 계약을 끝까지 수행 | 추가 선택 생략, 임의 target, 무료 처리 |
| A09 | Acquire 변경으로 Absorb / Purify / Boss / Event reward flow가 깨지지 않음 | 세 선택을 합산하거나 연속 지급 |
| A10 | child modal / confirmation / parent reward window의 input·close ownership을 분리 | child close가 parent close나 reward continuation 실행 |

---

## 5. 재현과 오류 처리 전략

### 5.1 기준선 재현

실제 위험전의 frozen reward opportunity에서 **Acquire** 경로를 사용해 재현한다.

기록할 것:

```text
Run / seed / encounter
offer identity
selected relic
selected rune runtime instance
bag revision
current reward phase
current screen/modal stack
first exception/assert
processing / input owner / focus state
```

기존 테스트나 report가 같은 증상을 기록하고 있다면 연결하되, 과거 기록을 현재 재현 결과로 대신하지 않는다.

### 5.2 Transaction 이전

유물 후보 클릭과 룬 선택은 preview/selection 상태다.

- hover / info / locale change는 유물이나 룬을 실제 소비하지 않는다.
- 현재 자격 조건은 기존 Source/provider/command contract를 따른다.
- confirmation 취소 후 동일한 opportunity와 유효한 선택 상태로 복구한다.
- processing 중에는 중복 action을 막되, 취소/재선택 가능한 상태로 돌아오면 정상 입력을 복구한다.

### 5.3 Commit 경계

실제 검증과 원자적 지급은 existing application/service가 소유한다.

confirmation 이후에도 commit 직전에 다음을 재검증한다.

```text
offer identity
selected relic option
selected rune runtime instance
bag revision
pending reward opportunity
additional selection requirement
```

#### Commit 이전 거절/예외

- Rune / Relic / HP / resonance / opportunity를 변경하지 않는다.
- 현재 authoritative state를 다시 표시한다.
- 사용자가 취소하거나 다시 선택할 수 있어야 한다.

#### Commit 이후 display 예외

- 이미 성공한 transaction은 유지한다.
- 화면만 authoritative state에서 복구한다.
- commit command를 재호출하지 않는다.
- 같은 reward opportunity를 다시 Acquire/Absorb/Purify할 수 없게 한다.

persistent display failure에서도 이미 처리한 제물 선택을 다시 보여 사용자가 재소비하게 하지 않는다.

### 5.4 Runtime identity

Rune ID와 Rune instance identity를 구분한다.

예:

```text
Rune ID = 10001
Runtime Instance = #42
Runtime Instance = #77
```

같은 Rune ID가 여러 장 있어도 사용자가 선택한 runtime instance만 제거해야 한다.

선택 시점의 index를 그대로 신뢰하지 말고 commit 시점에 stable runtime identity로 현재 상태를 다시 검증한다.

### 5.5 Async / stale callback

confirmation 대기나 추가 선택 중에 다음이 발생할 수 있다.

- Cancel
- Parent Hide
- New Run
- Scene/Scope dispose
- Opportunity revision change

오래된 async completion/finally가 새 Run이나 새 reward screen의 `_isBusy`, processing, selection을 덮어쓰지 않게 기존 generation/token/identity 경계를 사용한다.

새 범용 recovery framework를 만들지 않는다.

---

## 6. Input과 Modal Ownership

- relic card/row click과 Submit이 같은 frame에 중복 실행되지 않게 한다.
- held input이 confirmation open과 동시에 confirm으로 재사용되지 않게 한다.
- child information popup을 닫는 입력이 parent reward window를 닫거나 transaction을 승인하지 않는다.
- confirmation cancel은 acquisition cancel과 구분한다.
- transaction 완료 입력과 다음 화면 진행 입력은 별도 fresh input이어야 한다.
- 상위 modal이 열려 있는 동안 아래 화면 입력은 기존 방식으로 차단한다.
- pointer / keyboard / controller path는 같은 semantic command로 수렴하되 중복 실행하지 않는다.

---

## 7. Visual / Localization Work Contract

```text
TaskDefaultVisualStrategy:
  PolishExisting

Target:
  Existing relic reward / rune sacrifice / confirmation / player information flow

Preserve:
  - current layout
  - font / color / icon / material
  - scroll areas
  - hierarchy
  - existing popup/frame presentation
  - current information-panel behavior
  - gameplay/data rules

InspectOnly:
  - broad UI builders
  - whole-project localization seed/apply tools
  - old migration tools

DeleteAfterMigration:
  - task-owned temporary diagnostic/migration tools only
```

기본 production serialized write set은 빈 집합으로 시작한다.

serialized binding 결함이 실제 원인일 때만 정확한 asset/property를 write set에 추가한다. prefab 전체 rebuild, broad reserialize, YAML/.meta 수동 편집을 하지 않는다.

### Localization

`Localization impact: Yes` — 오류/취소/재선택/상태 갱신 화면에 영향이 있다.

- 기존 player-facing copy와 typed localization key를 우선 재사용한다.
- raw exception / enum / internal ID를 사용자 메시지로 노출하지 않는다.
- 새 문구가 정말 필요할 때만 좁은 typed key + KO/EN을 추가한다.
- unrelated localization table을 재생성하지 않는다.
- KO/EN/Pseudo × 1920×1080 / 1280×720에서 sacrifice/confirmation/error/recovery를 확인한다.

---

## 8. 검증 시나리오

기존 유효 테스트와 production fixture를 우선 재사용하고 빠진 경계만 추가한다.

| ID | 실제 확인할 것 |
|---|---|
| T01 | 원래 플레이 증상의 최초 실패 지점 또는 이미 해결된 증거를 식별 |
| T02 | 위험전 승리→정산→학습→룬 보상→유물 Acquire→제물→확인→지급→후속 전체 경로 |
| T03 | 동일 Rune ID 여러 instance에서 선택한 한 instance만 소비 |
| T04 | 제물 선택 취소 / confirmation 취소 / 재선택 후 같은 frozen opportunity 유지 |
| T05 | 대상 부족 / invalid rune / 이미 획득 불가 relic / stale opportunity의 정상 차단과 입력 복구 |
| T06 | confirmation 중 runtime instance / revision / option 변경 시 stale rejection 또는 재확인 |
| T07 | commit 전 exception/rejection 후 정상 재시도 가능 |
| T08 | commit 직후 display exception: transaction exactly-once, 중복 relic/rune mutation 없음 |
| T09 | 추가 즉시 선택이 필요한 실제 relic의 기존 continuation 유지 |
| T10 | click+Submit / double click / held input / pointer-down→modal→pointer-up 중복 방지 |
| T11 | Absorb / Purify 정상·취소·불가 경로 회귀 |
| T12 | Boss / Event 등 실제 공유 reward opportunity의 acquire flow 회귀 |
| T13 | child information modal / confirmation / parent close ownership 회귀 |
| T14 | locale change / resolution change / cancel-reentry에서 같은 valid transaction state 유지 |
| T15 | fresh checkpoint restore에서 pending reward와 완료된 reward가 중복 소비되지 않음 |
| T16 | NewRun / Title / scope disposal 중 늦게 도착한 callback이 새 session을 수정하지 않음 |
| T17 | negative fixture 하나 이상이 실제 결함을 검출 |

### Negative fixture 예시

- selected runtime instance 대신 같은 Rune ID의 첫 번째 instance를 삭제하는 구현.
- commit 후 display failure에서 commit command를 다시 호출.
- stale bag revision을 무시하고 과거 confirmation으로 거래 실행.
- held Submit으로 reward acquire와 confirmation을 같은 입력에서 연속 실행.

expected를 production 구현의 반환값에서 그대로 생성하지 않는다.

---

## 9. 완료 판정과 문서

실제로 변경된 계약만 owning Source/Current에 반영한다.

검증 기록에는 다음을 남긴다.

```text
Observed symptom
→ Reproduced cause
→ Actual fix
→ Runtime / test evidence
```

재현하지 못한 항목은 해결 완료라고 쓰지 않는다.

각 요구사항은 다음 상태 중 하나로 기록한다.

```text
Verified
ImplementedUnverified
NotImplemented
ExplicitlyExcluded
```

자동 테스트 통과, 실제 기기 입력, 사용자 visual acceptance를 서로 다른 증거로 취급한다.

---

## 10. 최종 보고

최종 보고에는 다음을 포함한다.

1. 시작/최종 branch와 full HEAD, task commit IDs, 사용자 overlay 보존 상태.
2. 최초 증상과 실제 재현 경로.
3. 확정된 원인과 수정 경계. 가설/미재현은 별도 표시.
4. transaction commit 이전/이후 오류 처리 방식.
5. runtime Rune identity와 stale revision 처리.
6. input/modal lifetime과 exactly-once 증거.
7. Acquire / Cancel / Absorb / Purify / additional selection / restart-dispose 회귀 결과.
8. code/serialized asset/localization 변경 목록.
9. T01–T17과 A01–A10의 상태.
10. 실행한 EditMode/PlayMode/렌더/실물 입력의 정확한 범위와 미실행 항목.

‘예외가 더 이상 발생하지 않는다’만으로 완료 처리하지 않는다.  
**유물 transaction이 정확히 한 번만 적용되고, 실패·취소·stale 상태에서도 데이터와 UI가 일관되며, 실제 production reward flow가 끝까지 정상 진행되는 상태를 증거로 남긴다.**
