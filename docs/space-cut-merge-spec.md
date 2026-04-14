# Space Cut / Merge Spec

이 문서는 보관용 설계 메모입니다.

현재 메인 구현은 `RoomCreate` 기반 직접 room 작성 흐름이며, 이 문서의 virtual boundary 기반 분할/병합 설계는 활성 개발 경로가 아닙니다.

## 요약

- 과거에는 wall + virtual boundary 그래프로 room을 분할하려는 시도가 있었다.
- 현재는 복잡도를 줄이기 위해 room을 직접 생성/선택/이동/수정하는 방향으로 정리했다.
- 필요 시 이후에 room merge 기능만 별도 재설계하는 편이 더 적절하다.
