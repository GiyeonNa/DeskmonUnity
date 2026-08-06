using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Deskmon.Native
{
    /// <summary>
    /// 비상 종료 장치.
    ///
    /// 왜 필요한가: 이 앱은 WS_EX_NOACTIVATE + WS_EX_TOOLWINDOW + 클릭통과 상태로 뜬다.
    /// 즉 포커스를 못 받고, Alt+Tab/작업표시줄에도 안 뜬다. 이 상태에서 Unity의
    /// Input.GetKeyDown은 절대 발화하지 않는다 (포커스 있는 창에만 키가 간다).
    /// 렌더가 잘못돼 화면이 검게 덮이기라도 하면 작업 관리자 외에는 끌 방법이 없다.
    ///
    /// 그래서 종료는 반드시 OS 레벨 전역 훅으로 잡는다:
    ///   1. RegisterHotKey — Ctrl+Alt+Q. 포커스와 무관하게 WM_HOTKEY가 온다.
    ///   2. GetAsyncKeyState 폴링 — 핫키 등록이 실패해도 동작하는 이중 안전장치.
    ///   3. 워치독 — 시작 후 N초 안에 정상 렌더가 확인되지 않으면 스스로 종료.
    ///
    /// 항상 씬에 있어야 한다. 이건 디버그 기능이 아니라 안전장치다.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class Killswitch : MonoBehaviour
    {
        [Header("전역 종료 핫키")]
        [Tooltip("포커스와 무관하게 동작한다. 기본 Ctrl+Alt+Q")]
        public bool enableHotkey = true;

        [Header("워치독")]
        [Tooltip("true면 아래 시간 안에 살아있음이 확인되지 않을 때 자동 종료한다.")]
        public bool enableWatchdog = true;

        [Tooltip("이 시간(초) 안에 Heartbeat()가 호출되지 않으면 종료한다.")]
        public float watchdogTimeout = 10f;

        [Header("검은 화면 감지")]
        [Tooltip("투명 창 적용이 실패하면(=불투명 검은 창) 이 시간 뒤 자동 종료한다. 0이면 비활성.")]
        public float blackScreenGrace = 8f;

        // ── Win32 ──
        const int MOD_ALT = 0x0001;
        const int MOD_CONTROL = 0x0002;
        const int MOD_NOREPEAT = 0x4000;
        const int VK_Q = 0x51;
        const int HOTKEY_ID = 0xDE5C;   // 임의의 고유 ID

        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);

        const int VK_CONTROL = 0x11;
        const int VK_MENU = 0x12;    // Alt

#if !UNITY_EDITOR
        // 핫키 등록은 빌드에서만 한다. 에디터에서는 선언 자체를 빼야 "쓰지 않는 필드"
        // 경고가 나지 않는다.
        bool _hotkeyRegistered;
#endif
        float _lastHeartbeat;
        bool _quitting;

        /// <summary>워치독에게 "정상 동작 중"을 알린다. 렌더 루프에서 매 프레임 호출.</summary>
        public void Heartbeat() => _lastHeartbeat = Time.realtimeSinceStartup;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            _lastHeartbeat = Time.realtimeSinceStartup;
        }

        void Start()
        {
#if !UNITY_EDITOR
            if (enableHotkey)
            {
                // hWnd=0으로 등록하면 이 스레드에 WM_HOTKEY가 온다. Unity는 메시지 루프를
                // 직접 노출하지 않으므로, 등록은 "다른 앱이 이 조합을 못 뺏게" 하는 의미가
                // 크고 실제 감지는 아래 GetAsyncKeyState 폴링이 담당한다.
                _hotkeyRegistered = RegisterHotKey(IntPtr.Zero, HOTKEY_ID, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_Q);
                if (!_hotkeyRegistered)
                    Debug.LogWarning("[Killswitch] 전역 핫키 등록 실패 — 폴링으로만 감지합니다.");
            }
            Debug.Log("[Killswitch] 활성. 종료: Ctrl+Alt+Q");
#endif
        }

        void Update()
        {
            if (_quitting) return;

#if !UNITY_EDITOR
            // 이중 안전장치: 포커스가 없어도 키 상태를 직접 읽는다.
            if (enableHotkey && Down(VK_CONTROL) && Down(VK_MENU) && Down(VK_Q))
            {
                Debug.Log("[Killswitch] Ctrl+Alt+Q 감지 → 종료합니다.");
                Quit();
                return;
            }
#endif

            // 에디터에서는 포커스가 있으므로 일반 입력도 받아준다.
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.Q))
            {
                Quit();
                return;
            }

            if (enableWatchdog && Time.realtimeSinceStartup - _lastHeartbeat > watchdogTimeout)
            {
                Debug.LogError($"[Killswitch] 워치독 발동 — {watchdogTimeout}초간 정상 신호 없음. 자동 종료합니다.");
                Quit();
                return;
            }

#if !UNITY_EDITOR
            // 검은 화면 방어: 투명 적용이 실패했다면 이 창은 화면을 덮은 불투명 검은 창이다.
            // 사용자가 아무것도 못 보고 아무것도 못 누르는 상태이므로 스스로 물러난다.
            // (Update는 렌더가 깨져도 계속 돌기 때문에 하트비트만으로는 이걸 못 잡는다.)
            if (blackScreenGrace > 0f
                && Time.realtimeSinceStartup > blackScreenGrace
                && !WindowController.IsApplied)
            {
                Debug.LogError("[Killswitch] 투명 창 적용 실패가 확인됐습니다 " +
                               $"({blackScreenGrace}초 경과). 검은 화면으로 남지 않도록 종료합니다.");
                Quit();
            }
#endif
        }

        static bool Down(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

        void Quit()
        {
            if (_quitting) return;
            _quitting = true;
            Cleanup();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Cleanup()
        {
#if !UNITY_EDITOR
            if (_hotkeyRegistered)
            {
                UnregisterHotKey(IntPtr.Zero, HOTKEY_ID);
                _hotkeyRegistered = false;
            }
#endif
        }

        void OnApplicationQuit() => Cleanup();
        void OnDestroy() => Cleanup();
    }
}
