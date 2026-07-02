# Crafting 0.1 Debug Loop

이 문서는 현재 구현된 제작/가공 0.1 디버그 루프 상태를 정리한다.
현재 제작은 정식 UI 기능이 아니라, Workbench 기반 `process_wood_plank` 디버그 루프가 기존 운반/보관 시스템과 연결되는지 검증하기 위한 개발용 경로다.

## 1. 현재 제작 루프 요약

```text
F7로 Workbench에 process_wood_plank CraftJob 생성
-> Wood x2 배달
-> ReadyToCraft
-> Crafting 진행
-> OutputReady
-> ProducedOutputs에 판재 x1 확정
-> 용병이 판재 회수
-> 기존 Haul 입고 루프
-> 보관함 입고
```

현재 확인된 동작:

- F7로 Workbench에 `process_wood_plank` `CraftJob`을 생성한다.
- 용병이 저장소에서 `Wood x2`를 꺼내 CraftJob에 배달한다.
- 입력 재료가 모두 채워지면 `ReadyToCraft`가 된다.
- 용병이 Workbench에서 `Crafting` 작업을 진행한다.
- 작업량이 채워지면 `OutputReady`가 된다.
- `ProducedOutputs`에 `Plank x1`, 즉 판재 x1이 한 번만 확정된다.
- 용병이 판재를 회수해 개인 Inventory에 넣는다.
- 회수한 판재는 기존 `Haul` 입고 루프를 통해 보관함/저장소에 입고된다.
- 저장소가 없거나 꽉 차면 판재는 용병 Inventory에 보존된다.
- `Completed` / `Cancelled` `CraftJob`은 정리되어 active job으로 누적되지 않는다.

## 2. 사용 방법

1. `MainWorld.EnableCraftingDebugHotkeys = true`로 켠다.
2. Workbench를 건설한다.
3. Workbench 위에 마우스를 올리고 F7을 누른다.
4. active debug `CraftJob`을 취소하려면 F8을 누른다.

주의:

- `EnableCraftingDebugHotkeys` 기본값은 `false`다.
- `MainWorld.tscn`에서도 기본적으로 `true`로 켜지지 않는다.
- 이 기능은 개발용 진단 hotkey이며 정식 제작 UI가 아니다.

## 3. 핵심 파일과 역할

### CraftRecipeDatabase 계열

파일:

- `scripts/world/definitions/CraftRecipeCategory.cs`
- `scripts/world/definitions/CraftRecipeEntry.cs`
- `scripts/world/definitions/CraftRecipeDatabase.cs`

역할:

- 제작 레시피 데이터를 정의한다.
- 현재 활성/비활성 레시피 목록을 제공한다.
- 창고 조회, 자원 소비, AI 행동, 결과물 입고는 직접 수행하지 않는다.

### CraftJob / CraftingManager 계열

파일:

- `scripts/world/crafting/CraftJobState.cs`
- `scripts/world/crafting/CraftJob.cs`
- `scripts/world/crafting/CraftingManager.cs`

역할:

- `CraftJob`은 입력 재료, 작업 진행도, 출력 결과, 예약 상태를 가진다.
- `CraftingManager`는 CraftJob 생성/취소/조회와 inactive job 정리를 담당한다.
- `Completed` / `Cancelled` job은 active job 조회에서 제거된다.

### MercenaryLifeAI

파일:

- `scripts/units/MercenaryLifeAI.cs`

역할:

- CraftJob 재료 배달을 선택한다.
- ReadyToCraft job의 작업 진행을 수행한다.
- OutputReady 결과물을 회수한다.
- 회수한 자원을 기존 Haul 입고 루프에 넘긴다.

### MainWorldController

파일:

- `scripts/world/MainWorldController.cs`

역할:

- `EnableCraftingDebugHotkeys`가 켜진 경우 F7/F8 debug 입력을 처리한다.
- F7은 마우스 위치의 Workbench에 `process_wood_plank` job을 생성한다.
- F8은 active debug CraftJob을 취소한다.

### BaseOverlayLayer

파일:

- `scripts/effects/BaseOverlayLayer.cs`

역할:

- overlay 진단에 craft jobs count를 표시한다.
- 첫 active CraftJob의 recipe/state/facility/material/progress/output 정보를 표시한다.

## 4. 현재 레시피 상태

활성 레시피:

| RecipeId | 입력 | 출력 | 시설 | RequiredWork |
| --- | --- | --- | --- | ---: |
| `process_wood_plank` | Wood x2 | Plank x1 | Workbench | 4.0 |

