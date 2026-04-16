# Virtual Boundary Design

virtual boundary 관련 설계 메모입니다.

## 현재 역할

- room boundary 계산의 보조 입력으로 사용될 수 있습니다.
- top view에서는 별도 dashed segment로 렌더링됩니다.

## 현재 구현과의 관계

- 메인 wall 편집 흐름의 주체는 실제 `Wall` 데이터입니다.
- 최근 수정된 wall join, handle snap, floor height 정책은 virtual boundary 자체보다 wall/room 편집 경로에 집중되어 있습니다.

## 용도

- 추후 room 분할/보조 경계 처리 검토 시 참고
