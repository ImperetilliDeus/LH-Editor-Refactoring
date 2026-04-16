# Workflows

현재 프로젝트의 대표 동작 흐름입니다.

## 1. 벽 작성

1. `Default` 모드 진입
2. 벽을 그리거나 endpoint handle로 조정
3. 필요하면 벽 자체를 선택해서 이동
4. handle snap이 필요하면 `Ctrl`을 누른 상태에서 드래그

## 2. room 작성

1. `RoomCreate` 모드 진입
2. 빈 공간을 드래그해서 room 생성
3. 기존 room 클릭 시 선택
4. room drag로 이동
5. room handle drag로 polygon 수정
6. 생성된 `Floor` 시각 오브젝트는 월드 `y = 0.1`에 배치

## 3. DetailEdit 벽 편집

1. `DetailEdit` 모드 진입
2. 벽 선택 또는 다중 선택
3. 길이/높이/두께 입력 적용
4. 입력 직후 3D / top view / room 정보가 즉시 갱신
5. 문/창 배치 또는 opening container 재구성

## 4. handle drag

1. `HandleManager`가 vertex group을 구성
2. 일반 endpoint는 연결된 wall vertex를 같이 이동
3. split point는 허용된 선분 위에서 슬라이드
4. T자 접합은 split point 기준으로 host wall을 따라 이동

## 5. wall 시각 갱신

1. wall geometry가 변경됨
2. `HandleManager` / `WallPropertyInputManager` / `WallSelectionManager`가 관련 refresh 수행
3. top view는 dirty flag로 다시 그림
4. 3D wall object는 인접 벽의 방향/두께를 참고해 end-cap을 재계산

## 6. export

1. 최종 편집 상태 확인
2. room / wall / opening 데이터가 최신인지 확인
3. exporter 실행
