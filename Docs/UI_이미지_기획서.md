# UI 이미지 생성 기획서

베이스캠프 카드/패널/연출에 쓸 UI 이미지 15종의 제작 사양.
`151종_몬스터_스프라이트_생성_기획서.md`와 같은 체계를 따른다 -
원장을 먼저 채우고, 개별 프롬프트로 생성하고, 검수표를 통과한 것만 정식 파일이 된다.

현재 UI는 단색 사각형 플레이스홀더(코드 드로잉)로 동작하며, 이미지가 승인되면
`UIKit`의 팩토리만 교체해 전체 적용한다 (§10).

## 1. 핵심 원칙

1. 크리처 도트와 같은 세계의 물건이어야 한다 - 초기 휴대용 몬스터 RPG의 메뉴 창 문법.
2. UI는 크리처보다 조용해야 한다. 화면의 주인공은 방목 몬스터이고 카드는 받침이다.
   장식은 모서리에만, 면은 비워둔다.
3. 특정 기존 프랜차이즈의 UI(창 테두리, 아이콘 모양)와 직접 닮으면 안 된다.
4. 같은 역할군(프레임끼리, 아이콘끼리)은 한 세트로 보여야 한다 - 선 두께, 팔레트,
   라운드 반경을 통일한다.
5. 아이콘은 실루엣만으로 의미가 읽혀야 한다. 12~16px에서 디테일은 노이즈다.
6. 텍스트를 이미지에 넣지 않는다. 글자는 전부 OS 폰트가 그린다.

## 2. 공통 이미지 스펙

| 항목 | 규칙 |
|---|---|
| 작업 원본 | 생성 시 마젠타 `#ff00ff` 크로마키 배경, 후처리로 투명화 |
| 스타일 | 초기 휴대용 몬스터 RPG풍 픽셀 UI. 1px 그리드, 안티에일리어싱 금지 |
| 팔레트 | 면 `#1c2a18`(진녹) · 테두리 `#8fd977`(연두) · 포인트 `#ffe9a3`(금) · 보조 `#ff9ec6`(분홍) |
| 색 수 | 프레임 3~4색, 아이콘 4~6색 |
| 반투명 | 프레임 면 채움에만 허용 (알파 93%). 선과 아이콘은 불투명 |
| 필터 | Point, 무압축, mipmap 끔 |
| 텍스트/워터마크 | 금지 |

팔레트 근거: 테두리 연두는 몽글이 본체색(`#8fd977`)과 동일 - 시리즈 정체성.
면은 바탕화면 위 가독성을 위해 어두운 쪽을 쓴다(원본 index.html은 밝은 종이 톤이었지만
데스크탑 오버레이에서는 아이콘과 뒤섞인다).

## 3. 파일 명명 규칙

### 3.1 후보 생성 파일 (기존 에셋을 덮어쓰지 않는다)

```text
Assets/Sprites/UI/<asset_id>_ai_v<n>.png
예: Assets/Sprites/UI/ui_frame_card_ai_v1.png
```

### 3.2 승인 후 정식 파일

```text
Assets/Sprites/UI/<asset_id>.png
예: Assets/Sprites/UI/ui_frame_card.png
```

검수 통과 시 후보를 정식 이름으로 바꾸고 `_ai_v*`는 삭제한다. 버전 이력은 git이 담당한다.

## 4. 제작 원장

이미지 생성 전 이 표가 정본이다. `status`: todo / draft / review / approved.

### 4.1 원장 스키마

| 컬럼 | 설명 |
|---|---|
| asset_id | 파일명이 되는 식별자 |
| kind | frame(9슬라이스) / icon / fx |
| size | 정식 파일 크기 (px) |
| border | 9슬라이스 모서리 (frame만) |
| role | 화면에서 하는 일 |
| apply_at | 교체 지점 (`UIKit` 함수 또는 컴포넌트) |
| silhouette | 실루엣 한 줄 (아이콘 중복 방지 키) |
| status | 진행 상태 |

### 4.2 원장

