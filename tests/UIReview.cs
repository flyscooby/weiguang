using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
class ButtonReview : SoftButton {
    public void Key(Keys key) { OnKeyDown(new KeyEventArgs(key)); OnKeyUp(new KeyEventArgs(key)); }
}
static class UIReview {
    static object Field(object o,string n) { return o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o); }
    static void Assert(bool value,string text) { if(!value) throw new Exception(text); }
    static void EnableAll(Control c) { c.Enabled=true; var handle=c.Handle; foreach(Control child in c.Controls) EnableAll(child); }
    [STAThread] static void Main(string[] args) {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        using(var button=new ButtonReview()) { int calls=0; button.Click+=delegate { calls++; }; button.Key(Keys.Space); button.Key(Keys.Enter); Assert(calls==2,"Keyboard activation failed"); button.Enabled=false; button.Key(Keys.Space); Assert(calls==2,"Disabled button activated"); }
        using(var input=new PercentInput()) {
            input.Value=80; Assert(input.Value==80,"Value readback"); input.Value=120; Assert(input.Value==100,"Upper bound"); input.Value=-1; Assert(input.Value==0,"Lower bound");
            input.Value=80; ((TextBox)input.Controls[0]).Text="abc"; Assert(input.Value==80,"Invalid text fallback"); ((TextBox)input.Controls[0]).Text="999"; Assert(input.Value==100,"Pasted input clamp");
        }
        using(var form=new BrightnessWindow()) {
            form.GetType().GetField("syncing",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(form,true);
            var picker=(DisplayPicker)Field(form,"screens"); picker.Items.Add("Generic PnP Monitor · 外接屏"); typeof(DisplayPicker).GetField("selected",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(picker,0);
            ((SmoothSlider)Field(form,"slider")).Value=80; ((PercentInput)Field(form,"exact")).Value=80; ((Label)Field(form,"number")).Text="80%";
            ((SmoothSlider)Field(form,"warmSlider")).Value=6500; ((Label)Field(form,"warmNumber")).Text="6500 K";
            ((SmoothSlider)Field(form,"contrastSlider")).Value=80; ((Label)Field(form,"contrastNumber")).Text="80%";
            ((Label)Field(form,"blueNumber")).Text="0%"; ((SoftButton)Field(form,"restoreButton")).Selected=true;
            ((Label)Field(form,"status")).Text="界面预览 · 显示器名称与数值仅用于展示。";
            EnableAll(form); form.CreateControl();
            using(var bitmap=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size)); bitmap.Save(args[0]); }
            // Check button corners without reading the actual desktop or changing display settings.
            var mode=(SoftButton)Field(form,"eyeButton"); using(var bitmap=new Bitmap(mode.Width,mode.Height)) { mode.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size)); Assert(bitmap.GetPixel(0,0).ToArgb()==Palette.Background.ToArgb(),"Button corner background mismatch"); }
            ((NotifyIcon)Field(form,"tray")).Dispose();
        }
        Console.WriteLine("PASS: keyboard activation, disabled state, numeric bounds, invalid input, button corner rendering; preview rendered without display writes.");
    }
}
