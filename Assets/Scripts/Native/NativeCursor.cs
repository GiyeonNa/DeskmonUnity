using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Deskmon.Native
{
    /// <summary>
    /// OS 커서 위치를 직접 읽는다.
    ///
    /// 왜 필요한가: 클릭통과(WS_EX_TRANSPARENT)가 켜져 있으면 창은 마우스 메시지를
    /// 전혀 받지 못하므로 Input.mousePosition이 멈춘다. 그런데 "커서가 가까이 오면
    /// 클릭통과를 해제"하려면 통과 중에도 커서 위치를 알아야 한다 — 닭과 달걀.
    /// Electron은 setIgnoreMouseEvents(true, {forward:true})의 forward 옵션이
    /// 이 문제를 해결해줬지만, Win32에는 그런 옵션이 없으므로 GetCursorPos로 폴링한다.
    /// </summary>
    public static class NativeCursor
    {
        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int X, Y; }

        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT lpPoint);
        [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);
        [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();

        /// <summary>데스크탑 전역 좌표 (좌상단 원점, Y 아래로 증가).</summary>
        public static Vector2 GetScreenPosition()
        {
            if (GetCursorPos(out POINT p)) return new Vector2(p.X, p.Y);
            return Vector2.zero;
        }

        /// <summary>
        /// Unity Input 좌표계로 변환한 커서 위치 (좌하단 원점, Y 위로 증가).
        /// 에디터에서는 Input.mousePosition을 그대로 쓴다.
        /// </summary>
        public static Vector2 GetPositionInWindow()
        {
#if UNITY_EDITOR
            return Input.mousePosition;
#else
            // UniWinC가 창 기준으로 변환한 커서 위치를 이미 제공한다 (좌하단 원점).
            // 창 핸들을 우리가 따로 들고 있을 필요가 없어 이쪽을 우선 사용한다.
            var uni = WindowController.Uni;
            if (uni != null) return uni.cursorPosition;

            // 폴백: UniWinC가 아직 준비되지 않은 프레임
            if (!GetCursorPos(out POINT p)) return Vector2.zero;
            IntPtr hwnd = GetActiveWindow();
            if (hwnd != IntPtr.Zero) ScreenToClient(hwnd, ref p);

            // Win32는 Y가 아래로 증가, Unity는 위로 증가 → 뒤집는다
            return new Vector2(p.X, Screen.height - p.Y);
#endif
        }
    }
}
