using System;

namespace DqjCombat
{
    internal static class HostProjection
    {
        internal static bool TryHorizontal(double vertical,int width,int height,out float horizontal)
        {
            horizontal=0;
            if(width<=0||height<=0||double.IsNaN(vertical)||double.IsInfinity(vertical)||vertical<=1||vertical>=179)return false;
            double value=2*Math.Atan(Math.Tan(vertical*Math.PI/360)*width/height)*180/Math.PI;
            if(double.IsNaN(value)||double.IsInfinity(value)||value<=1||value>=179)return false;
            horizontal=(float)value;return true;
        }
    }
    // Restore a borrowed scalar only if it still contains our last assignment.
    // This protects native UI/other owners after a normal handoff.
    internal sealed class NativeFloatLease
    {
        private readonly Func<float> read;
        private readonly Action<float> write;
        private float original,last;
        internal bool Active {get;private set;}
        internal float Original {get{return original;}}
        internal NativeFloatLease(Func<float> read,Action<float> write){this.read=read;this.write=write;}
        internal bool Apply(float value)
        {
            if(!Valid(value))return false;
            float current=read();if(!Valid(current))return false;
            if(!Active)original=current;
            if(Math.Abs(current-value)>.0001f)write(value);
            last=value;Active=true;return true;
        }
        internal void Restore()
        {
            if(!Active)return;
            try{float current=read();if(Valid(current)&&Math.Abs(current-last)<=.001f&&Math.Abs(current-original)>.001f)write(original);}
            finally{Active=false;}
        }
        private static bool Valid(float value){return !float.IsNaN(value)&&!float.IsInfinity(value)&&value>1&&value<179;}
    }
}
