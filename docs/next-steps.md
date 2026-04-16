# Next Steps

현재 구조 기준으로 남아 있는 후속 작업 후보입니다.

## 1. 3D wall join 정밀화

최근 변경으로 endpoint별 연결 벽 기준 end-cap 보정이 들어갔지만, 복잡한 접합에서는 여전히 본체 mesh 기반 miter join이 더 적합할 수 있습니다.

- wall 본체 mesh를 endpoint별 가변 길이/가변 단면으로 바꾸는지 검토
- opening container wall segment와 일반 wall join 정책을 통합할지 검토

## 2. DetailEdit UX 검증

- Ctrl 기반 handle snap이 실제 사용자 기대와 맞는지 확인
- T자 접합 split point drag가 모든 케이스에서 슬라이드처럼 느껴지는지 점검
- 길이/높이/두께 변경 직후 즉시 반영이 누락되는 경로가 없는지 확인

## 3. scene 정리

- `SampleScene.unity` 내 legacy 오브젝트 이름/비활성 버튼 정리
- Inspector 참조가 자동 탐색에 의존하는 부분 최소화

## 4. room / floor 정책 확인

- floor 월드 `y = 0.1` 정책이 NavMesh, 가구 배치, export와 충돌하지 않는지 검증
- room root 위치와 floor 시각 위치를 분리한 현재 정책을 계속 유지할지 검토

## 5. export 회귀 점검

- room polygon
- wall / opening 데이터
- Undo/Redo 후 export 결과
