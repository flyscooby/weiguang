using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

static partial class Native {
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    public struct Physical { public IntPtr Handle; [MarshalAs(UnmanagedType.ByValTStr, SizeConst=128)] public string Name; }
    public delegate bool EnumProc(IntPtr monitor, IntPtr dc, IntPtr rect, IntPtr data);
    [DllImport("user32.dll")] public static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, EnumProc callback, IntPtr data);
    [DllImport("dxva2.dll", SetLastError=true)] public static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr monitor, out uint count);
    [DllImport("dxva2.dll", CharSet=CharSet.Unicode, SetLastError=true)] public static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr monitor, uint count, [Out] Physical[] monitors);
    [DllImport("dxva2.dll")] public static extern bool DestroyPhysicalMonitor(IntPtr handle);
    [DllImport("dxva2.dll", SetLastError=true)] public static extern bool GetMonitorBrightness(IntPtr handle, out uint min, out uint value, out uint max);
    [DllImport("dxva2.dll", SetLastError=true)] public static extern bool SetMonitorBrightness(IntPtr handle, uint value);
    [DllImport("dxva2.dll", SetLastError=true)] public static extern bool GetMonitorCapabilities(IntPtr handle, out uint caps, out uint temperatures);
    [DllImport("dxva2.dll", SetLastError=true)] public static extern bool GetMonitorColorTemperature(IntPtr handle, out uint temperature);
    [DllImport("dxva2.dll", SetLastError=true)] public static extern bool GetMonitorRedGreenOrBlueGain(IntPtr handle, int channel, out uint min, out uint value, out uint max);
    [DllImport("dxva2.dll", SetLastError=true)] public static extern bool SetMonitorRedGreenOrBlueGain(IntPtr handle, int channel, uint value);
    [DllImport("dxva2.dll", SetLastError=true)] public static extern bool GetVCPFeatureAndVCPFeatureReply(IntPtr handle, byte code, out uint type, out uint value, out uint max);
    [DllImport("dxva2.dll", SetLastError=true)] public static extern bool SetVCPFeature(IntPtr handle, byte code, uint value);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string className,string title);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr handle,int command);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr handle);
}

