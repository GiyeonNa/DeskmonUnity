# 데스크몬 (Deskmon) — Unity

바탕화면에서 픽셀 크리처를 만나고, 잡고, 함께 사는 데스크탑 마스코트 게임.
검증이 끝난 Electron 프로토타입을 Unity로 포팅하는 중이다.

- **엔진**: Unity 6000.0.77f1 · Windows Standalone
- **원본**: Electron 프로토타입 (동작 "정답지"로 사용)
- **기획**: `데스크몬_기획서_v4.md` · **포팅 계획**: `데스크몬_Unity포팅계획.md`

## 현재 상태 — S2 코어 루프 완료

포팅계획 §4의 6단계(S0~S5) 중 **S0~S2 코드 완료**. 다음은 S3.

S2의 DoD "한 종을 잡아 도감에 등록"이 성립한다 —
출몰 스케줄 → 각인 포획 → 포획 연출 → 도감 등록 → 저장.

### 런타임 (`Assets/Scripts/`)

| 영역 | 파일 | 내용 |
|---|---|---|
| 창 제어 | `Native/WindowController.cs` | UniWinC 파사드. 투명/항상위/클릭통과/전체화면 앱 감지 |
| 오버레이 | `Native/DesktopOverlay.cs` | 커서 근접 → 클릭통과 토글, 전체화면 자동 숨김, 항상위 재확보 |
| 안전장치 | `Native/Killswitch.cs` | Ctrl+Alt+Q 전역 핫키 + 폴링 + 워치독 + 검은화면 자동 종료 |
| 커서 | `Native/NativeCursor.cs` | 클릭통과 중 커서를 읽기 위한 GetCursorPos 폴링 |
| 유휴 | `Native/IdleTime.cs` | GetLastInputInfo (32비트 래핑 처리) |
| 데이터 | `Core/SpeciesData` `FieldData` `BalanceData` `DeskmonDatabase` | `data.js` 1:1 이전 (ScriptableObject) |
| 세이브 | `Core/SaveData` `SaveSystem` | 원본 JSON 스키마 유지 + `migrateV4` + 원자적 쓰기 |
| 진행 | `Core/CreatureRegistry` | 도감 등록, 마일스톤, 생산량 |
| 루프 | `Core/GameState` `SpawnScheduler` | 출몰 → 포획 → 등록 → 저장을 잇는 지점 |
| 포획 | `Capture/SigilRecognizer` | $1 Unistroke 이식. 문양 8종 + 그리기 변형·거울상 |
| | `Capture/SigilCapture` `SigilInput` `SigilUI` | 각인 판정 / 입력 / 렌더 |
| | `Capture/WildBehavior` | 접근 난이도 (느긋함·겁쟁이·순간이동) |
| | `Capture/CaptureEffects` `CaughtAnimation` | 링·하트·반짝임, 날아가기 |
| 크리처 | `Creatures/CreatureView` `CreatureAppearance` | 산책 모션 / 팔레트 스왑·아웃라인 |

### 개발용 (배포물에 미포함)

`SpikeHUD` · `Capture/SigilTestHarness` — 검증 HUD.
씬 `S0_Spike` · `SigilTest`, 그리고 `Editor/` 의 씬 생성기와 자가 점검 도구.

### 검증 이력 (2026-08-06, 빌드에서 확인)

- [x] 투명·항상위·클릭통과 창 + 뒤쪽 창 클릭
- [x] 코어 루프: 출몰 → 각인 → 포획 연출 → 도감 등록 → 저장/복원
- [x] 각인 UI(GL)가 투명 오버레이 위에서 정상 렌더
- [x] 전체화면 감지 — Win11 WorkerW 오탐 수정 후 정상 (숨김/복귀 로그 항상 남음)
- [ ] 멀티모니터 · DPI 배율 환경 (포팅계획 §6 최우선 리스크)

## 빌드

UniWinC는 UPM git 의존성이라 최초 열기 때 자동으로 받아온다 (`Packages/manifest.json`,
버전은 `packages-lock.json`에 해시로 고정).

**처음 열었다면 순서대로:**

1. `Deskmon/데이터 임포트 (data.js -> 에셋)` — 종 12·필드 4 에셋 생성
2. `Deskmon/본 게임 씬 생성` — `Assets/Scenes/Main.unity` + 빌드 대상 등록
3. `Deskmon/빌드 후 실행`

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

### 검증 도구

- `Deskmon/각인 UI 테스트 씬 생성` — 문양을 직접 그려본다. 이전/다음으로 8종 순회
- `Deskmon/각인 인식 자가 점검` — 문양 추가 후 회귀 확인 (자기 분류·거울상·출제 정합)
- `Deskmon/S0 스파이크 씬 생성` — 투명 창만 떼어내 확인. 빌드 대상은 바꾸지 않는다

> **종료는 Ctrl+Alt+Q.** 이 앱은 포커스를 받지 않고 Alt+Tab·작업표시줄에도 뜨지 않는다.
> 렌더가 잘못되면 작업 관리자 외에 끌 방법이 없어서 전역 핫키가 안전장치로 항상 들어간다.

## 앞으로

| 단계 | 내용 | 상태 |
|---|---|---|
| S1 | 픽셀 크리처 — 정지 스프라이트 + 코드 모션, 팔레트스왑 샤이니 | 코드 완료 · **도트 6종 제작 남음** |
| S2 | 코어 루프 — 출몰 → 각인 포획 → 연출 → 데이터/세이브 | 완료 |
| S3 | 시스템 — 방목·친밀도·진화, 코너 카드 UI, 도감/가방/설정 | 다음 |
| S4 | 콘텐츠 — 필드 4·종 12, 진영, 크로노, 출몰 조건 매트릭스 | |
| S5 | 폴리시·패키징 — 오디오·파티클, 서명된 .exe | |

**S3 시작 전 필요한 결정** — 기획서 v4 §11의 열린 결정 중 *방목 최대 슬롯 수*(5 가안).
UI 레이아웃이 여기 물린다.

아트는 `Docs/픽셀_스타일_가이드.md` 기준. 현재 스프라이트는 코드 생성 플레이스홀더다.

## 참고 — 폐기한 접근

투명 창을 직접 Win32(`SetWindowLong` + `DwmExtendFrameIntoClientArea`)로 구현했다가
**화면 전체가 검은 창으로 덮이는** 실패를 겪고 UniWindowController 위임으로 갈아엎었다.
이유는 `Native/WindowController.cs` 상단 주석에 남겼다. 되돌리지 말 것.