| asset_id | kind | size | border | role | apply_at | silhouette | status |
|---|---|---|---|---|---|---|---|
| ui_frame_card | frame | 48x48 | 12 | 카드/패널 배경 | `UIKit.Panel` | 두꺼운 밝은 테두리 창 | approved |
| ui_frame_button | frame | 24x24 | 8 | 버튼 평상시 | `UIKit.Button` | 얇은 테두리 알약 사각 | draft |
| ui_frame_button_on | frame | 24x24 | 8 | 버튼 선택/활성 | `UIKit.Button` | 같은 형태 + 연두 발광 | draft |
| ui_frame_cell | frame | 16x16 | 5 | 도감 폼 칸 (빈 칸) | `DexPanel.BuildRow` | 안쪽으로 파인 홈 | draft |
| ui_icon_berry | icon | 12x12 | - | 베리 수치 옆 | `UIRoot` 요약줄 | 열매 1알 + 잎 1장 | approved |
| ui_icon_heart | icon | 12x12 | - | 친밀도 표시 | `BagPanel.BuildRow` | 도트 하트 | draft |
| ui_icon_bag | icon | 16x16 | - | 가방 탭 | `UIRoot.BuildCard` | 끈 묶인 보따리 | draft |
| ui_icon_dex | icon | 16x16 | - | 도감 탭 | `UIRoot.BuildCard` | 펼친 수첩 | draft |
| ui_icon_gear | icon | 16x16 | - | 설정 탭 | `UIRoot.BuildCard` | 톱니 1개 | draft |
| ui_icon_sparkle | icon | 12x12 | - | 샤이니 표시 | `BagPanel`/`DexPanel` | 4각 반짝 별 | draft |
| ui_icon_sleep | icon | 12x12 | - | 방목 낮잠 상태 | `Roamer` (추후) | z 두 글자 도트 | draft |
| ui_badge | icon | 40x40 | - | 접힌 배지 | `UIRoot.BuildBadge` | 텐트 또는 모닥불 | approved |
| fx_heart | fx | 10x10 | - | 쓰다듬기 하트 | `CaptureEffects.PlayPet` | 하트 (외곽선 포함) | draft |
| fx_spark | fx | 8x8 | - | 포획 반짝이 | `CaptureEffects` | 4각 별 | draft |
| fx_ring | fx | 32x32 | - | 포획 링 | `CaptureEffects` | 얇은 원 고리 | draft |
| app_icon | icon | 256x256 | - | exe/인스톨러 아이콘 | `PlayerSettings` 아이콘 (패키징) | 몽글이 얼굴 클로즈업 | todo |

## 5. 개별 명세와 생성 프롬프트

생성 해상도는 정식 크기의 8~16배로 뽑고 후처리에서 줄인다 (§6).
프롬프트의 크기 표기는 "최종 다운스케일 목표"를 알려주는 용도다.

### 5.1 ui_frame_card - 카드/패널 배경 (최우선)

전체 UI의 톤을 정하는 기준 이미지. 이것을 먼저 승인한 뒤 나머지를 만든다.

- 요구: 두꺼운 연두 테두리(2px) + 진녹 면. 모서리 2~3px 계단 라운드.
  모서리 장식(작은 잎눈 정도)은 12px 안에 가둔다. 변은 단순 반복 패턴, 면은 민무늬.
- 금지: 면 안의 무늬, 그라데이션, 비대칭 모서리.

```text
Use case: stylized-concept
Asset type: Unity game UI sprite source for a 48x48 pixel 9-slice window frame
Primary request: Create an original retro pixel art menu window frame for a monster-collecting desktop game. It must not resemble any existing franchise UI.
Subject: A rectangular dialog frame with a thick light-green border (2px feel) and a dark green fill. Rounded corners made of pixel steps. A tiny leaf-bud ornament in each corner, contained within 12 pixels of the corner.
Style/medium: crisp retro pixel art UI, early handheld monster RPG menu feel, 1-pixel grid, no anti-aliasing, no gradients.
Composition/framing: the frame fills the canvas edge to edge, perfectly symmetrical, edges must be simple repeatable patterns so the image works as a 9-slice, center area is plain flat fill.
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background is NOT needed here; the frame fills the canvas, center fill is the panel color itself.
Color palette: dark green fill #1c2a18, light green border #8fd977, deep outline #0d130b. Maximum 4 colors.
Constraints: no text; no watermark; no creature; no icons; symmetrical in both axes; all corner detail within 12px of corners.
```

### 5.2 ui_frame_button - 버튼 (평상시)

- 요구: 카드와 같은 문법, 테두리 1px로 더 얇게. 모서리 라운드 2px.
  살짝 밝은 면(눌릴 수 있어 보이게) - 면색은 `#243521` 정도.
- 금지: 그림자, 하이라이트 경사(3D 버튼처럼 보이면 안 된다).

