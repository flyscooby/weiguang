using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

static class Palette {
    public static Color Background=Color.FromArgb(15,21,31),Card=Color.FromArgb(26,35,49),Border=Color.FromArgb(47,59,76),Text=Color.FromArgb(239,243,248),Muted=Color.FromArgb(155,171,191),Gold=Color.FromArgb(246,198,112);
    public static GraphicsPath Round(RectangleF r,float radius) { var p=new GraphicsPath(); float d=radius*2; p.AddArc(r.X,r.Y,d,d,180,90); p.AddArc(r.Right-d,r.Y,d,d,270,90); p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); p.AddArc(r.X,r.Bottom-d,d,d,90,90); p.CloseFigure(); return p; }
}
class Card : Panel {
    public Card() { DoubleBuffered=true; BackColor=Palette.Background; }
    protected override void OnPaint(PaintEventArgs e) { e.Graphics.SmoothingMode=SmoothingMode.AntiAlias; using(var path=Palette.Round(new RectangleF(0,0,Width-1,Height-1),18)) using(var brush=new SolidBrush(Palette.Card)) { e.Graphics.FillPath(brush,path); } }
}
class SoftButton : Control {
    public bool Selected; public string Subtitle;
    bool hovering,pressed;
    public SoftButton() { SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true); TabStop=true; AccessibleRole=AccessibleRole.PushButton; Cursor=Cursors.Hand; BackColor=Color.Transparent; ForeColor=Palette.Text; }
    protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Parent is Card?Palette.Card:Palette.Background); }
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        Color fill=Selected?Color.FromArgb(58,49,35):Palette.Card;
        if(hovering && Enabled) fill=Selected?Color.FromArgb(71,58,39):Color.FromArgb(37,49,65);
        if(pressed && Enabled) fill=Selected?Color.FromArgb(83,65,40):Color.FromArgb(44,58,76);
        using(var path=Palette.Round(new RectangleF(1,1,Width-3,Height-3),12)) using(var brush=new SolidBrush(fill)) using(var pen=new Pen(Selected?Palette.Gold:Palette.Border,Selected?2:1)) { e.Graphics.FillPath(brush,path); e.Graphics.DrawPath(pen,path); }
        Color ink=!Enabled?Palette.Muted:Selected?Palette.Gold:Palette.Text;
        if(Subtitle==null) TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        else { TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(18,13,Width-36,30),ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter); using(var small=new Font(Font.FontFamily,9)) TextRenderer.DrawText(e.Graphics,Subtitle,small,new Rectangle(18,48,Width-36,24),Palette.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter); }
        if(Focused && ShowFocusCues) using(var focus=Palette.Round(new RectangleF(4,4,Width-9,Height-9),9)) using(var pen=new Pen(Palette.Gold,1)) e.Graphics.DrawPath(pen,focus);
    }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovering=true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovering=false; pressed=false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if(e.Button==MouseButtons.Left) Focus(); pressed=e.Button==MouseButtons.Left; Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); pressed=false; Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); pressed=false; Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); pressed=false; Invalidate(); }
    protected override void OnKeyDown(KeyEventArgs e) { if(e.KeyCode==Keys.Space || e.KeyCode==Keys.Enter) { pressed=true; Invalidate(); e.Handled=true; e.SuppressKeyPress=true; } base.OnKeyDown(e); }
    protected override void OnKeyUp(KeyEventArgs e) { if(pressed && (e.KeyCode==Keys.Space || e.KeyCode==Keys.Enter)) { pressed=false; Invalidate(); if(Enabled) OnClick(EventArgs.Empty); e.Handled=true; } base.OnKeyUp(e); }
}
class DisplayPicker : Control {
    public readonly List<object> Items=new List<object>();
    int selected=-1; bool hovering;
    public event EventHandler SelectedIndexChanged;
    public int SelectedIndex { get { return selected; } set { int next=value>=0 && value<Items.Count?value:-1; if(selected==next) { Invalidate(); return; } selected=next; Invalidate(); if(SelectedIndexChanged!=null) SelectedIndexChanged(this,EventArgs.Empty); } }
    public DisplayPicker() { SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true); TabStop=true; Cursor=Cursors.Hand; AccessibleName="选择显示器"; AccessibleRole=AccessibleRole.ComboBox; }
    protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Palette.Background); }
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using(var path=Palette.Round(new RectangleF(1,1,Width-3,Height-3),10)) using(var brush=new SolidBrush(Palette.Card)) using(var pen=new Pen(Enabled && (hovering || (Focused && ShowFocusCues))?Palette.Gold:Palette.Border)) { e.Graphics.FillPath(brush,path); e.Graphics.DrawPath(pen,path); }
        string text=selected>=0 && selected<Items.Count?Items[selected].ToString():"选择显示器";
        TextRenderer.DrawText(e.Graphics,text,Font,new Rectangle(14,0,Width-54,Height),Enabled?Palette.Text:Palette.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        using(var pen=new Pen(Palette.Muted,1.6f)) { int x=Width-24,y=Height/2; e.Graphics.DrawLines(pen,new Point[]{new Point(x-4,y-2),new Point(x,y+2),new Point(x+4,y-2)}); }
    }
    void OpenMenu() {
        if(!Enabled || Items.Count==0) return;
        var menu=new ContextMenuStrip { BackColor=Palette.Card,ForeColor=Palette.Text,Font=Font,ShowImageMargin=false,ShowCheckMargin=true,Renderer=new ToolStripProfessionalRenderer(new DarkMenuColors()) };
        for(int i=0;i<Items.Count;i++) { int index=i; var item=new ToolStripMenuItem(Items[i].ToString()) { Checked=i==selected,AutoSize=false,Width=Math.Max(250,Width-8),Height=38 }; item.Click+=delegate { SelectedIndex=index; }; menu.Items.Add(item); }
        menu.Closed+=delegate { menu.Dispose(); Invalidate(); }; menu.Show(this,new Point(0,Height+4));
    }
    protected override void OnClick(EventArgs e) { base.OnClick(e); Focus(); OpenMenu(); }
    protected override bool IsInputKey(Keys keyData) { return keyData==Keys.Up || keyData==Keys.Down || base.IsInputKey(keyData); }
    protected override void OnKeyDown(KeyEventArgs e) { if(e.KeyCode==Keys.Space || e.KeyCode==Keys.Enter || e.KeyCode==Keys.F4 || (e.Alt && e.KeyCode==Keys.Down)) { OpenMenu(); e.Handled=true; e.SuppressKeyPress=true; } else if(e.KeyCode==Keys.Up || e.KeyCode==Keys.Down) { SelectedIndex=Math.Max(0,Math.Min(Items.Count-1,selected+(e.KeyCode==Keys.Up?-1:1))); e.Handled=true; } base.OnKeyDown(e); }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovering=true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovering=false; Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
}
class DarkMenuColors : ProfessionalColorTable {
    public override Color ToolStripDropDownBackground { get { return Palette.Card; } }
    public override Color ImageMarginGradientBegin { get { return Palette.Card; } }
    public override Color ImageMarginGradientMiddle { get { return Palette.Card; } }
    public override Color ImageMarginGradientEnd { get { return Palette.Card; } }
    public override Color MenuItemSelected { get { return Color.FromArgb(44,58,76); } }
    public override Color MenuItemBorder { get { return Palette.Border; } }
    public override Color MenuBorder { get { return Palette.Border; } }
    public override Color CheckBackground { get { return Palette.Card; } }
    public override Color CheckSelectedBackground { get { return Palette.Border; } }
}
class PercentInput : UserControl {
    readonly TextBox editor=new TextBox(); decimal current;
    public decimal Minimum=0,Maximum=100,Increment=1;
    public decimal Value { get { decimal parsed; return Decimal.TryParse(editor.Text,out parsed)?Math.Max(Minimum,Math.Min(Maximum,parsed)):current; } set { current=Math.Max(Minimum,Math.Min(Maximum,value)); editor.Text=current.ToString("0"); } }
    public PercentInput() {
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true); BackColor=Palette.Card; TabStop=false;
        editor.BorderStyle=BorderStyle.None; editor.BackColor=Palette.Card; editor.ForeColor=Palette.Text; editor.TextAlign=HorizontalAlignment.Center; editor.MaxLength=3; editor.AccessibleName="亮度百分比，0 至 100"; Controls.Add(editor); Value=0;
        editor.KeyPress+=delegate(object sender,KeyPressEventArgs e) { if(!Char.IsControl(e.KeyChar) && !Char.IsDigit(e.KeyChar)) e.Handled=true; };
        editor.KeyDown+=delegate(object sender,KeyEventArgs e) { if(e.KeyCode==Keys.Up || e.KeyCode==Keys.Down) { Value+=e.KeyCode==Keys.Up?Increment:-Increment; e.SuppressKeyPress=true; } if(e.KeyCode==Keys.Enter) Value=Value; OnKeyDown(e); };
        editor.Enter+=delegate { Invalidate(); }; editor.Leave+=delegate { Value=Value; Invalidate(); };
    }
    protected override void OnLayout(LayoutEventArgs e) { base.OnLayout(e); editor.Font=Font; editor.SetBounds(8,Math.Max(1,(Height-editor.PreferredHeight)/2),Math.Max(1,Width-29),editor.PreferredHeight); }
    protected override void OnPaint(PaintEventArgs e) { e.Graphics.SmoothingMode=SmoothingMode.AntiAlias; using(var path=Palette.Round(new RectangleF(1,1,Width-3,Height-3),10)) using(var pen=new Pen(ContainsFocus?Palette.Gold:Palette.Border)) e.Graphics.DrawPath(pen,path); TextRenderer.DrawText(e.Graphics,"%",Font,new Rectangle(Width-24,0,18,Height),Palette.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter); }
}
class SmoothSlider : Control {
    public int Minimum=0,Maximum=100,Step=1; public bool Warm;
    int value; bool dragging;
    public event EventHandler ValueChanged;
    public int Value { get { return value; } set { int next=Math.Max(Minimum,Math.Min(Maximum,value)); if(this.value==next) return; this.value=next; Invalidate(); if(ValueChanged!=null) ValueChanged(this,EventArgs.Empty); } }
    public SmoothSlider() { DoubleBuffered=true; Height=34; TabStop=true; Cursor=Cursors.Hand; BackColor=Palette.Card; }
    void MoveValue(int x) { double t=Math.Max(0,Math.Min(1,(x-12.0)/(Width-24))); Value=Minimum+(int)Math.Round(t*(Maximum-Minimum)/Step)*Step; }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if(e.Button==MouseButtons.Left) { Focus(); Capture=true; dragging=true; MoveValue(e.X); } }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if(dragging) MoveValue(e.X); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); dragging=false; Capture=false; }
    protected override bool IsInputKey(Keys keyData) { return keyData==Keys.Left || keyData==Keys.Right || base.IsInputKey(keyData); }
    protected override void OnKeyDown(KeyEventArgs e) { if(e.KeyCode==Keys.Left || e.KeyCode==Keys.Right) { Value+=e.KeyCode==Keys.Left?-Step:Step; e.Handled=true; } base.OnKeyDown(e); }
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias; int cy=Height/2; float x=12+(Width-24)*(Value-Minimum)/(float)(Maximum-Minimum);
        using(var pen=new Pen(Palette.Border,6)) { pen.StartCap=pen.EndCap=LineCap.Round; e.Graphics.DrawLine(pen,12,cy,Width-12,cy); }
        if(Warm) { using(var gradient=new LinearGradientBrush(new Point(12,cy),new Point(Width-12,cy),Palette.Gold,Color.FromArgb(176,210,248))) using(var pen=new Pen(gradient,6)) e.Graphics.DrawLine(pen,12,cy,Width-12,cy); }
        else using(var pen=new Pen(Enabled?Palette.Gold:Palette.Muted,6)) { pen.StartCap=pen.EndCap=LineCap.Round; e.Graphics.DrawLine(pen,12,cy,x,cy); }
        using(var brush=new SolidBrush(Enabled?Palette.Text:Palette.Muted)) e.Graphics.FillEllipse(brush,x-8,cy-8,16,16);
        if(Focused) using(var pen=new Pen(Palette.Gold)) e.Graphics.DrawEllipse(pen,x-11,cy-11,22,22);
    }
}

