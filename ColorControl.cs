using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;

static partial class Native {
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct MonitorInfo { public int Size; public int L,T,R,B,WL,WT,WR,WB; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string Device; }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct DisplayDevice { public int Size; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string Name; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string Description; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string ID; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string Key; }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr h,ref MonitorInfo info);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool EnumDisplayDevices(string device,uint index,ref DisplayDevice result,uint flags);
    [DllImport("dxva2.dll")] public static extern bool GetMonitorContrast(IntPtr h,out uint min,out uint value,out uint max);
    [DllImport("dxva2.dll")] public static extern bool SetMonitorContrast(IntPtr h,uint value);
    public static string DisplayIdentity(IntPtr h) {
        var info=new MonitorInfo { Size=Marshal.SizeOf(typeof(MonitorInfo)) };
        if(!GetMonitorInfo(h,ref info)) throw new Exception("无法识别显示器，已停止修改。");
        var device=new DisplayDevice { Size=Marshal.SizeOf(typeof(DisplayDevice)) };
        if(EnumDisplayDevices(info.Device,0,ref device,1) && !String.IsNullOrWhiteSpace(device.ID)) return device.ID;
        return info.Device;
    }
}

public class ScreenSnapshot {
    public string Identity;
    public uint Brightness;
    public uint[] Gains;
    public bool HasContrast,HasPreset;
    public uint Contrast,Preset;
}
public class ComfortProfile {
    public int Brightness=50, Warmth=5000, Blue=10, Contrast=-1;
    public ComfortProfile Copy() { return (ComfortProfile)MemberwiseClone(); }
    public void Validate() { Brightness=Math.Max(0,Math.Min(100,Brightness)); Warmth=Math.Max(3500,Math.Min(6500,Warmth)); Blue=Math.Max(0,Math.Min(40,Blue)); Contrast=Math.Max(-1,Math.Min(100,Contrast)); }
}
public class ComfortSettings {
    public ComfortProfile Eye=new ComfortProfile();
    public ComfortProfile Night=new ComfortProfile { Brightness=20,Warmth=3800,Blue=20 };
    public static string FilePath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"comfort-settings.xml");
    public static ComfortSettings Load() {
        try { using(var f=File.OpenRead(FilePath)) { var s=(ComfortSettings)new XmlSerializer(typeof(ComfortSettings)).Deserialize(f); if(s.Eye==null || s.Night==null) return new ComfortSettings(); s.Eye.Validate(); s.Night.Validate(); return s; } } catch { return new ComfortSettings(); }
    }
    public void Save() { using(var f=File.Create(FilePath+".tmp")) new XmlSerializer(typeof(ComfortSettings)).Serialize(f,this); if(File.Exists(FilePath)) File.Replace(FilePath+".tmp",FilePath,null); else File.Move(FilePath+".tmp",FilePath); }
}

