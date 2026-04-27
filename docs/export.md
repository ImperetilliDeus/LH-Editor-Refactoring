# Export Guide

- Mobile viewer schema draft: `docs/mobile-viewer-schema-v2.md`

## 목적

LH scene 데이터를 JSON으로 내보내는 경로와 사전 점검 포인트를 정리합니다.

## 진입점

- `Assets/Scripts/Export/LhSceneExporter.cs`
- `Assets/Scripts/Export/LhSceneExportBuilder.cs`

## 전제

- wall 데이터는 `Wall` 및 관련 매니저가 유지합니다.
- room 데이터는 `RoomManager`가 보유한 `Room` 목록을 기준으로 수집합니다.
- room polygon은 현재 `RoomCreate` 및 수동 polygon 편집 결과를 사용합니다.

## export 전 점검

- `RoomManager`가 최신 room polygon을 반영했는지 확인
- top view 표시와 실제 room polygon이 일치하는지 확인
- Undo/Redo 직후의 wall / room / opening 상태를 한 번 더 확인
- DetailEdit에서 방금 바꾼 벽 길이/높이/두께가 즉시 적용되었는지 확인
- floor 시각 위치 정책과 export 데이터의 좌표 체계가 충돌하지 않는지 확인

## 메모

- 현재 문서화된 최근 변경은 주로 편집 UI/시각화 경로입니다.
- export 데이터 스키마 자체가 바뀐 것은 아니므로, 편집 결과가 최신 상태로 반영되는지 점검하는 것이 핵심입니다.
