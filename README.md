# 데스크몬 (Deskmon) — Unity

바탕화면에서 픽셀 크리처를 만나고, 잡고, 함께 사는 데스크탑 마스코트 게임.
투명 오버레이 창이 화면 전체를 덮고, 크리처가 작업 중인 창들 위를 산책한다.
포획은 마우스로 문양을 그리는 "각인"으로 한다.

- **엔진**: Unity 6000.0.77f1 · Windows Standalone (D3D11 · BitBlt 스왑체인)
- **원본**: 검증이 끝난 Electron 프로토타입 (동작 "정답지"로 사용, 포팅 완료)
- **창 제어**: [UniWindowController](https://github.com/kirurobo/UniWindowController) (UPM git 의존성 — 최초 열기 때 자동 수신)

## 무엇이 있나

- **출몰과 포획** — 시간·요일·작업 여부에 따라 야생이 나타난다. 접근 난이도
  3종(느긋함·겁쟁이·순간이동)을 뚫고 각인 문양을 그리면 포획. 잡히기 전까지
  야생은 떠나지 않는다. 문양은 20종 — $1 인식 16종 + 직선 4방향 전용 판정.
- **도감 151종** — 진화체마다 개별 도감 번호를 갖는 151 엔트리(80 진화 라인).
  전 종 실제 64px 도트 연결. 상세 보기는 폼 단위로 번호를 순환해 진화체도
  자기 페이지를 갖는다. 도감 카드 이미지 저장.
- **필드 13종** — 초원부터 동굴·산·해안·하늘·도시·유적·기계·꿈·날씨까지
  베리로 해금. 별도로 스페셜(루미 등)과 이벤트(크로노·소원지) 풀이 있다.
- **키우기** — 방목(필드 해금당 슬롯 +1), 쓰다듬기/간식 친밀도, 진화(야행·만복
  게이트), 샤이니(팔레트 스왑 셰이더), 전설 무지개 연출.
- **진영** — 호수 해금 시 이슬/이끼 팀 선택. 진영 배타 종이 갈린다.
- **손맛** — 공놀이(기획 v4 §6.2), 합성 효과음(에셋 없이 런타임 베이크),
  걷기 꿀렁 코드 모션(대기 호흡과 분리 — 프레임 애니메이션 대체, `WobbleMotion`).
- **이벤트** — 크로노는 매주 금요일 밤 모두의 화면에 동시에 나타난다.

## 구조

### 런타임 (`Assets/Scripts/`)

| 영역 | 파일 | 내용 |
|---|---|---|
| 창 제어 | `Native/WindowController.cs` | UniWinC 파사드. 투명/항상위/클릭통과/전체화면 앱 감지 |
| 오버레이 | `Native/DesktopOverlay.cs` | 커서 근접 → 클릭통과 토글, 전체화면 자동 숨김 |
| 안전장치 | `Native/Killswitch.cs` | Ctrl+Alt+Q 전역 핫키 + 워치독 + 검은화면 자동 종료 |
| 유휴 | `Native/IdleTime.cs` | GetLastInputInfo — "작업 중" 판정의 근거 |
| 데이터 | `Core/SpeciesData` `FieldData` `BalanceData` `DeskmonDatabase` | ScriptableObject. 종 80라인 · 필드 13 |
| 세이브 | `Core/SaveData` `SaveSystem` | 원본 JSON 스키마 유지 + 마이그레이션 + 원자적 쓰기 |
| 루프 | `Core/GameState` `SpawnScheduler` `RoamSystem` `RoamManager` | 출몰 스케줄 · 방목 |
| 성장 | `Core/FriendshipSystem` `EvolutionSystem` `CreatureRegistry` | 친밀도 · 진화 연쇄 · 도감 등록/마일스톤 |
| 포획 | `Capture/` | $1 Unistroke 각인 인식 · 접근 패턴 · 포획 연출 |
| 크리처 | `Creatures/` | 산책 모션 · 팔레트 스왑/아웃라인 · 공놀이 |
| UI | `UI/` | 코너 카드 · 도감(상세/카드 저장) · 가방 · 설정 · 진영 모달 · 테마 |
| 개발 | `Core/DevOverrides` `GameDebugHUD` | DEV.time/work/day 오버라이드 — 게이트 실측용 (배포 빌드 제외) |

### 데이터 파이프라인 — 문서가 정본

`Docs/151종_몬스터_스프라이트_생성_기획서.md` §17 "도감 번호 기준 원장"(151행 표)이
게임 데이터의 정본이다. 에디터 임포터가 이 md를 **직접 파싱**한다:

```
기획서 §17 표 수정
  → [Deskmon/데이터 임포트 (151 원장 -> 에셋)]
  → 진화군 복원(151 엔트리 → 80라인) + SpeciesData/FieldData 생성
  → MonsterGenV2_64 도트를 런타임 이름으로 복사 + 픽셀 임포트 설정 강제
```

- 원장에 없는 게임플레이 값은 휴리스틱(희귀도→행동패턴, 서브필드→출몰 게이트)과
  data.js 시절 손튜닝 오버라이드로 채운다 (`Assets/Editor/SpeciesImporter.cs`).
- 대표색은 도트에서 최빈색을 추출한다 — 팔레트 스왑의 기준색이라 실색과 일치해야
  샤이니가 물든다. 샤이니색은 id 해시 기반 색상 회전(재임포트해도 동일).
- `Assets/Sprites/`에 png를 넣으면 `SpriteAutoLink`가 임포트를 자동 실행한다.
- CLI에서 임포트를 요청하려면 프로젝트 루트에 `.deskmon-import-request` 파일을
  만들고 에디터에 포커스를 주면 된다 (`Assets/Editor/ImportRequest.cs`).

아트 규칙: 정본 64x64 · PPU 100 · Point 필터 · 무압축. `Docs/픽셀_스타일_가이드.md` 참조.

## 빌드

**처음 열었다면 순서대로:**

1. `Deskmon/데이터 임포트 (151 원장 -> 에셋)` — 종 80라인 · 필드 13 에셋 생성
2. `Deskmon/본 게임 씬 생성` — `Assets/Scenes/Main.unity` + 빌드 대상 등록
3. `Deskmon/빌드 후 실행 (배포)` 또는 `Deskmon/개발 빌드 후 실행`

CLI (배치 빌드):

```sh
Unity.exe -quit -batchmode -nographics \
  -projectPath . \
  -executeMethod Deskmon.EditorTools.GameBuilder.CI \
  -logFile build.log
```

산출물은 `Build/Deskmon/Deskmon.exe` (저장소에서 제외됨).

### 패키징 (인스톨러)

1. `Deskmon/빌드만 (배포)` — 개발 HUD/치트 키가 빠진 배포 빌드
2. Inno Setup 6 설치 후:
   ```sh
   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" Tools\Installer\Deskmon.iss
   ```
3. 산출물: `Build/Installer/Deskmon-Setup-<버전>.exe`
   — 사용자별 설치(관리자 불필요), 로그인 시 자동 시작 옵션, 제거 시 세이브 유지

코드 서명은 인증서 확보 후 `Deskmon.iss`의 SignTool 주석을 해제한다.
이 앱은 투명 오버레이 + 전역 키 폴링이라 서명 없이는 백신 오탐 확률이 높다.

### 검증 도구 (에디터 메뉴)

- `Deskmon/S3 로직 자가 점검` — 방목·친밀도·진화 규칙 회귀 확인 (임시 세이브 사용)
- `Deskmon/각인 인식 자가 점검` — 문양 추가 후 회귀 확인 (자기 분류·거울상·출제 정합)
- `Deskmon/각인 UI 테스트 씬 생성` — 문양을 직접 그려본다
- `Deskmon/S0 스파이크 씬 생성` — 투명 창만 떼어내 확인

> **종료는 Ctrl+Alt+Q.** 이 앱은 포커스를 받지 않고 Alt+Tab·작업표시줄에도 뜨지 않는다.
> 렌더가 잘못되면 작업 관리자 외에 끌 방법이 없어서 전역 핫키가 안전장치로 항상 들어간다.

## 문서 (`Docs/`)

| 문서 | 내용 |
|---|---|
| `151종_몬스터_스프라이트_생성_기획서.md` | **§17 도감 원장 = 게임 데이터 정본.** 생성 프롬프트·검수 기준 포함 |
| `151종_몬스터_디자인_원장.md` | 종별 실루엣·파츠·팔레트·금지 소재 디자인 원장 |
| `몬스터_디자인_품질기준.md` | 디자인 품질 기준 |
| `UI_이미지_기획서.md` | UI 테마 이미지 기획 (frame/badge/icon 파이프라인) |
| `픽셀_스타일_가이드.md` | 도트 제작 규칙 (64px · 팔레트 · 외곽선) |

기획서 v4·Unity 포팅계획은 저장소 밖 상위 문서다. 코드 주석이 해당 절을 인용한다.

## 남은 것

- 걷기 좌우 바라보기 방향 검증 (도트 세트 확정 후 — MonsterWalk64 v2 도트는 보류 자산)
- 샤이니 저채도 16종 색변경 이미지 (`<id>_shiny.png` 규격으로 직접 제공 예정)
- 신규 종 자동 추출 색 스팟체크 (이상한 종만 `SpeciesImporter.Overrides`에 손색 추가)
- 멀티모니터 · DPI 배율 환경 검증
- 코드 서명

페이싱은 "천천히 해금하고 키우기"가 목표라 해금 속도 목표치는 없다 (2026-08-18 결정).
포획 즉시 보상(원본 catchBonusSec)은 의도적으로 이식하지 않았다 — 수입원은
생산·쓰다듬기·마일스톤 세 가지다.

## 참고 — 폐기한 접근

투명 창을 직접 Win32(`SetWindowLong` + `DwmExtendFrameIntoClientArea`)로 구현했다가
**화면 전체가 검은 창으로 덮이는** 실패를 겪고 UniWindowController 위임으로 갈아엎었다.
이유는 `Native/WindowController.cs` 상단 주석에 남겼다. 되돌리지 말 것.
