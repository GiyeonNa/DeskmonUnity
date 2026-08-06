using System.Runtime.InteropServices;
using UnityEngine;

namespace Deskmon.Native
{
    /// <summary>
    /// 포커스와 무관하게 키를 읽는다.
    ///
    /// 왜 필요한가: 이 앱의 창은 WS_EX_NOACTIVATE + 클릭통과 상태로 뜬다. 즉 포커스를
    /// 받지 못하는데, Unity의 Input.GetKeyDown은 포커스 있는 창에만 전달되는 키 메시지를
    /// 본다. 그래서 에디터(게임 뷰가 포커스를 가짐)에서는 되던 단축키가 빌드에서는
    /// 전혀 발화하지 않는다 - 눌러도 아무 반응이 없는 것처럼 보인다.
    ///
    /// Killswitch가 같은 이유로 GetAsyncKeyState 폴링을 쓰고 있었다. 이 클래스는 그
    /// 방식을 단축키 전반이 쓸 수 있게 뽑아낸 것이다.
    ///
    /// 주의: 이 창이 포커스를 못 받아도 키는 읽힌다. 즉 다른 앱에서 타이핑하는 중에도
    /// 반응한다. 전역 단축키로 쓸 키는 평범한 글자를 피하고 F키나 조합키를 쓴다.
    /// </summary>
    public static class GlobalKey
    {
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);

        // 이전 프레임의 눌림 상태. GetAsyncKeyState는 "지금 눌려 있는가"만 알려주므로
        // "이번 프레임에 눌렸는가"(GetKeyDown)를 만들려면 직접 기억해야 한다.
        static readonly System.Collections.Generic.Dictionary<KeyCode, bool> _prev
            = new System.Collections.Generic.Dictionary<KeyCode, bool>();

        // 프레임당 1회만 갱신하기 위한 것 - 아래 Down() 주석 참고.
        static readonly System.Collections.Generic.Dictionary<KeyCode, int> _frame
            = new System.Collections.Generic.Dictionary<KeyCode, int>();
        static readonly System.Collections.Generic.Dictionary<KeyCode, bool> _down
            = new System.Collections.Generic.Dictionary<KeyCode, bool>();

        /// <summary>지금 눌려 있는가.</summary>
        public static bool Held(KeyCode key)
        {
#if UNITY_EDITOR
            // 에디터에서는 게임 뷰가 포커스를 가지므로 Unity 입력이 정상 동작한다.
            // 폴링을 쓰면 에디터 창 밖에서 누른 키까지 잡혀 오히려 불편하다.
            return Input.GetKey(key);
#else
            int vk = ToVirtualKey(key);
            return vk != 0 && (GetAsyncKeyState(vk) & 0x8000) != 0;
#endif
        }

        /// <summary>
        /// 이번 프레임에 눌리기 시작했는가. Input.GetKeyDown의 대체.
        ///
        /// 같은 키를 한 프레임에 두 번 물어봐도 같은 답이 나오도록 프레임 번호를
        /// 함께 기억한다. 그렇게 하지 않으면 첫 호출이 상태를 갱신해버려
        /// 두 번째 호출이 항상 false가 된다.
        /// </summary>
        public static bool Down(KeyCode key)
        {
#if UNITY_EDITOR
            return Input.GetKeyDown(key);
#else
            if (!_frame.TryGetValue(key, out int f) || f != Time.frameCount)
            {
                _frame[key] = Time.frameCount;
                _prev.TryGetValue(key, out bool wasHeld);
                bool nowHeld = Held(key);
                _prev[key] = nowHeld;
                _down[key] = nowHeld && !wasHeld;
            }
            return _down.TryGetValue(key, out bool d) && d;
#endif
        }

        // ── 마우스 ──
        // 클릭통과가 켜져 있는 동안에는 이 창에 마우스 메시지가 오지 않으므로
        // Input.GetMouseButton도 멈춘다. 키와 같은 이유로 직접 읽는다.