```text
Use case: stylized-concept
Asset type: Unity game UI sprite source for a 24x24 pixel 9-slice button frame
Primary request: Create an original retro pixel art button frame matching a monster-collecting game menu window (thin border sibling of the window frame). Must not resemble any existing franchise UI.
Subject: A small rounded rectangle button. 1px light-green border, slightly lighter dark-green fill than the window panel, pixel-step rounded corners.
Style/medium: crisp retro pixel art UI, early handheld monster RPG menu feel, 1-pixel grid, no anti-aliasing, no gradients, completely flat (no bevel, no drop shadow).
Composition/framing: fills the canvas edge to edge, symmetrical, edges plain and repeatable for 9-slice, all corner detail within 8 pixels of corners, center plain fill.
Color palette: fill #243521, border #8fd977, outline #0d130b. Maximum 3 colors.
Constraints: no text; no watermark; no icon; no gloss; symmetrical.
```

### 5.3 ui_frame_button_on - 버튼 (선택/활성)

- 요구: 5.2와 **완전히 같은 형태**에서 색만 바뀐다 - 테두리가 더 밝고 면에 연두 기운.
  나란히 놓으면 "같은 버튼이 켜진 것"으로 읽혀야 한다.
- 제작 방법 권장: 생성하지 말고 5.2 승인본을 색 치환으로 만든다 (형태 일치가 보장된다).
- 색: 면 `#2f4a28`, 테두리 `#b4f09a`.

### 5.4 ui_frame_cell - 도감 폼 칸

- 요구: 안쪽으로 파인 홈. 위·왼쪽 변이 어둡고 아래·오른쪽 변이 밝은 1px 인셋.
  빈 칸 상태가 기본이고, 수집/샤이니는 코드가 칸 위에 색을 얹는다.

```text
Use case: stylized-concept
Asset type: Unity game UI sprite source for a 16x16 pixel 9-slice inset slot
Primary request: Create an original retro pixel art empty inventory slot, an inset socket that looks carved into a dark green panel. Must not resemble any existing franchise UI.
Subject: A small square socket with a 1px inset edge: darker on top/left, slightly lighter on bottom/right, dark recessed fill.
Style/medium: crisp retro pixel art UI, 1-pixel grid, no anti-aliasing, flat colors only.
Composition/framing: fills the canvas, symmetrical enough for 9-slice with 5px borders, center plain.
Color palette: recessed fill #121a0f, dark edge #0a0f08, light edge #3d5535. Maximum 3 colors.
Constraints: no text; no icon; no gloss.
```

### 5.5 ui_icon_berry - 베리

- 요구: 파란 열매 1알 + 잎 1장. 원본 Electron판의 베리 이모지 자리를 잇는 상징.
  1px 짙은 외곽선. 12px에서 "열매"로 읽히면 성공.

```text
Use case: stylized-concept
Asset type: Unity game UI icon source for a 12x12 pixel currency icon
Primary request: Create an original pixel art berry icon for a monster-collecting game currency. Must not resemble any existing franchise item icon.
Subject: One round blue berry with a single small green leaf on top. 1-pixel dark outline. Tiny white 1px highlight.
Style/medium: crisp retro pixel art, 1-pixel grid, no anti-aliasing, flat colors.
Composition/framing: single object centered, generous padding, readable at 12x12.
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background for background removal.
Color palette: berry blue #6ea8ff with darker shade, leaf green #8fd977, outline near-black. Maximum 5 colors.
Constraints: one object only; no text; no watermark; no basket; do not use #ff00ff in the object.
```

### 5.6 ui_icon_heart - 친밀도

- 요구: 분홍 도트 하트 + 1px 외곽선. 빈 하트는 코드가 알파를 낮춰 처리하므로
  채워진 상태 한 장만 만든다.
- 프롬프트: 5.5의 골격에서 Subject만 교체 -
  `One filled pink pixel heart. Palette: pink #ff9ec6 with darker shade #d66f9a, outline near-black.`

### 5.7 ui_icon_bag / 5.8 ui_icon_dex / 5.9 ui_icon_gear - 탭 3형제

한 세트로 보여야 한다. **한 프롬프트로 3개를 같이 생성하지 말고**, 같은 스타일 문장으로
각각 생성한 뒤 나란히 놓고 세트 검수를 한다 (§7.2).

공통 골격 (Subject만 교체):

