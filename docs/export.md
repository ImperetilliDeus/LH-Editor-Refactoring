# Export Guide

## 목적

LH 씬 데이터를 JSON으로 내보내는 기능의 현재 기준을 정리한 문서입니다.

## 진입점

- `Assets/Scripts/Export/LhSceneExporter.cs`

## 전제

- 벽 데이터는 `Wall`과 관련 매니저에서 유지합니다.
- room 데이터는 `RoomManager`가 보관하는 `Room` 목록을 기준으로 수집합니다.
- room polygon은 현재 `RoomCreate` 흐름으로 작성된 데이터를 사용합니다.

## 점검 항목

- export 전 `RoomManager`에 최신 room이 반영돼 있는지 확인
- top view 표시와 실제 room polygon이 일치하는지 확인
- Undo/Redo 직후라면 상태를 한 번 더 확인

## 메모

- 예전 virtual boundary 기반 room 분할 실험은 현재 메인 export 경로가 아닙니다.
- 관련 설계 문서는 참고용 보관 문서로만 유지합니다.
