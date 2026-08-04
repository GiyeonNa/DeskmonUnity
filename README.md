# 데스크몬 (Deskmon) — Unity

바탕화면에서 픽셀 크리처를 만나고, 잡고, 함께 사는 데스크탑 마스코트 게임.
검증이 끝난 Electron 프로토타입을 Unity로 포팅하는 중이다.

- **엔진**: Unity 6000.0.77f1 · Windows Standalone
- **원본**: Electron 프로토타입 (동작 "정답지"로 사용)
- **기획**: `데스크몬_기획서_v4.md` · **포팅 계획**: `데스크몬_Unity포팅계획.md`

## 현재 상태 — S0 하드파트 스파이크

포팅계획 §4의 6단계(S0~S5) 중 **S0 코드 완료**. 배치 빌드 통과, 실행 검증 진행 중.

S0의 목표는 "이게 되면 나머지는 번역"인 리스크 제거다 —
투명·클릭통과·항상위 전체화면 창 + 크리처 1마리 산책 + 커서 근접 시 클릭통과 해제.

| 영역 | 파일 | 내용 |
|---|---|---|
| 창 제어 | `Native/WindowController.cs` | UniWinC 파사드. 투명/항상위/클릭통과/전체화면 앱 감지 |
| 오버레이 | `Native/DesktopOverlay.cs` | 커서 근접 → 클릭통과 토글, 전체화면 자동 숨김, 항상위 재확보 |
| 안전장치 | `Native/Killswitch.cs` | Ctrl+Alt+Q 전역 핫키 + 폴링 + 워치독 + 검은화면 자동 종료 |
| 커서 | `Native/NativeCursor.cs` | 클릭통과 중 커서를 읽기 위한 GetCursorPos 폴링 |
| 유휴 | `Native/IdleTime.cs` | GetLastInputInfo (32비트 래핑 처리) |
| 크리처 | `Creatures/CreatureView.cs` | 산책 상태머신 + 부피보존 스쿼시·플립·홉 |
| HUD | `SpikeHUD.cs` | S0 검증용 오버레이 (F1 토글) — 본 게임에는 미포함 |

### 남은 검증

- [ ] 통과 상태에서 뒤쪽 창/바탕화면 아이콘이 실제로 클릭되는가
- [ ] 크리처에 커서를 대면 통과가 풀리고 HUD "클릭 수신"이 오르는가
- [ ] `IsForegroundFullscreen()` 오탐 여부 — 최대화 창을 전체화면으로 잘못 잡는지
- [ ] 멀티모니터 · DPI 배율 환경 (포팅계획 §6 최우선 리스크)

## 빌드

UniWinC는 UPM git 의존성이라 최초 열기 때 자동으로 받아온다 (`Packages/manifest.json`,
버전은 `packages-lock.json`에 해시로 고정).

에디터 메뉴:

- `Deskmon/S0 스파이크 씬 생성` — 씬을 코드로 재생성 (손으로 만든 씬은 재현이 안 된다)
- `Deskmon/S0 빌드 후 실행`

CLI (배치 빌드):

```sh
Unity.exe -quit -batchmode -nographics \
  -projectPath . \
  -executeMethod Deskmon.EditorTools.BuildSpike.CI \
  -logFile build.log
```

산출물은 `Build/S0/Deskmon.exe` (저장소에서 제외됨).

> **종료는 Ctrl+Alt+Q.** 이 앱은 포커스를 받지 않고 Alt+Tab·작업표시줄에도 뜨지 않는다.
> 렌더가 잘못되면 작업 관리자 외에 끌 방법이 없어서 전역 핫키가 안전장치로 항상 들어간다.

## 앞으로

| 단계 | 내용 |
|---|---|
| S1 | 픽셀 크리처 — 정지 스프라이트 + 코드 모션, 팔레트스왑 샤이니, 초기 6종 |
| S2 | 코어 루프 — 출몰 스케줄 → 각인 포획 → 데이터/세이브 (JSON 스키마 유지) |
| S3 | 시스템 — 방목·친밀도·진화, 코너 카드 UI, 도감/가방/설정 |
| S4 | 콘텐츠 — 필드 4·종 12, 진영, 크로노, 출몰 조건 매트릭스 |
| S5 | 폴리시·패키징 — 오디오·파티클, 서명된 .exe |

## 참고 — 폐기한 접근

투명 창을 직접 Win32(`SetWindowLong` + `DwmExtendFrameIntoClientArea`)로 구현했다가
**화면 전체가 검은 창으로 덮이는** 실패를 겪고 UniWindowController 위임으로 갈아엎었다.
이유는 `Native/WindowController.cs` 상단 주석에 남겼다. 되돌리지 말 것.
