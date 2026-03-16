---
name: gameplay-systems
description: 랠리포인트, 생산 시스템, 건물 배치 등 게임플레이 시스템 세부사항
type: project
---

# 게임플레이 시스템

## 랠리포인트 마커 (2026-03-07)
- GameConfig.RallyMarkerOffset (Vector3, default: 0.05/0.15/0)
- GameConfig.RallyMarkerEuler (Vector3, default: 0/0/0)
- ProductionTicker.CreateOrMoveMarker(): _config 참조

## 공격 방향 — 상세: [attack-direction-refactor.md](attack-direction-refactor.md)
- TryAttack 반환: `(int id, bool isUnit)?` 튜플
- UnitView.CalculateAttackAngle: 실제 transform.position → Atan2 → _meshYOffset 보정
- BuildingFactory.GetBuildingObject(int buildingId) 추가

## 팀별 초상화 동적 업데이트 (2026-03-14)
- ProductionPanelUI: UpdateButtonPortraits(TeamId) — Show(barracks) 시 교체
- BuildingPlacementUI: UpdateButtonPortraits(TeamId) — Show(coord, team) 시 교체
