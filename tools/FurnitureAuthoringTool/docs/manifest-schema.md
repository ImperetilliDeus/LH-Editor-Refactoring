# Manifest Schema

## 목적

`FurnitureAuthoringTool`이 저장하는 `manifest.json`은 아래 세 시스템 사이의 계약입니다.

- 저작 도구 UI
- Unity Build Worker
- LH Editor 런타임 로더

`position`, `angle`, `scale`은 런타임 배치 결과이므로 manifest에 포함하지 않습니다.
manifest는 `가구 정의(master data)`만 다룹니다.

## 루트 구조

```json
{
  "manifestVersion": 1,
  "catalogVersion": "2026.04.30.01",
  "createdAt": "2026-04-30T10:00:00+09:00",
  "author": "admin",
  "items": []
}
```

## 루트 필드

| Field | Type | Required | Rule |
| --- | --- | --- | --- |
| `manifestVersion` | `int` | Yes | 현재 값은 `1` |
| `catalogVersion` | `string` | Yes | 사람이 읽을 수 있는 카탈로그 버전 문자열 |
| `createdAt` | `string` | Yes | ISO-8601 datetime offset |
| `author` | `string` | Yes | 작성자 또는 배포 담당자 |
| `items` | `array` | Yes | 가구 정의 목록 |

## Item 구조

```json
{
  "code": "S001",
  "displayName": "소파 1",
  "exportCode": "S001",
  "nativeCode": "",
  "prefabSourcePath": "D:\\Furniture\\S001.prefab",
  "thumbnailSourcePath": "D:\\Furniture\\S001.png",
  "placementOffset": { "x": 0.0, "y": 0.0, "z": 0.0 },
  "defaultEulerAngles": { "x": 0.0, "y": 0.0, "z": 0.0 },
  "boundsSize": { "x": 10.0, "y": 10.0, "z": 10.0 },
  "defects": []
}
```

## Item 필드

| Field | Type | Required | Rule |
| --- | --- | --- | --- |
| `code` | `string` | Yes | 가구 preset 식별자. 전체 item에서 유일해야 함 |
| `displayName` | `string` | Yes | 저작 도구와 메뉴에서 표시할 이름 |
| `exportCode` | `string` | Yes | Scene export에 기록할 코드. 기본값은 `code`와 동일 |
| `nativeCode` | `string` | Yes | 외부 시스템 연동용 코드. 없으면 빈 문자열 |
| `prefabSourcePath` | `string` | Yes | 원본 prefab 파일 경로 |
| `thumbnailSourcePath` | `string` | No | 원본 thumbnail 파일 경로. 없으면 Unity 워커가 생성 가능 |
| `placementOffset` | `object` | Yes | 초기 배치 오프셋 |
| `defaultEulerAngles` | `object` | Yes | 초기 회전값 |
| `boundsSize` | `object` | Yes | 배치 검증용 기본 크기 |
| `defects` | `array` | Yes | 유지보수/하자 코드 목록 |

## Vector3 구조

```json
{
  "x": 0.0,
  "y": 0.0,
  "z": 0.0
}
```

| Field | Type | Required | Rule |
| --- | --- | --- | --- |
| `x` | `number` | Yes | 소수 허용 |
| `y` | `number` | Yes | 소수 허용 |
| `z` | `number` | Yes | 소수 허용 |

## Defect 구조

```json
{
  "mntnCd": "901",
  "locCd": "2",
  "mtrlCd": "080"
}
```

| Field | Type | Required | Rule |
| --- | --- | --- | --- |
| `mntnCd` | `string` | Yes | 공백 불가 |
| `locCd` | `string` | Yes | 공백 불가 |
| `mtrlCd` | `string` | Yes | 공백 불가 |

## 1차 검증 규칙

1. `code`는 필수이며 대소문자 구분 없이 유일해야 합니다.
2. `displayName`, `exportCode`, `prefabSourcePath`는 비어 있으면 안 됩니다.
3. `thumbnailSourcePath`는 비어 있을 수 있습니다.
4. `defects`는 빈 배열을 허용합니다.
5. `defects` 각 행의 `mntnCd`, `locCd`, `mtrlCd`는 공백이면 안 됩니다.
6. `exportCode`가 비어 있으면 저장 전에 `code`로 보정하는 방향을 기본 정책으로 잡습니다.
7. `nativeCode`가 없으면 빈 문자열로 저장합니다.

## 런타임 관계

- `code`
  - Unity Editor 내 furnish preset 식별자
  - scene export JSON의 `furnish[].code`로 기록됨
- `defects`
  - 가구 정의에 종속
  - 배치된 인스턴스는 가구 정의의 `defects`를 복사해서 export에 포함
- `position`, `angle`, `scale`
  - 배치 시점에 결정
  - manifest에 없음
