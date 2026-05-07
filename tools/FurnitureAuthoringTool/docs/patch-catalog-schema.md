# Patch Catalog Schema

`patch-catalog.json`은 `FurnitureAuthoringTool`의 1차 `Patch Build` 결과물입니다.

역할:

- manifest 메타데이터를 빌드 시점 산출물 기준으로 재정리
- 복사된 prefab/thumbnail의 상대 경로 제공
- 이후 Unity batchmode worker 또는 LH Editor 로더가 읽을 중간 산출물 역할

## 루트 구조

```json
{
  "manifestVersion": 1,
  "catalogVersion": "2026.04.30.01",
  "createdAt": "2026-04-30T10:00:00+09:00",
  "builtAt": "2026-04-30T10:15:00+09:00",
  "author": "admin",
  "manifestFile": "manifest.json",
  "items": []
}
```

## Item 구조

```json
{
  "code": "S001",
  "displayName": "소파 1",
  "exportCode": "S001",
  "nativeCode": "",
  "prefabFile": "prefabs/S001.prefab",
  "thumbnailFile": "thumbnails/S001.png",
  "placementOffset": { "x": 0.0, "y": 0.0, "z": 0.0 },
  "defaultEulerAngles": { "x": 0.0, "y": 0.0, "z": 0.0 },
  "boundsSize": { "x": 10.0, "y": 10.0, "z": 10.0 },
  "defects": []
}
```

## 출력 폴더 구조

```text
BuildOutput/
  2026.04.30.01_20260430-101500/
    manifest.json
    patch-catalog.json
    build-report.txt
    prefabs/
      S001.prefab
    thumbnails/
      S001.png
```

## 1차 정책

1. `prefabSourcePath`는 필수이며 실제 파일이 존재해야 합니다.
2. `thumbnailSourcePath`가 비어 있으면 경고를 남기고 계속 진행합니다.
3. `thumbnailSourcePath`가 채워져 있으면 실제 파일이 존재해야 합니다.
4. 복사된 파일명은 item `code` 기준으로 정규화합니다.
