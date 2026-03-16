---
name: camera-and-view
description: 카메라, ViewConverter, XZ 좌표계, 경계 클램프 시스템
type: project
---

# 카메라 및 뷰 시스템

## ViewConverter (팀별 관점 변환)
- 파일: `Core/ViewConverter.cs` (정적 클래스)
- 공식: `viewPos = 2 * mapCenter - domainPos` (자기 역함수)
- 방향 반전: `FlipDirection(dir) = (dir + 3) % 6` (Red팀만)
- 초기화 순서: StartNetworkGame() → ViewConverter.Setup(isRed, mapCenter) → LoadMap()
- LoadMap() 내 싱글플레이만 ViewConverter.Reset() (네트워크는 Setup 유지)
- 적용 위치: HexGridRenderer, UnitFactory, BuildingFactory, UnitView, InputHandler, ProductionTicker
- 도메인 좌표는 항상 Blue 기준 — 뷰 레이어에서만 반전

## XZ 좌표계 (Phase 1)
- HexMetrics.HexToWorld(): `new Vector3(x, 0f, z)`
- HexMetrics.WorldToHex(): X, Z 기반 역산
- ViewConverter.ToView(): X, Z 반전 (Y=높이 통과)

## 카메라 설정
- Orthographic + X축 55도 틸트
- CameraController: `_tiltAngle=55f` SerializeField, Start()→ApplyTilt()
- ScreenToXZPlane(): Plane.Raycast 기반 (틸트 후에도 정확)
- 팬 시 Y 고정: `new Vector3(diff.x, 0f, diff.z)`

## 카메라 경계 ClampPosition (2026-03-07)
- halfW = orthographicSize * aspect
- halfH = orthographicSize / sin(tiltAngle)
- look-at point 변환: `lookAtZ = pos.z + zOffset`, 클램프 후 역변환
- 매 프레임 Update()에서 ClampPosition() 호출

## 틸트 Z 오프셋 원리
- `zOffset = cameraHeight / tan(tiltAngle)`
- 카메라 X축 틸트 시 화면 중앙이 앞쪽(+Z) 향함 → -Z 오프셋 필요

## 건물 렌더링 버그 수정 이력
### Red팀 건물 위치 버그 (2026-02-22)
- 원인: StartNetworkGame()에서 GridCenter()를 Orientation 설정 전 호출
- 수정: GridCenter() 전 HexMetrics.Orientation/TileWidth/TileHeight 사전 설정
### Y 오프셋 적용 순서 버그
- 원인: _buildingYOffset을 ToView() 이전에 적용 → 반전 시 오프셋 방향도 반전
- 수정: ToView() 이후에 오프셋 가산