비활성 레시피:

- `process_stone_brick`
- `smelt_metal_ingot`
- `cook_simple_meal`

비활성 레시피는 데이터만 있으며 현재 debug loop에서 실제 생산 대상으로 쓰지 않는다.

## 5. CraftJob 상태 흐름

```text
WaitingForMaterials
-> ReadyToCraft
-> Crafting
-> OutputReady
-> Completed
```

상태 의미:

- `WaitingForMaterials`: 입력 재료가 아직 부족하다.
- `ReadyToCraft`: 입력 재료가 모두 투입되었다.
- `Crafting`: 용병이 Workbench에서 작업 시간을 진행 중이다.
- `OutputReady`: 결과물이 `ProducedOutputs`에 확정되어 회수 대기 중이다.
- `Completed`: 결과물이 회수되어 job이 완료되었다.
- `Cancelled`: debug cancel 또는 실패 정리로 취소되었다.

## 6. 자원 흐름

입력 흐름:

```text
저장소 Wood
-> 용병 Inventory
-> CraftJob InputsDelivered
```

출력 흐름:

```text
CraftJob ProducedOutputs
-> 용병 Inventory
-> 기존 Haul 입고 루프
-> 저장소
```

중요 정책:

- 입력 Wood는 CraftJob에 투입되는 시점에 용병 Inventory에서 제거된다.
- 출력 Plank는 `ProducedOutputs`에 먼저 봉인된다.
- 용병이 output을 회수한 뒤 개인 Inventory에 들어간다.
- 이후 별도 제작 전용 입고 시스템이 아니라 기존 일반 Haul/Deposit 입고 루프를 사용한다.
- 저장 실패 시 결과물은 삭제되지 않고 용병 Inventory에 남는다.

## 7. 아직 없는 것

현재 0.1 debug loop에는 아래 기능이 없다.

- 정식 제작 UI
- 제작대별 작업 목록 UI
- 레시피 큐
- 자동 반복 제작
- 출력 ResourcePile 드롭
- 저장소 실패 알림/HUD
- Workbench 외 제작대 실사용

추가로 아직 구현하지 않은 것:

- 제작대별 품질/속도 보너스
- 작업자 스킬 기반 제작 속도/품질 보정
- SimpleMeal 식사 연결
- Medicine 치료 연결
- JSON/모드형 레시피 로딩

## 8. 수동 테스트 체크리스트

- 보관함 없는 상태에서 제작 후 판재가 Inventory에 남는지 확인한다.
- 보관함 생성 후 판재가 자동 입고되는지 확인한다.
- 보관함 있는 상태에서 다시 제작하면 바로 입고되는지 확인한다.
- F7/F8 반복 후 job 누적이 없는지 확인한다.
- 기존 ResourcePile 운반이 정상인지 확인한다.
- 기존 직접 건설이 정상인지 확인한다.
- logistics warning이 없는지 확인한다.

권장 테스트 순서:

1. `EnableCraftingDebugHotkeys`를 `true`로 켠다.
2. Workbench를 건설한다.
3. 저장소가 없는 상태라면 F7 제작 완료 후 판재가 용병 Inventory에 남는지 본다.
4. 저장소를 만든 뒤 판재가 기존 Haul 입고 루프로 들어가는지 본다.
5. 저장소가 있는 상태에서 다시 F7 제작을 수행한다.
6. 판재가 회수 후 곧바로 입고되는지 본다.
7. F8로 active job 취소가 되는지 확인한다.
8. F7/F8을 반복해 `craft jobs` 진단이 누적되지 않는지 확인한다.
9. 기존 ResourcePile 운반과 직접 건설이 계속 정상인지 확인한다.
10. Godot output에 logistics warning이 없는지 확인한다.

## 9. 주의 사항

- 자동 제작을 먼저 넣지 말 것.
- 제작 결과를 global wallet에 넣지 말 것.
- 저장 실패 시 결과물을 삭제하지 말 것.
- HUD 임시 패치는 용병 탭/HUD 개편 전까지 보류할 것.
- BaseBuildManager에 제작 로직을 계속 추가하지 말 것.
- CraftingManager와 CraftJob은 제작 데이터/작업 상태 중심으로 유지할 것.
- 저장/입고는 기존 창고/Haul API를 재사용할 것.
- OutputReady 결과물이 남은 상태의 제작대 철거 처리는 별도 설계 후 구현할 것.
