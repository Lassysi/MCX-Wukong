using System;
using DqjCombat;
internal static class HostProjection37Contract
{
    private static void Require(bool ok,string why){if(!ok)throw new Exception(why);}
    private static void Near(double actual,double expected,string why){Require(Math.Abs(actual-expected)<.00002,why);}
    public static void Main()
    {
        float h;Require(HostProjection.TryHorizontal(70,3840,2160,out h),"16:9 valid");Near(h,102.44785777798826,"measured90 mismatch corrected");
        Require(HostProjection.TryHorizontal(70,1920,1080,out h),"scaled viewport");Near(h,102.44785777798826,"resolution scaling invariant");
        Require(HostProjection.TryHorizontal(70,1024,768,out h),"4:3");Near(2*Math.Atan(Math.Tan(h*Math.PI/360)/(4.0/3))*180/Math.PI,70,"vertical roundtrip");
        Require(!HostProjection.TryHorizontal(double.NaN,3840,2160,out h)&&!HostProjection.TryHorizontal(70,0,2160,out h)&&!HostProjection.TryHorizontal(180,3840,2160,out h),"invalid input must not change owner");
        float current=90;int writes=0;var lease=new NativeFloatLease(()=>current,v=>{current=v;writes++;});
        Require(lease.Apply(102.44786f),"first lease");Near(lease.Original,90,"save native value");
        int before=writes;lease.Apply(102.44786f);Require(writes==before,"unchanged value does not rewrite");
        lease.Apply(110);Near(lease.Original,90,"FOV changes retain original");lease.Restore();Near(current,90,"menu/disconnect restore");
        before=writes;lease.Restore();Require(writes==before&&!lease.Active,"repeat restore idempotent");
        lease.Apply(102);current=97;lease.Restore();Near(current,97,"external owner change retained");
        Require(!lease.Apply(float.NaN)&&!lease.Active,"invalid desired no lease");
        current=float.NaN;Require(!lease.Apply(102)&&!lease.Active,"invalid native state no write");
        current=85;lease.Apply(100);lease.Restore();Near(current,85,"new lifecycle captures new native value");
        Console.WriteLine("PASS11: projection/scaling/roundtrip/invalid/lease/update/idempotence/restore/external-owner/native-invalid/new-lifecycle");
    }
}
