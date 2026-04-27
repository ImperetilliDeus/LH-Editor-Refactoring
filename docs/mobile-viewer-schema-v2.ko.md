# Mobile Viewer Schema v2 한글판

## 목적

이 문서는 LH 모바일 뷰어용 JSON 스키마 초안을 한글로 정리한 문서다.

목표는 단순히 에디터의 공간을 3D로 다시 그리는 것이 아니라, 아래 흐름까지 안정적으로 지원하는 것이다.

- 방 단위 선택
- 부위 단위 선택
- 하자 유형 필터링
- React Native 앱으로의 코드 전달
- 이후 모바일 뷰어 리팩터링 시의 확장성 확보

기본 방향은 기존 구조를 최대한 살리는 것이다.

- top-level `startPoint`
- top-level `wallData`
- top-level `roomData`

여기에 하자 접수 업무에 필요한 business metadata를 추가한다.

## 핵심 원칙

### 1. 렌더링 데이터와 업무 데이터를 분리한다

모바일 뷰어는 두 종류의 데이터를 동시에 필요로 한다.

- 3D 공간 복원용 geometry 데이터
- 사용자가 무엇을 눌렀는지 판단하고 앱에 어떤 코드를 넘길지 결정하는 business 데이터

둘은 같은 JSON 안에 들어갈 수 있지만, 의미를 섞어서 암묵적으로 처리하면 안 된다.

### 2. ROOM 소속을 명시적으로 유지한다

각 ROOM은 자신에게 속한 벽, 바닥, 천장, 문, 창, 가구를 명시적으로 알아야 한다.

이유는 뷰어가 단순 렌더링 도구가 아니라 다음 기능을 수행하기 때문이다.

- 방에 따라 다른 하자 유형 표시
- `거실 > 벽 > 갈라짐` 같은 경로 생성
- 앱으로 room code, location code를 안정적으로 전달

### 3. 선택 가능한 대상을 1급 객체로 만든다

뷰어는 mesh 자체를 다루는 것이 아니라 “선택 가능한 대상”을 다룬다.

예:

- 거실 북쪽 벽
- 거실 바닥
- 안방 창호 1
- 욕실 천장

이 대상들은 mesh 생성 방식이 바뀌어도 유지되는 안정적인 id와 code를 가져야 한다.

### 4. 에디터 내부 모델은 계속 분리한다

모바일 뷰어용 스키마 때문에 에디터 내부 Room/Wall/Furniture 모델이 오염되면 안 된다.

따라서 export 시점에만 별도 DTO로 변환하는 adapter 구조가 맞다.

## 구형 모바일 뷰어 분석 결과

구형 모바일 뷰어는 단순히 JSON을 읽어서 공간을 복원하는 수준이 아니다.
실제 런타임 계약은 아래 3개 코드 중심이다.

- `mntnSpceCd`: 공간 코드
- `locCd`: 부위 코드
- `reonMtrlCd`: 선택한 하자 코드

실제 흐름은 다음과 같다.

1. React Native가 공간 JSON을 Unity에 전달한다.
2. `Communicator`가 `RoomManager`로 JSON을 넘긴다.
3. `RoomManager`는 `startPoint`, `wallData`, `roomData`로 공간을 복원한다.
4. 사용자가 어떤 부위를 터치하면 `InterestManager`가 `mntnSpceCd`와 `locCd`를 결정한다.
5. `DefectManager`가 `(mntnSpceCd, locCd)` 기준으로 하자 유형을 필터링한다.
6. 최종적으로 `mntnSpceCd`, `locCd`, `reonMtrlCd`, hit point, user point가 RN 앱으로 전달된다.

즉 이 JSON은 scene description이면서 동시에 defect-selection context의 source of truth다.

## 레거시 호환 필수 조건

모바일 뷰어를 바로 리팩터링하지 못한다면, 에디터 export는 구형 뷰어가 기대하는 계약을 유지해야 한다.

필수 항목:

- top-level `startPoint`, `wallData`, `roomData`
- 각 room의 `code` 필드
- room이 소유하는 `walls`, `floor`, `ceil`
- `furnish[*].defects[*]` 구조
- wall/floor/ceil hit에 사용되는 location 성격의 정보

