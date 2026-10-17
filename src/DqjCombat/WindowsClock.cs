using System;
using System.Runtime.InteropServices;

namespace DqjCombat
{
    internal static class WindowsClock
    {
        // Mono's Stopwatch epoch is process-relative on this loader. Native
        // ReShade and Python use Windows QPC, so read that clock explicitly.
        [DllImport("kernel32.dll")]
        private static extern bool QueryPerformanceCounter(out long counter);
        [DllImport("kernel32.dll")]
        private static extern bool QueryPerformanceFrequency(out long frequency);
        internal static long Nanoseconds()
        {
            long counter,frequency;
            if(!QueryPerformanceCounter(out counter)||!QueryPerformanceFrequency(out frequency)||frequency<=0)
                throw new InvalidOperationException("Windows QPC unavailable");
            return counter/frequency*1000000000L+(counter%frequency)*1000000000L/frequency;
        }
    }
}
