# KOS Estimate

KOS는 현장 프로젝트별 CAD 도면 관리, DWG/DXF 분석, 물량산출, 자동 단가 매칭, Excel형 공내역서 및 출력 기능을 하나의 Windows 프로그램으로 통합한 테스트 버전입니다.

## 첫 통합 빌드 포함 기능

- 현장 프로젝트 생성·검색·저장
- 프로젝트별 Drawings / Analysis / Output / Logs 분리
- DWG/DXF 파일 개별 다중 추가
- CAD 폴더 및 하위 폴더 일괄 추가
- ACadSharp 기반 AutoCAD 없는 DWG/DXF 분석
- 객체 Handle, 레이어, 길이, 면적, 블록 개수 후보
- 치수·문자·해치·도곽 등 직접 물량 제외
- 길이 m / 면적 ㎡ / 개수 개소 구분
- 공종·품명·규격·층·구역·도면별 측정 그룹
- 조달청 2026 및 XCost 공개 참조 데이터 자동 매칭
- 재료비·노무비·경비·합계단가·금액 표시
- 단가 미확정 항목 유지
- Excel형 공내역서 편집·검색·필터
- 산출 근거 오른쪽 패널
- 표준 공내역서 Excel 출력
- self-contained KOS.exe
- KOS_Setup.exe 자동 생성

## 자동 빌드

저장소의 `main` 브랜치에 파일을 업로드하거나 커밋하면 GitHub Actions가 자동으로 Windows 빌드를 시작합니다.

결과물:

- `KOS_Setup.exe`
- `KOS_Portable_win-x64.zip`

## 다운로드 위치

1. GitHub 저장소 상단의 **Actions**
2. 최신 **Build KOS Windows**
3. 아래쪽 **Artifacts**
4. `KOS-Windows-Test` 다운로드

또는 저장소 오른쪽 **Releases**에서 최신 `KOS 개발 테스트 빌드`의 `KOS_Setup.exe`를 받습니다.

## 데이터 저장 위치

프로그램과 현장 데이터는 분리됩니다.

```text
문서\KOS\Projects\프로젝트ID\
├─ project.json
├─ Drawings\
├─ Analysis\
├─ Output\
└─ Logs\
```

프로그램을 업데이트하거나 제거해도 프로젝트 데이터는 자동 삭제하지 않습니다.

## 현재 검증 범위

GitHub Actions Windows 빌드에서 컴파일·설치파일 생성 여부를 검증합니다. CAD 산출 정확도는 실제 도면 테스트 결과를 기반으로 단계별로 개선합니다. 검토되지 않은 품목과 단가는 확정값으로 취급하지 않습니다.