특히 구형 뷰어는 다음 방식에 의존한다.

- room `code`를 `mntnSpceCd`로 사용
- wall/floor/ceil은 runtime hit 결과에서 `locCd`를 유도
- 가구는 `furnish.defects` 안의 `(mntnCd, locCd, mtrlCd)` 튜플을 사용

따라서 당장 export를 바꿀 때는 “이상적인 v2 스키마”보다 “구형 뷰어 호환”을 먼저 맞춰야 한다.

## 권장 top-level shape

```json
{
  "version": 2,
  "unitTypeCode": "55A",
  "startPoint": { "x": 0, "y": 0, "z": 0 },
  "wallData": [],
  "roomData": [],
  "elements": [],
  "defectCatalog": [],
  "exportMeta": {
    "coordinateSystem": "unity-left-handed",
    "unit": "cm",
    "source": "LH Editor Refactoring"
  }
}
```

전환 전략은 단순하다.

- 구형 뷰어가 요구하는 legacy 필드는 유지
- 새 뷰어용 business layer는 병행 추가
- 모바일 뷰어 리팩터링 후 `elements` 중심으로 전환

## 주요 구조

### `wallData`

`wallData`는 벽 geometry 복원용 데이터다.

권장 예시:

```json
{
  "name": "Wall24",
  "id": 24,
  "position": { "x": 33.5, "y": 11.0, "z": 2.0 },
  "angle": { "x": 0.0, "y": 0.0, "z": 0.0 },
  "scale": { "x": 18.5, "y": 22.0, "z": 1.5 },
  "segments": [
    {
      "position": { "x": -0.29, "y": 0.0, "z": 0.0 },
      "angle": { "x": 0.0, "y": 0.0, "z": 0.0 },
      "scale": { "x": 0.42, "y": 1.0, "z": 1.0 },
      "hasInterior": false,
      "door": null,
      "window": null
    }
  ]
}
```

규칙:

- `wallData`는 geometry 복원용이다.
- business 의미를 직접 넣는 주 구조가 아니다.
- `id`는 export 문서 안에서 안정적이어야 한다.
- `segments` 순서는 항상 동일해야 한다.
- 임의 보정값 없이 실제 geometry를 내보내야 한다.

### `roomData`

`roomData`는 room 복원용 데이터이면서, room 소속 관계를 명시하는 핵심 구조다.

권장 예시:

```json
{
  "id": "room_living",
  "name": "거실",
  "code": "900",
  "roomTypeKey": "LIVING",
  "nativeCode": "RM001",
  "position": { "x": 0.0, "y": 0.0, "z": 0.0 },
  "angle": { "x": 0.0, "y": 0.0, "z": 0.0 },
  "scale": { "x": 1.0, "y": 1.0, "z": 1.0 },
  "walls": [24, 25, 27, 28],
  "floor": {},
  "ceil": {},
  "furnish": [],
  "elementIds": [
    "elem_room_living_wall_24",
    "elem_room_living_floor",
    "elem_room_living_ceil"
  ]
}
```

규칙:

- `id`는 Unity object name에 의존하지 않는 안정적인 값이어야 한다.
- `code`는 구형 뷰어의 `mntnSpceCd`와 계속 연결된다.
- `nativeCode`는 RN 앱으로 넘길 공용 room code다.
- `walls`는 room에 속하는 wall geometry root id 목록이다.
- `floor`, `ceil`은 항상 명시적이어야 한다.
- `elementIds`는 room 안의 selectable business target 목록이다.

### `elements`

`elements`는 새로 추가되는 business layer다.
이 구조가 v2의 핵심이다.

각 element는 사용자가 실제로 선택하는 대상을 의미한다.

```json
{
  "id": "elem_room_living_wall_24",
  "roomId": "room_living",
  "type": "WALL",
  "subtype": "MAIN",
  "name": "거실 벽 1",
  "nativeCode": "EL_WALL_001",
  "meshRef": {
    "kind": "wall",
    "wallId": 24,
    "segmentIndex": null
  },
  "defectGroupIds": ["wall_finish", "wall_crack"],
  "metadata": {
    "textureCode": "W001"
  }
}
```

