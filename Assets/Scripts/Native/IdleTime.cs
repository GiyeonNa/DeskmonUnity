using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Deskmon.Native
{
    /// <summary>
    /// OS 유휴 시간. Electron powerMonitor.getSystemIdleTime()의 대체.
    ///
    /// 주의: GetLastInputInfo가 반환하는 dwTime과 GetTickCount는 모두 32비트 ms 카운터로
    /// 약 49.7일마다 래핑된다. unchecked 뺄셈으로 처리하면 래핑을 넘어서도 값이 정확하다.
    /// </summary>
    public static class IdleTime
    {
        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
        [DllImport("kernel32.dll")] static extern uint GetTickCount();

        /// <summary>마지막 입력 이후 경과 초. 실패 시 0.</summary>
        public static float Seconds()
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
            return 0f;
#else
            var lii = new LASTINPUTINFO();
            lii.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
            if (!GetLastInputInfo(ref lii)) return 0f;

            // 32비트 래핑 안전 뺄셈
            uint delta = unchecked(GetTickCount() - lii.dwTime);
            return delta / 1000f;
#endif
        }

        /// <summary>
        /// 작업 중 판정. data.js BOOST.idleSec(60) 기준 — index.html의 isWorking()과 동일.
        /// </summary>
        public static bool IsWorking(float idleSecThreshold = 60f)
        {
            return Seconds() < idleSecThreshold;
        }
    }
}
