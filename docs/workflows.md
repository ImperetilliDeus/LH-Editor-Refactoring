# Workflows

현재 프로젝트의 대표 동작 흐름입니다.

## 1. 벽 작성

1. `Default` 모드 진입
2. 벽을 그리거나 endpoint handle로 조정
3. 필요하면 벽 자체를 선택해서 이동

## 2. room 작성

1. `RoomCreate` 모드 진입
2. 빈 공간을 드래그해서 room 생성
3. 기존 room을 클릭해 선택
4. room 내부 드래그로 이동
5. room handle 드래그로 크기/형태 조절
6. 우측 room 속성 패널에서 타입/면적 확인

## 3. room 선택 시 UI 동기화

1. `RoomHandleManager`가 focus room을 갱신
2. `RoomAuthoringPanelManager`가 선택 room을 동기화
3. room 타입 드롭다운과 면적 필드가 갱신
4. top view와 3D room이 강조색으로 표시

## 4. 벽 상세 편집

1. `DetailEdit` 모드 진입
2. 벽 선택
3. 길이/높이/속성 수정
4. 문/창 배치

## 5. top view 갱신

1. 벽/room/opening 데이터가 변경됨
2. 관련 매니저가 dirty 상태를 올림
3. `TopViewRenderManager`가 배치 그래픽을 다시 그림

## 6. export

1. 편집 상태 최종 확인
2. room/wall/opening 데이터 최신 상태 확인
3. exporter 실행