가능한 `type` 예시:

- `WALL`
- `FLOOR`
- `CEIL`
- `DOOR`
- `WINDOW`
- `FURNITURE`
- `FIXTURE`

왜 필요한가:

- geometry id와 business id는 같은 개념이 아니다.
- 나중에 하나의 wall mesh가 여러 selectable target으로 쪼개질 수 있다.
- defect 허용 목록은 geometry보다 business element에 붙는 편이 자연스럽다.

## 구형 뷰어와의 매핑

구형 모바일 뷰어는 현재 business 의미를 상당히 취약한 방식으로 추론한다.

- 가구는 hit position과 room lookup으로 room을 찾음
- 벽/바닥/천장은 `WallSegment.type`에서 `locCd`를 얻음
- 일부 wall hit는 explicit room ownership이 아니라 nearest-room 추론을 씀

v2에서는 이 implicit rule을 제거해야 한다.

- `roomData.code`는 legacy room-space code 유지
- `elements[*].nativeCode`는 미래의 canonical `locCd`
- `elements[*].roomId`는 nearest-room 추론 대체
- 가구는 runtime lookup 대신 explicit mapping 사용

## 구현 단계

### Phase 1. 구형 모바일 뷰어 계약 맞추기

목표:

- 현재 에디터 export를 `Mobile-Viewer-Old`가 안전하게 읽을 수 있게 만든다.

구현 항목:

- `startPoint`, `wallData`, `roomData`를 legacy shape로 export
- nested `transform` 제거
- `position`, `angle`, `scale`를 direct field로 export
- room `code`, owned `walls`, `floor`, `ceil`, `furnish` 유지
- 가구 `defects` 필드 추가
- wall segment ordering 안정화
- floor/ceil scale이 기본 mesh에서도 의미 있게 나가도록 정규화

### Phase 2. 메타데이터 authoring UI 추가

목표:

- 뷰어와 RN 앱이 필요로 하는 code를 에디터에서 직접 작성 가능하게 만든다.

구현 항목:

- room id / room code / room type key / room native code
- floor / ceiling texture code
- wall finish code
- door/window export code
- furniture export code
- legacy 호환이 필요한 furniture defect tuple authoring

### Phase 3. `elements` 병행 export 추가

목표:

- 구형 뷰어를 깨지 않으면서 새 뷰어용 business layer를 준비한다.

구현 항목:

- room-scoped element id 생성
- geometry와 business target 연결
- element별 native code 부여
- roomId 기반 ownership 명시

### Phase 4. `defectCatalog` 연결

목표:

- 하자 필터링 로직을 뷰어 내부 하드코딩에서 분리한다.

구현 항목:

- defect master data 연결
- room type + element type 기준 그룹화
- native code export

### Phase 5. 모바일 뷰어 리팩터링

목표:

- runtime 추론을 제거하고 explicit data를 사용한다.

구현 항목:

- nearest-room lookup 제거
- `WallSegment.type` 의존 제거
- `elements[*].roomId`와 `elements[*].nativeCode` 사용
- 가구도 explicit element payload 사용

### Phase 6. validation / regression fixture 추가

목표:

- schema drift와 누락 데이터를 조기에 막는다.

구현 항목:

- room code 누락 검사
- element mapping 누락 검사
- floor/ceil/wall ownership 누락 검사
- defect reference 누락 검사
- golden JSON fixture
- viewer import smoke test

## 현재 기준 우선순위

당장 해야 할 순서는 아래가 맞다.

1. 구형 모바일 뷰어가 읽는 JSON 계약을 정확히 맞춘다.
2. room code와 texture code 같은 metadata 입력 기능을 붙인다.
3. `elements`를 추가해 새 뷰어 리팩터링 기반을 만든다.
4. `defectCatalog`와 validation을 붙인다.

즉 지금은 v2의 최종 형태를 한 번에 넣는 것보다, “legacy-compatible export + future-ready extension” 방식으로 가는 것이 가장 안전하다.