sealed class BrightnessWindow : Form {
    readonly DisplayPicker screens=new DisplayPicker(); readonly SoftButton refresh=new SoftButton();
    readonly Label number=new Label(),warmNumber=new Label(),blueNumber=new Label(),contrastNumber=new Label(),status=new Label(),deviceInfo=new Label();
    readonly SmoothSlider slider=new SmoothSlider(),warmSlider=new SmoothSlider(),blueSlider=new SmoothSlider(),contrastSlider=new SmoothSlider();
    readonly PercentInput exact=new PercentInput(); readonly CheckBox restReminder=new CheckBox();
    readonly NotifyIcon tray=new NotifyIcon(); readonly System.Windows.Forms.Timer debounce=new System.Windows.Forms.Timer(),restTimer=new System.Windows.Forms.Timer();
    readonly List<Control> actions=new List<Control>(); readonly SoftButton eyeButton=new SoftButton(),nightButton=new SoftButton(),restoreButton=new SoftButton();
    ComfortSettings settings=ComfortSettings.Load(); List<Display> displays=new List<Display>(); bool busy,loading,syncing,closing,allowClose;
    Label LabelAt(Control parent,string text,int x,int y,int w,int h,float size,Color color,bool bold=false) {
        var label=new Label { Text=text,Location=new Point(x,y),Size=new Size(w,h),Font=new Font("Microsoft YaHei UI",size,bold?FontStyle.Bold:FontStyle.Regular),ForeColor=color,BackColor=parent is Card?Palette.Card:Palette.Background }; parent.Controls.Add(label); return label;
    }
    void PlaceLabel(Label label,Control parent,int x,int y,int w,int h,float size) { label.Location=new Point(x,y); label.Size=new Size(w,h); label.ForeColor=Palette.Text; label.BackColor=Palette.Card; label.Font=new Font("Microsoft YaHei UI",size,FontStyle.Bold); parent.Controls.Add(label); }
    SoftButton ButtonAt(Control parent,string text,int x,int y,int w,int h,EventHandler click) { var b=new SoftButton { Text=text,Location=new Point(x,y),Size=new Size(w,h),Font=Font }; b.Click+=click; parent.Controls.Add(b); actions.Add(b); return b; }
    public BrightnessWindow() {
        Text="微光 · 屏幕舒适控制"; ClientSize=new Size(900,742); FormBorderStyle=FormBorderStyle.FixedSingle; MaximizeBox=false; StartPosition=FormStartPosition.CenterScreen; Font=new Font("Microsoft YaHei UI",10); AutoScaleMode=AutoScaleMode.Dpi; BackColor=Palette.Background; ForeColor=Palette.Text; DoubleBuffered=true;
        Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        var logo=new PictureBox { Image=Icon.ToBitmap(),SizeMode=PictureBoxSizeMode.Zoom,Location=new Point(30,28),Size=new Size(48,48) }; Controls.Add(logo);
        LabelAt(this,"微光",94,21,180,38,23,Palette.Text,true); LabelAt(this,"让每一段屏幕时光，都更舒服。",96,62,460,24,10,Palette.Muted);
        LabelAt(this,"SCREEN COMFORT",653,35,215,28,10,Palette.Gold);
        screens.Location=new Point(30,101); screens.Size=new Size(648,38); screens.BackColor=Palette.Card; screens.ForeColor=Palette.Text; Controls.Add(screens);
        refresh.Text="重新检测"; refresh.Location=new Point(698,101); refresh.Size=new Size(172,38); Controls.Add(refresh); refresh.Click+=async delegate { await Scan(); };
        SetupMode(eyeButton,"护眼模式","柔和暖白 · 适合日常阅读",30,152); SetupMode(nightButton,"夜间模式","低亮暖光 · 适合夜晚放松",316,152); SetupMode(restoreButton,"恢复原始设置","还原亮度、颜色与对比度",602,152);
        eyeButton.Click+=async delegate { await ApplyMode("护眼",settings.Eye); }; nightButton.Click+=async delegate { await ApplyMode("夜间",settings.Night); }; restoreButton.Click+=async delegate { await RestoreCurrent(); };
        var light=new Card { Location=new Point(30,260),Size=new Size(405,306) }; var color=new Card { Location=new Point(453,260),Size=new Size(417,306) }; Controls.Add(light); Controls.Add(color);
        LabelAt(light,"屏幕亮度",22,18,200,26,11,Palette.Muted); PlaceLabel(number,light,22,53,350,66,38); number.Text="—";
        slider.Location=new Point(14,129); slider.Size=new Size(377,36); light.Controls.Add(slider);
        ButtonAt(light,"−1%",22,181,78,38,delegate { slider.Value--; }); ButtonAt(light,"+1%",108,181,78,38,delegate { slider.Value++; });
        exact.Minimum=0; exact.Maximum=100; exact.Increment=1; exact.Location=new Point(198,181); exact.Size=new Size(84,38); exact.BackColor=Palette.Card; exact.ForeColor=Palette.Text; light.Controls.Add(exact); actions.Add(exact);
        ButtonAt(light,"应用",294,181,89,38,delegate { slider.Value=(int)exact.Value; if(!syncing) QueueChange(); });
        exact.KeyDown+=delegate(object sender,KeyEventArgs e) { if(e.KeyCode==Keys.Enter) { slider.Value=(int)exact.Value; QueueChange(); e.SuppressKeyPress=true; } };
        LabelAt(light,"对比度",22,242,82,24,9,Palette.Muted); PlaceLabel(contrastNumber,light,321,238,62,26,11);
        contrastSlider.Location=new Point(98,236); contrastSlider.Size=new Size(216,32); light.Controls.Add(contrastSlider);
        LabelAt(light,"默认保留原对比度，可自行微调。",22,275,362,20,9,Palette.Muted);
        LabelAt(color,"暖色参考",22,18,200,26,11,Palette.Muted); PlaceLabel(warmNumber,color,22,53,365,57,28);
        warmSlider.Minimum=3500; warmSlider.Maximum=6500; warmSlider.Step=100; warmSlider.Warm=true; warmSlider.Location=new Point(14,115); warmSlider.Size=new Size(389,34); color.Controls.Add(warmSlider);
        LabelAt(color,"更暖 3500 K",22,152,165,23,9,Palette.Gold); LabelAt(color,"原色基准 6500 K",246,152,150,23,9,Palette.Muted);
        LabelAt(color,"蓝色通道额外衰减",22,201,248,25,11,Palette.Muted); PlaceLabel(blueNumber,color,308,195,82,31,17);
        blueSlider.Minimum=0; blueSlider.Maximum=40; blueSlider.Location=new Point(14,233); blueSlider.Size=new Size(389,34); color.Controls.Add(blueSlider);
        LabelAt(color,"参考色温非实测；衰减比例不是光谱蓝光量。",22,275,380,20,9,Palette.Muted);
        var footer=new Card { Location=new Point(30,584),Size=new Size(840,78) }; Controls.Add(footer);
        restReminder.Text="每 20 分钟提醒休息"; restReminder.Location=new Point(20,23); restReminder.Size=new Size(250,30); restReminder.ForeColor=Palette.Text; restReminder.BackColor=Palette.Card; footer.Controls.Add(restReminder);
        ButtonAt(footer,"保存为护眼",475,19,160,40,delegate { SaveProfile(false); }); ButtonAt(footer,"保存为夜间",652,19,166,40,delegate { SaveProfile(true); });
        status.Location=new Point(30,677); status.Size=new Size(690,44); status.Font=new Font(Font.FontFamily,9); status.ForeColor=Palette.Muted; status.Text="正在连接显示器…"; Controls.Add(status);
        var help=new LinkLabel { Text="依据与说明",Location=new Point(754,682),Size=new Size(116,26),LinkColor=Palette.Gold,ActiveLinkColor=Palette.Text,VisitedLinkColor=Palette.Gold }; Controls.Add(help); help.LinkClicked+=delegate { try { System.Diagnostics.Process.Start(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"模式说明与研究依据.html")); } catch(Exception ex) { status.Text=ex.Message; } };
        slider.ValueChanged+=delegate { if(!syncing) { number.Text=slider.Value+"%"; exact.Value=slider.Value; QueueChange(); } };
        warmSlider.ValueChanged+=delegate { if(!syncing) { warmNumber.Text=warmSlider.Value+" K"; QueueChange(); } };
        blueSlider.ValueChanged+=delegate { if(!syncing) { blueNumber.Text=blueSlider.Value+"%"; QueueChange(); } };
        contrastSlider.ValueChanged+=delegate { if(!syncing) { contrastNumber.Text=contrastSlider.Value+"%"; QueueChange(); } };
        debounce.Interval=450; debounce.Tick+=async delegate { debounce.Stop(); await ApplyMode("自定义",ReadProfile()); };
        restTimer.Interval=20*60*1000; restReminder.CheckedChanged+=delegate { restTimer.Stop(); if(restReminder.Checked) restTimer.Start(); };
        restTimer.Tick+=delegate { tray.ShowBalloonTip(8000,"微光 · 休息一下","看约 6 米外 20 秒，眨眨眼，让眼睛休息。",ToolTipIcon.Info); };
        screens.SelectedIndexChanged+=delegate { debounce.Stop(); RefreshValues(); };
        var menu=new ContextMenuStrip(); menu.Items.Add("打开微光",null,delegate { ShowPanel(); }); menu.Items.Add("恢复并退出",null,delegate { Close(); });
        tray.Icon=Icon; tray.Text="微光 · 屏幕亮度与暖色"; tray.ContextMenuStrip=menu; tray.DoubleClick+=delegate { ShowPanel(); }; tray.Visible=true;
        Resize+=delegate { if(WindowState==FormWindowState.Minimized) Hide(); };
        Shown+=async delegate { await Scan(); };
        FormClosing+=async delegate(object sender,FormClosingEventArgs e) {
            if(allowClose) return; e.Cancel=true; if(busy || loading || closing) { status.Text="正在与显示器通信，请稍后退出。"; return; }
            closing=true; debounce.Stop(); SetEnabled(false); status.Text="正在恢复原始屏幕设置…";
            try { await Task.Run(()=>{ foreach(var d in displays) d.RestoreOriginal(); }); allowClose=true; Close(); }
            catch(Exception ex) { status.Text="恢复未完成："+ex.Message; closing=false; SetEnabled(true); if(MessageBox.Show(this,"显示器可能已断开。恢复记录已保存，下次连接并打开本工具时重试。仍然退出吗？","未完成恢复",MessageBoxButtons.YesNo)==DialogResult.Yes) { allowClose=true; Close(); } }
        };
        FormClosed+=delegate { debounce.Dispose(); restTimer.Dispose(); tray.Dispose(); foreach(var d in displays) d.Dispose(); };
        SetEnabled(false);
    }
    void SetupMode(SoftButton b,string title,string subtitle,int x,int y) { b.Text=title; b.Subtitle=subtitle; b.Location=new Point(x,y); b.Size=new Size(268,88); b.Font=new Font(Font.FontFamily,13,FontStyle.Bold); Controls.Add(b); actions.Add(b); }
    Display CurrentDisplay { get { return screens.SelectedIndex<0?null:displays[screens.SelectedIndex]; } }
    void ShowPanel() { Show(); WindowState=FormWindowState.Normal; Activate(); }
    void SetEnabled(bool enabled) {
        var d=CurrentDisplay; bool ready=enabled && d!=null; screens.Enabled=enabled; refresh.Enabled=enabled;
        foreach(var a in actions) a.Enabled=ready;
        slider.Enabled=ready; warmSlider.Enabled=blueSlider.Enabled=ready && d.HasRgb; contrastSlider.Enabled=ready && d.HasContrast;
        eyeButton.Enabled=nightButton.Enabled=ready && d.HasRgb;
    }
    void RefreshValues() {
        var d=CurrentDisplay; if(d==null) return; syncing=true;
        slider.Value=d.Percent; exact.Value=slider.Value; number.Text=slider.Value+"%";
        warmSlider.Value=d.Warmth; warmNumber.Text=d.HasRgb?d.Warmth+" K":"暂不支持"; blueSlider.Value=d.Blue; blueNumber.Text=d.HasRgb?d.Blue+"%":"—";
        contrastSlider.Value=d.ContrastPercent; contrastNumber.Text=d.HasContrast?d.ContrastPercent+"%":"—";
        eyeButton.Selected=d.Mode=="护眼"; nightButton.Selected=d.Mode=="夜间"; restoreButton.Selected=!d.HasChanges;
        eyeButton.Invalidate(); nightButton.Invalidate(); restoreButton.Invalidate(); syncing=false;
    }
    ComfortProfile ReadProfile() { return new ComfortProfile { Brightness=slider.Value,Warmth=warmSlider.Value,Blue=blueSlider.Value,Contrast=CurrentDisplay!=null && CurrentDisplay.HasContrast?contrastSlider.Value:-1 }; }
    void QueueChange() { if(busy || loading || syncing) return; debounce.Stop(); debounce.Start(); }
    void SaveProfile(bool night) {
        var p=ReadProfile(); if(night) settings.Night=p; else settings.Eye=p;
        try { settings.Save(); status.Text="已记住"+(night?"夜间":"护眼")+"组合："+p.Brightness+"% · "+p.Warmth+" K · 蓝色额外衰减 "+p.Blue+"%。"; } catch(Exception ex) { status.Text="保存失败："+ex.Message; }
    }
    async Task ApplyMode(string name,ComfortProfile profile) {
        if(busy || loading || CurrentDisplay==null) return; debounce.Stop(); busy=true; SetEnabled(false); var d=CurrentDisplay; status.Text="正在应用"+name+"：亮度、暖色与蓝色通道…";
        try {
            await Task.Run(()=>d.ApplyComfort(profile)); d.Mode=name; RefreshValues();
            if(name=="护眼" || name=="夜间") restReminder.Checked=true;
            status.Text=name+"已生效 · "+d.Percent+"% · 暖色参考 "+d.Warmth+" K · 蓝色额外衰减 "+d.Blue+"%\r\n最小化保持生效；退出时恢复原始屏幕设置。";
        } catch(Exception ex) { RefreshValues(); status.Text=ex.Message; }
        finally { busy=false; SetEnabled(true); }
    }
    async Task RestoreCurrent() {
        if(busy || loading || CurrentDisplay==null) return; debounce.Stop(); busy=true; SetEnabled(false); var d=CurrentDisplay;
        try { await Task.Run(()=>d.RestoreOriginal()); restReminder.Checked=false; RefreshValues(); status.Text="已恢复原始亮度、RGB 颜色和对比度。"; } catch(Exception ex) { status.Text="恢复未完成："+ex.Message; }
        finally { busy=false; SetEnabled(true); }
    }
    async Task Scan() {
        if(busy || loading) return; loading=true; debounce.Stop(); SetEnabled(false); status.Text="正在检测亮度与颜色控制能力…";
        try {
            await Task.Run(()=>{ foreach(var d in displays) d.RestoreOriginal(); });
            foreach(var d in displays) d.Dispose(); displays.Clear(); screens.Items.Clear(); screens.SelectedIndex=-1;
            string notes=""; displays=await Task.Run(()=>Display.Find(out notes));
            await Task.Run(()=>{ foreach(var d in displays) d.InitializeColor(); });
            foreach(var d in displays) screens.Items.Add(d);
            if(displays.Count>0) { screens.SelectedIndex=0; status.Text=CurrentDisplay.HasRgb?"显示器已连接 · 支持真实硬件亮度与 RGB 调节。选择模式即可一键应用。":"显示器支持亮度，未提供 RGB 接口；颜色模式不可用。"; }
            else status.Text="未找到可调节的屏幕，请检查 DDC/CI 或显示器连接。";
        } catch(Exception ex) { status.Text="检测未完成："+ex.Message; }
        finally { loading=false; SetEnabled(true); }
    }
}