        const int VK_LBUTTON = 0x01;
        const int VK_RBUTTON = 0x02;

        static bool _mouseNow, _mouseWas;
        static int _mouseFrame = -1;

#if !UNITY_EDITOR
        // 폴링은 빌드에서만 쓴다 - 에디터 분기는 Unity 입력을 그대로 쓰므로
        // 선언 자체를 빼야 "쓰지 않는 필드" 경고가 나지 않는다.
        static bool _rNow, _rWas;
        static int _rFrame = -1;
#endif

        /// <summary>이번 프레임에 우클릭이 눌렸는가. 공놀이 던지기 등 보조 조작용.</summary>
        public static bool MouseRightDown()
        {
#if UNITY_EDITOR
            return Input.GetMouseButtonDown(1);
#else
            if (_rFrame != Time.frameCount)
            {
                _rFrame = Time.frameCount;
                _rWas = _rNow;
                _rNow = (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0;
            }
            return _rNow && !_rWas;
#endif
        }

        /// <summary>
        /// 프레임당 한 번만 실제 상태를 읽는다.
        ///
        /// Down/Up을 각자 갱신하게 두면 한 프레임에 둘 다 호출됐을 때 앞의 호출이
        /// 이전 상태를 덮어써서 뒤의 호출이 항상 false가 된다. 눌렀는데 획이
        /// 시작되지 않는 종류의 버그가 여기서 나온다.
        /// </summary>
        static void SampleMouse()
        {
            if (_mouseFrame == Time.frameCount) return;
            _mouseFrame = Time.frameCount;
            _mouseWas = _mouseNow;
            _mouseNow = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
        }

        /// <summary>왼쪽 버튼이 눌려 있는가.</summary>
        public static bool MouseHeld()
        {
#if UNITY_EDITOR
            return Input.GetMouseButton(0);
#else
            SampleMouse();
            return _mouseNow;
#endif
        }

        /// <summary>이번 프레임에 눌렸는가.</summary>
        public static bool MouseDown()
        {
#if UNITY_EDITOR
            return Input.GetMouseButtonDown(0);
#else
            SampleMouse();
            return _mouseNow && !_mouseWas;
#endif
        }

        /// <summary>이번 프레임에 떼어졌는가.</summary>
        public static bool MouseUp()
        {
#if UNITY_EDITOR
            return Input.GetMouseButtonUp(0);
#else
            SampleMouse();
            return !_mouseNow && _mouseWas;
#endif
        }

        /// <summary>
        /// KeyCode -> Win32 가상 키 코드.
        /// 필요한 것만 옮긴다 - 전부 매핑하면 표만 커지고 쓰지 않는다.
        /// </summary>
        static int ToVirtualKey(KeyCode key)
        {
            // F1~F12는 연속이라 계산으로 처리한다
            if (key >= KeyCode.F1 && key <= KeyCode.F12)
                return 0x70 + (key - KeyCode.F1);

            // 숫자 1~9, 0
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
                return 0x30 + (key - KeyCode.Alpha0);

            // 영문자
            if (key >= KeyCode.A && key <= KeyCode.Z)
                return 0x41 + (key - KeyCode.A);

            switch (key)
            {
                case KeyCode.LeftControl:
                case KeyCode.RightControl: return 0x11;   // VK_CONTROL
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:     return 0x12;   // VK_MENU
                case KeyCode.LeftShift:
                case KeyCode.RightShift:   return 0x10;   // VK_SHIFT
                case KeyCode.Escape:       return 0x1B;
                case KeyCode.Space:        return 0x20;
                case KeyCode.Return:       return 0x0D;
                case KeyCode.LeftArrow:    return 0x25;
                case KeyCode.UpArrow:      return 0x26;
                case KeyCode.RightArrow:   return 0x27;
                case KeyCode.DownArrow:    return 0x28;
                default:                   return 0;
            }
        }
    }
}