sealed partial class Display {
    public string Identity;
    public bool HasRgb,HasContrast;
    public uint[] Gains=new uint[3], GainMin=new uint[3],GainMax=new uint[3];
    public uint ContrastMin,ContrastMax,ContrastValue;
    public int Warmth=6500,Blue;
    public ScreenSnapshot Baseline;
    public bool HasChanges { get { return Baseline!=null; } }
    public int ContrastPercent { get { return ContrastMax>ContrastMin?(int)Math.Round(100.0*(ContrastValue-ContrastMin)/(ContrastMax-ContrastMin)):0; } }
    string RecoveryPath {
        get { using(var hash=SHA256.Create()) { string key=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Identity))).Replace("-",""); return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"recovery",key+".xml"); } }
    }
    public void InitializeColor() {
        HasRgb=WmiPath==null;
        if(HasRgb) for(int i=0;i<3;i++) { if(!Native.GetMonitorRedGreenOrBlueGain(Handle,i,out GainMin[i],out Gains[i],out GainMax[i]) || GainMax[i]<=GainMin[i] || Gains[i]<GainMin[i] || Gains[i]>GainMax[i]) HasRgb=false; }
        HasContrast=WmiPath==null && Native.GetMonitorContrast(Handle,out ContrastMin,out ContrastValue,out ContrastMax) && ContrastMax>ContrastMin;
        if(File.Exists(RecoveryPath)) {
            using(var f=File.OpenRead(RecoveryPath)) Baseline=(ScreenSnapshot)new XmlSerializer(typeof(ScreenSnapshot)).Deserialize(f);
            if(Baseline.Identity!=Identity) { Baseline=null; throw new Exception("恢复记录与显示器不匹配。"); }
            RestoreOriginal();
        }
    }
    public ScreenSnapshot Capture() {
        Read(); var s=new ScreenSnapshot { Identity=Identity,Brightness=Current,HasContrast=HasContrast };
        if(HasRgb) { s.Gains=new uint[3]; for(int i=0;i<3;i++) { uint lo,hi; if(!Native.GetMonitorRedGreenOrBlueGain(Handle,i,out lo,out s.Gains[i],out hi)) throw new Exception("读取颜色失败，未应用新设置。"); } Gains=(uint[])s.Gains.Clone(); }
        if(HasContrast) { uint lo,hi; if(!Native.GetMonitorContrast(Handle,out lo,out s.Contrast,out hi)) throw new Exception("读取对比度失败。"); ContrastValue=s.Contrast; }
        if(WmiPath==null) { uint type,max; s.HasPreset=Native.GetVCPFeatureAndVCPFeatureReply(Handle,0x14,out type,out s.Preset,out max) && s.Preset>0 && s.Preset<=255; }
        return s;
    }
    void SaveBaseline(ScreenSnapshot s) {
        Directory.CreateDirectory(Path.GetDirectoryName(RecoveryPath));
        using(var f=File.Create(RecoveryPath+".tmp")) new XmlSerializer(typeof(ScreenSnapshot)).Serialize(f,s);
        if(File.Exists(RecoveryPath)) throw new Exception("存在未恢复的屏幕记录，请先恢复原始设置。");
        File.Move(RecoveryPath+".tmp",RecoveryPath); Baseline=s;
    }
    public static double[] ColorFactors(int kelvin,int blue) {
        kelvin=Math.Max(3500,Math.Min(6500,kelvin)); blue=Math.Max(0,Math.Min(40,blue));
        // Deliberately approximate warm-white control, not a calibrated CCT measurement.
        double t=(kelvin-3500)/3000.0;
        return new[]{1.0,0.76+0.24*t,(0.52+0.48*t)*(1-blue/100.0)};
    }
    public void ApplyComfort(ComfortProfile profile) {
        profile=profile.Copy(); profile.Validate();
        if(!HasRgb && (profile.Warmth<6500 || profile.Blue>0)) throw new Exception("此屏幕不支持 RGB 调节，未应用该颜色模式。");
        var before=Capture(); bool newBaseline=Baseline==null;
        if(newBaseline) SaveBaseline(before);
        try {
            Set(profile.Brightness);
            if(HasRgb) {
                var f=ColorFactors(profile.Warmth,profile.Blue); var expected=new uint[3];
                for(int i=0;i<3;i++) expected[i]=GainMin[i]+(uint)Math.Round((Baseline.Gains[i]-GainMin[i])*f[i]);
                SetGains(expected);
            }
            if(HasContrast) {
                uint contrast=profile.Contrast<0?Baseline.Contrast:ContrastMin+(uint)Math.Round((ContrastMax-ContrastMin)*profile.Contrast/100.0);
                if(ContrastValue!=contrast && !Native.SetMonitorContrast(Handle,contrast)) throw new Exception("对比度调节失败。");
                uint lo,hi; if(!Native.GetMonitorContrast(Handle,out lo,out ContrastValue,out hi) || ContrastValue!=contrast) throw new Exception("显示器未确认对比度。");
            }
            Read(); if(Math.Abs(Percent-profile.Brightness)>1) throw new Exception("显示器未确认目标亮度。");
            Warmth=profile.Warmth; Blue=profile.Blue;
        } catch(Exception failure) {
            try { RestoreSnapshot(before); if(newBaseline) { File.Delete(RecoveryPath); Baseline=null; } }
            catch(Exception rollback) { throw new Exception("设置未完成，恢复也未完成。请点击恢复原始设置。"+failure.Message+" / "+rollback.Message); }
            throw new Exception("设置未完成，已回退。"+failure.Message);
        }
    }
    void SetGains(uint[] values) {
        if(values==null || values.Length!=3 || !HasRgb) throw new Exception("缺少有效的颜色恢复数据。");
        for(int i=0;i<3;i++) {
            if(values[i]<GainMin[i] || values[i]>GainMax[i]) throw new Exception("颜色参数超出显示器范围。");
            uint lo,now,hi;
            if(!Native.GetMonitorRedGreenOrBlueGain(Handle,i,out lo,out now,out hi)) throw new Exception("颜色读取失败。");
            if(now!=values[i] && !Native.SetMonitorRedGreenOrBlueGain(Handle,i,values[i])) throw new Exception("颜色写入失败。");
        }
        for(int i=0;i<3;i++) { uint lo,hi; if(!Native.GetMonitorRedGreenOrBlueGain(Handle,i,out lo,out Gains[i],out hi) || Gains[i]!=values[i]) throw new Exception("显示器未确认 RGB 颜色值。"); }
    }
    public void RestoreSnapshot(ScreenSnapshot s) {
        if(s.Identity!=Identity || s.Brightness<Min || s.Brightness>Max) throw new Exception("恢复记录无效。");
        if(s.Gains!=null) SetGains(s.Gains);
        if(s.HasPreset) {
            uint type,current,max;
            if(!Native.GetVCPFeatureAndVCPFeatureReply(Handle,0x14,out type,out current,out max)) throw new Exception("无法确认色彩预设。");
            if(current!=s.Preset) {
                if(!Native.SetVCPFeature(Handle,0x14,s.Preset)) throw new Exception("恢复色彩预设失败。");
                if(!Native.GetVCPFeatureAndVCPFeatureReply(Handle,0x14,out type,out current,out max) || current!=s.Preset) throw new Exception("色彩预设恢复未确认。");
            }
        }
        if(s.HasContrast) {
            if(!HasContrast || s.Contrast<ContrastMin || s.Contrast>ContrastMax || !Native.SetMonitorContrast(Handle,s.Contrast)) throw new Exception("恢复对比度失败。");
            uint lo,hi; if(!Native.GetMonitorContrast(Handle,out lo,out ContrastValue,out hi) || ContrastValue!=s.Contrast) throw new Exception("对比度恢复未确认。");
        }
        SetRaw(s.Brightness); Read(); if(Current!=s.Brightness) throw new Exception("亮度恢复未确认。");
        if(s.Gains!=null) for(int i=0;i<3;i++) { uint lo,hi; if(!Native.GetMonitorRedGreenOrBlueGain(Handle,i,out lo,out Gains[i],out hi) || Gains[i]!=s.Gains[i]) throw new Exception("原始颜色恢复未确认。"); }
    }
    public void RestoreOriginal() {
        if(Baseline==null) return; RestoreSnapshot(Baseline); File.Delete(RecoveryPath); Baseline=null; Warmth=6500; Blue=0; Mode="原始";
    }
}