sealed partial class Display : IDisposable {
    public string Name, WmiPath;
    public IntPtr Handle;
    public uint Min, Max, Current;
    public bool Vcp;
    public string Mode="手动";
    public int Percent { get { return Max > Min ? (int)Math.Round(100.0 * (Current-Min)/(Max-Min)) : 0; } }
    public override string ToString() { return Name; }
    public void Read() {
        if (WmiPath != null) {
            using (var search = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightness"))
            using (var rows = search.Get()) {
                foreach (ManagementObject row in rows) using(row) {
                    if ((string)row["InstanceName"] == WmiPath) { Current=Convert.ToUInt32(row["CurrentBrightness"]); return; }
                }
            }
            throw new Exception("屏幕已断开，请重新检测。");
        }
        uint a,b,c,t;
        bool ok = Vcp ? Native.GetVCPFeatureAndVCPFeatureReply(Handle, 0x10, out t, out b, out c) : Native.GetMonitorBrightness(Handle, out a, out b, out c);
        if (!ok) throw new Exception("无法读取显示器亮度，请检查连接与 DDC/CI 设置。");
        Current=b;
    }
    public void SetRaw(uint value) {
        if (WmiPath != null) {
            using(var search = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightnessMethods"))
            using(var rows=search.Get()) {
                foreach(ManagementObject row in rows) using(row) {
                    if((string)row["InstanceName"] != WmiPath) continue;
                    row.InvokeMethod("WmiSetBrightness", new object[] { (uint)1, (byte)value }); return;
                }
            }
            throw new Exception("屏幕已断开，请重新检测。");
        }
        bool ok=Vcp ? Native.SetVCPFeature(Handle, 0x10, value) : Native.SetMonitorBrightness(Handle, value);
        if(!ok) throw new Exception("显示器未接受亮度设置。请开启显示器菜单中的 DDC/CI；部分 HDR 或预设模式可能限制调节。");
    }
    public void Set(int percent) {
        uint raw=Min+(uint)Math.Round((Max-Min)*Math.Max(0,Math.Min(100,percent))/100.0);
        SetRaw(raw); System.Threading.Thread.Sleep(100); Read();
    }
    bool disposed;
    public void Dispose() { if(!disposed && WmiPath==null) Native.DestroyPhysicalMonitor(Handle); disposed=true; }
    public static List<Display> Find(out string notes) {
        var found=new List<Display>(); var messages=new List<string>();
        Native.EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,delegate(IntPtr monitor,IntPtr dc,IntPtr rect,IntPtr data) {
            uint count;
            if(!Native.GetNumberOfPhysicalMonitorsFromHMONITOR(monitor,out count) || count==0) return true;
            var physical=new Native.Physical[count];
            if(!Native.GetPhysicalMonitorsFromHMONITOR(monitor,count,physical)) return true;
            int physicalIndex=0;
            foreach(var p in physical) {
                uint min,value,max,type; bool vcp=false;
                bool ok=Native.GetMonitorBrightness(p.Handle,out min,out value,out max);
                if(!ok) { min=0; vcp=true; ok=Native.GetVCPFeatureAndVCPFeatureReply(p.Handle,0x10,out type,out value,out max); }
                if(ok && max>min && value>=min && value<=max) found.Add(new Display { Name=p.Name+" · 外接屏", Identity=Native.DisplayIdentity(monitor)+"/"+physicalIndex, Handle=p.Handle,Min=min,Max=max,Current=value,Vcp=vcp });
                else { Native.DestroyPhysicalMonitor(p.Handle); messages.Add(p.Name+"：未提供可用的亮度控制。"); }
                physicalIndex++;
            }
            return true;
        },IntPtr.Zero);
        try {
            using(var search=new ManagementObjectSearcher("root\\WMI","SELECT * FROM WmiMonitorBrightness WHERE Active = TRUE"))
            using(var rows=search.Get()) foreach(ManagementObject row in rows) using(row) {
                found.Add(new Display { Name="内置屏幕", Identity=(string)row["InstanceName"], WmiPath=(string)row["InstanceName"], Min=0, Max=100, Current=Convert.ToUInt32(row["CurrentBrightness"]) });
            }
        } catch(ManagementException) {} catch(UnauthorizedAccessException) { messages.Add("无法访问内置屏幕亮度接口。"); }
        notes=String.Join("\r\n",messages.ToArray()); return found;
    }
}

static class Program {
    [STAThread] static void Main(string[] args) {
        Native.SetProcessDPIAware();
        if(args.Length>=2 && args[0]=="--probe") {
            var report=new StringBuilder(); List<Display> monitors=null;
            try {
                string notes; monitors=Display.Find(out notes); report.AppendLine("Supported displays: "+monitors.Count); report.AppendLine(notes);
                foreach(var d in monitors) {
                    report.AppendLine(d.Name+" | brightness="+d.Percent+"% | raw="+d.Current+" | range="+d.Min+".."+d.Max+" | mode="+(d.WmiPath!=null?"WMI":d.Vcp?"VCP":"DDC"));
                    uint caps,temps,temp; if(d.WmiPath==null) { report.AppendLine("Capabilities: "+Native.GetMonitorCapabilities(d.Handle,out caps,out temps)+" flags="+caps+" temperatures="+temps); report.AppendLine("Color temperature: "+Native.GetMonitorColorTemperature(d.Handle,out temp)+" value="+temp); }
                    if(d.WmiPath==null) for(int channel=0;channel<3;channel++) { uint lo,gain,hi; report.AppendLine("RGB "+channel+": "+Native.GetMonitorRedGreenOrBlueGain(d.Handle,channel,out lo,out gain,out hi)+" "+lo+"/"+gain+"/"+hi); }
                    if(args.Length>2 && args[2]=="--verify") { uint before=d.Current; d.SetRaw(before); System.Threading.Thread.Sleep(150); d.Read(); report.AppendLine("Same-value write/read: "+(d.Current==before?"PASS":"MISMATCH")+" ("+d.Current+")"); }
                }
            } catch(Exception ex) { report.AppendLine("ERROR: "+ex); }
            finally { if(monitors!=null) foreach(var d in monitors) d.Dispose(); }
            File.WriteAllText(args[1],report.ToString(),Encoding.UTF8); return;
        }
        bool first;
        using(var singleton=new System.Threading.Mutex(true,"Local\\ScreenComfortControl",out first)) {
            if(!first) { IntPtr window=Native.FindWindow(null,"微光 · 屏幕舒适控制"); if(window!=IntPtr.Zero) { Native.ShowWindow(window,9); Native.ShowWindow(window,5); Native.SetForegroundWindow(window); } return; }
            try { Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new BrightnessWindow()); }
            finally { singleton.ReleaseMutex(); }
        }
    }
}