```text
Use case: stylized-concept
Asset type: Unity game UI icon source for a 16x16 pixel menu tab icon
Primary request: Create an original pixel art <SUBJECT> icon for a monster-collecting game menu. Must not resemble any existing franchise icon.
Subject: <아래 표>
Style/medium: crisp retro pixel art, 1-pixel grid, 1-pixel dark outline, no anti-aliasing, flat colors.
Composition/framing: single object centered, silhouette-first readability at 16x16, generous padding.
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background.
Color palette: warm tan #d9b98c and brown #8a6a45 with green accent #8fd977, outline near-black. Maximum 5 colors.
Constraints: one object only; no text; no watermark; do not use #ff00ff in the object.
```

| 아이콘 | Subject 문장 |
|---|---|
| ui_icon_bag | A small cloth pouch tied with a string, slightly plump |
| ui_icon_dex | An open small field notebook with visible page split line |
| ui_icon_gear | A single chunky gear wheel with 6 teeth |

### 5.10 ui_icon_sparkle - 샤이니

- 요구: 금색 4각 반짝 별. 현재 텍스트 "S"의 대체. 중심 밝고 끝이 뾰족.
- 프롬프트: 5.5 골격, Subject =
  `A four-pointed sparkle star, bright gold #ffe9a3 core with #d9b45c tips, 1px outline.`

### 5.11 ui_icon_sleep - 낮잠

- 요구: "z" 두 글자를 도트화한 형태 (큰 z + 작은 z 대각 배치). 글자처럼 보여도
  이것은 기호라서 허용한다 - 로컬라이즈 대상이 아니다.
- 프롬프트: 5.5 골격, Subject =
  `Two pixel letter-z shapes arranged diagonally, big one lower-left, small one upper-right, pale blue #bcd9ff, 1px outline.`

### 5.12 ui_badge - 접힌 배지

- 요구: 캠프 텐트(권장) 또는 모닥불. "여기를 누르면 캠프가 열린다"가 형태에서
  보여야 한다. 40x40이라 아이콘 중 가장 디테일 여유가 있지만 과밀 금지.
- 원형 배경 포함 - 배지는 프레임 없이 이 이미지 단독으로 뜬다.

```text
Use case: stylized-concept
Asset type: Unity game UI icon source for a 40x40 pixel round badge
Primary request: Create an original pixel art camp badge for a monster-collecting desktop game: a small tent on a round dark-green token. Must not resemble any existing franchise icon.
Subject: A round token/coin shape filling most of the canvas, dark green fill with light green rim, and a small cute triangular camp tent on it. Tent cloth warm tan, entrance visible as dark triangle.
Style/medium: crisp retro pixel art, 1-pixel grid, 1-pixel dark outline, no anti-aliasing, flat colors.
Composition/framing: single round badge centered, generous padding, readable at 40x40.
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background.
Color palette: token #1c2a18 fill / #8fd977 rim, tent tan #d9b98c + brown #8a6a45, outline near-black. Maximum 6 colors.
Constraints: one badge only; no text; no watermark; no creature; do not use #ff00ff in the badge.
```

### 5.13 fx_heart / 5.14 fx_spark / 5.15 fx_ring - 연출 (후순위)

지금은 GL 코드 드로잉이 대신한다. 교체 시 감성 이득은 있으나 우선순위 최하.

- fx_heart: 5.6과 같되 10x10, 외곽선 포함 (바탕화면 위에 단독으로 뜬다)
- fx_spark: 5.10과 같되 8x8
- fx_ring: 32x32, 두께 2px의 금색 원 고리. 코드가 확대해 쓰므로 완전한 원이어야 한다

### 5.16 app_icon - exe/인스톨러 아이콘 (패키징용)

- 요구: 몽글이 얼굴 클로즈업. 작업표시줄 16px에서도 "초록 젤리"로 읽혀야 하므로
  몸 전체가 아니라 얼굴 위주로 크게. 배경은 투명이 아니라 **원형 토큰**(ui_badge와
  같은 문법) - Windows 아이콘은 밝은/어두운 배경 어디에나 놓인다.
- 256x256 한 장이면 된다. Unity가 다운스케일 세트(48/32/16)를 자동 생성한다.
- 프롬프트: 5.12(ui_badge) 골격에서 Subject만 교체 -
  `A close-up face of a cute round green jelly monster with a small sprout leaf on top, on a round dark-green token with light green rim.`

## 6. 후처리 규칙

