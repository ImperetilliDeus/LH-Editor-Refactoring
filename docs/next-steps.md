# Next Steps

현재 구조 기준으로 남아 있는 다음 작업 후보입니다.

## 1. `SampleScene` 재저장

이번 정리 과정에서 씬을 Unity 백업으로 복구했습니다.

- Unity Editor에서 `SampleScene.unity`를 열고
- 참조가 정상인지 확인한 뒤
- 한 번 저장해 두는 것이 좋습니다.

## 2. Scene UI 이름 정리

스크립트 역할은 `RoomAuthoringPanelManager`로 정리됐지만, 씬 안 패널 이름은 일부 예전 명칭이 남아 있을 수 있습니다.

- room 속성 패널 오브젝트 이름 정리
- 비활성 legacy 버튼이 남아 있는지 재확인

## 3. 남은 UI 오브젝트 최적화

- opening marker/label 풀링
- 화면 밖 label 숨김 정책 강화
- handle 재사용 구조 보강

## 4. room 작성 UX 개선

- room 생성 시 스냅 피드백 강화
- room 이동/크기 조절 시 제약 옵션 추가
- 직사각형 외 polygon room 작성 확장 검토

## 5. export 검증

- room polygon
- wall/opening 데이터
- JSON 구조

이 세 가지가 현재 작성 흐름과 맞는지 다시 점검할 필요가 있습니다.
