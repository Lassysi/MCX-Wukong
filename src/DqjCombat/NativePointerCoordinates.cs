using System;

namespace DqjCombat
{
    internal static class NativePointerCoordinates
    {
        // WM_MOUSE coordinates and the source HWND client rectangle share the
        // same DPI space. A render viewport may be smaller (DLSS/render scaling).
        internal static bool TryNormalize(double x,double y,int clientWidth,int clientHeight,out double nx,out double ny)
        {
            nx=ny=0;
            if(clientWidth<=0||clientHeight<=0||double.IsNaN(x)||double.IsNaN(y)||double.IsInfinity(x)||double.IsInfinity(y))return false;
            nx=Math.Max(0,Math.Min(1,x/clientWidth));ny=Math.Max(0,Math.Min(1,y/clientHeight));return true;
        }
    }
}