1. 생성 원본(대개 큰 해상도)을 정식 크기로 다운스케일 - 최근접(nearest) 보간만 사용
2. 마젠타 `#ff00ff` 계열 제거 → 투명. 경계에 마젠타 프린지가 남으면 1px 정리
3. 프레임 면 채움 픽셀만 알파 93%로 조정 (선/장식은 불투명 유지)
4. 색 수 검사 - 팔레트 초과 색은 가장 가까운 팔레트 색으로 스냅
5. `Assets/Sprites/UI/`에 후보 이름(`_ai_v1`)으로 저장
6. Unity 임포트: Sprite / Point / 무압축 / mipmap 끔.
   **frame 3종은 Sprite Editor에서 Border를 원장의 border 값으로 지정** (9슬라이스)
7. 승인 시 정식 이름으로 변경

주의: `Assets/Sprites/` 하위에 png가 들어오면 크리처용 자동 임포트(`SpriteAutoLink`)가
돌지만 UI 파일에는 영향이 없다 (종 id와 파일명이 다르므로 무시된다).

## 7. 검수 체크리스트

### 7.1 단일 이미지

- [ ] 정식 크기가 원장과 일치하는가
- [ ] 투명 배경인가 (마젠타 잔여물 없음)
- [ ] 색 수가 원장 팔레트 안인가
- [ ] 1px 그리드가 지켜졌는가 (안티에일리어싱/반픽셀 없음)
- [ ] 실루엣만으로 의미가 읽히는가 (아이콘: 12~16px 실제 크기에서 확인)
- [ ] 기존 프랜차이즈 UI를 떠올리게 하지 않는가
- [ ] 텍스트/워터마크가 없는가

### 7.2 세트 검수 (같은 역할군끼리)

- [ ] 프레임 3종의 테두리 색·라운드 반경이 같은가
- [ ] button과 button_on이 형태 동일·색만 다른가 (겹쳐 보면 픽셀이 일치해야 한다)
- [ ] 탭 아이콘 3종의 선 두께·외곽선 색·패딩이 같은가
- [ ] 아이콘끼리 실루엣이 겹치지 않는가 (sparkle vs fx_spark은 크기 외 동일해도 됨)

### 7.3 화면 검수 (적용 후)

- [ ] 9슬라이스를 카드(260px)와 버튼(40px)으로 늘렸을 때 변이 깨지지 않는가
- [ ] 밝은 바탕화면과 어두운 바탕화면 양쪽에서 카드가 읽히는가
- [ ] 크리처 도트와 나란히 있을 때 같은 세계로 보이는가
- [ ] 버튼 on/off가 한눈에 구분되는가

## 8. 생산 순서

1. **1차 파일럿 (3장)**: `ui_frame_card` → `ui_badge` → `ui_icon_berry`.
   이 3장을 실제 화면에 적용해보고 톤을 확정한다. 톤이 틀리면 여기서 갈아엎는다 -
   15장을 다 만들고 갈아엎는 것보다 싸다.
2. **2차 (프레임 완성)**: `ui_frame_button` → 색 치환으로 `_on` → `ui_frame_cell`
3. **3차 (아이콘)**: 탭 3형제 → sparkle → heart → sleep
4. **4차 (연출, 선택)**: fx 3종

## 9. 승인 기준

- §7.1 + §7.2 전부 통과
- 1차 파일럿은 §7.3까지 통과해야 2차 진행
- 원장의 status를 approved로 바꾼 것만 정식 파일명을 가진다

## 10. 적용 절차 (승인 후 코드 교체)

교체 지점은 `UIKit.cs`에 모여 있다:

| 지점 | 현재 | 교체 후 |
|---|---|---|
| `UIKit.Panel` | 단색 Image + Outline | `ui_frame_card` sliced Image |
| `UIKit.Button` | 단색 Image | `ui_frame_button`, 활성 시 `_on` 스왑 |
| 도감 폼 칸 | 단색 Image | `ui_frame_cell` + 수집 색 오버레이 |
| 배지 | Panel 재사용 | `ui_badge` 단독 Image |
| 텍스트 옆 수치 | 텍스트만 | 아이콘 Image + 텍스트 |

로드 방식: `Resources` 폴더를 쓰지 않고 `UIKit`에 스프라이트 참조를 주입하는
ScriptableObject(`UITheme`)를 하나 만들어 `DeskmonDB`처럼 관리한다 (적용 시점에 구현).

## 11. 제외 (만들지 않는다)

- 도트 폰트 - 한글 수천 자를 감당할 수 없다. 텍스트는 OS 폰트(맑은 고딕)
- 크리처 아이콘 - 크리처 도트가 가방/도감 줄에서 그대로 아이콘을 겸한다
- 각인 문양 - 인식기 좌표에서 GL로 그린다. 이미지로 만들면 판정 기준과 표시가 어긋난다
