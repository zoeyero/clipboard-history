using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ClipboardNext {
    static class Theme {
        public static string[][] Presets { get { return new string[][] {
            new[]{"macaron",L.T("奶油薄荷"),"#FFDCD3","#D8F4E8","#FFA394","#FFF8F5"},
            new[]{"apricot",L.T("暖杏花园"),"#BBC18A","#E27759","#F2C674","#F6CEA5"},
            new[]{"sunny",L.T("晴日早餐"),"#B3C8D5","#EEAE6B","#E86543","#F5DE85"},
            new[]{"lavender",L.T("香芋暖阳"),"#F5DD82","#9083AF","#ED9D49","#C7C1DD"},
            new[]{"garden",L.T("青柠花园"),"#F5DD8B","#7D906B","#E16B4D","#C2D1A3"},
            new[]{"ocean",L.T("海盐橘子"),"#ECAD64","#5683A1","#DC6443","#B5C7D2"},
            new[]{"peach",L.T("蜜桃下午茶"),"#C5D4E1","#E26447","#688DAE","#F5C9B7"}
        }; } }
        public static void Apply(string id,Image image) {
            if(String.IsNullOrEmpty(id)||id=="auto"){FromImage(image);return;}
            string[] preset=Array.Find(Presets,p=>p[0]==id);
            if(preset==null||id=="macaron"){FromImage(null);return;}
            Color main=ColorTranslator.FromHtml(preset[5]),secondary=ColorTranslator.FromHtml(preset[2]),accent=ColorTranslator.FromHtml(preset[3]);
            Background=Blend(main,Color.White,.83f);Pink=main;Mint=Blend(secondary,Color.White,.25f);
            Border=Blend(main,Color.White,.25f);AccentInk=Blend(accent,Color.Black,.65f);
        }
        public static Color Background = ColorTranslator.FromHtml("#FFF8F5");
        public static Color Pink = ColorTranslator.FromHtml("#FFDCD3");
        public static Color Mint = ColorTranslator.FromHtml("#D8F4E8");
        public static readonly Color Coral = ColorTranslator.FromHtml("#FFA394");
        public static readonly Color Ink = ColorTranslator.FromHtml("#343A40");
        public static readonly Color Muted = ColorTranslator.FromHtml("#73757D");
        public static Color AccentInk = ColorTranslator.FromHtml("#486B63");
        public static Color Border = ColorTranslator.FromHtml("#EDE4E2");
        public static Color Blend(Color a,Color b,float amount){return Color.FromArgb((int)(a.R*(1-amount)+b.R*amount),(int)(a.G*(1-amount)+b.G*amount),(int)(a.B*(1-amount)+b.B*amount));}
        public static void FromImage(Image image){
            if(image==null){Background=ColorTranslator.FromHtml("#FFF8F5");Pink=ColorTranslator.FromHtml("#FFDCD3");Mint=ColorTranslator.FromHtml("#D8F4E8");AccentInk=ColorTranslator.FromHtml("#486B63");Border=ColorTranslator.FromHtml("#EDE4E2");return;}
            var bins=new System.Collections.Generic.Dictionary<int,int>();using(var small=new Bitmap(image,64,64))for(int y=0;y<64;y++)for(int x=0;x<64;x++){Color c=small.GetPixel(x,y);if(c.A<64)continue;int key=(c.R/32<<6)|(c.G/32<<3)|c.B/32;if(!bins.ContainsKey(key))bins[key]=0;bins[key]++;}
            int best=0;double score=-1;foreach(var pair in bins){Color c=Color.FromArgb(((pair.Key>>6)&7)*32+16,((pair.Key>>3)&7)*32+16,(pair.Key&7)*32+16);double s=pair.Value*(0.3+c.GetSaturation());if(s>score){score=s;best=pair.Key;}}
            Color seed=Color.FromArgb(((best>>6)&7)*32+16,((best>>3)&7)*32+16,(best&7)*32+16);Background=Blend(seed,Color.White,.92f);Pink=Blend(seed,Color.White,.70f);Mint=Blend(seed,Color.White,.78f);Border=Blend(seed,Color.White,.55f);AccentInk=Blend(seed,Color.Black,.60f);
        }
        public static readonly Icon AppIcon = MakeIcon();
        public static GraphicsPath Rounded(RectangleF r, float radius) {
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height)); var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right-d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right-d, r.Bottom-d, d, d, 0, 90); p.AddArc(r.X, r.Bottom-d, d, d, 90, 90); p.CloseFigure(); return p;
        }
        public static void Logo(Graphics g, int size) {
            // Keep the original app identity independent of the chosen window palette.
            Color logoPink=ColorTranslator.FromHtml("#FFDCD3"),logoMint=ColorTranslator.FromHtml("#D8F4E8"),logoInk=ColorTranslator.FromHtml("#486B63");
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var state = g.Save(); g.ScaleTransform(size / 64f, size / 64f);
            using (var path = Rounded(new RectangleF(1, 1, 62, 62), 18))
            using (var fill = new LinearGradientBrush(new Point(0,0), new Point(64,64), logoPink, Coral)) g.FillPath(fill, path);
            using (var path = Rounded(new RectangleF(19, 16, 29, 36), 7))
            using (var fill = new SolidBrush(Color.FromArgb(150, logoMint))) g.FillPath(fill, path);
            using (var path = Rounded(new RectangleF(16, 14, 29, 36), 6)) g.FillPath(Brushes.White, path);
            using (var path = Rounded(new RectangleF(23, 10, 15, 8), 3))
            using (var fill = new SolidBrush(logoInk)) g.FillPath(fill, path);
            using (var pen = new Pen(logoInk, 2.6f)) { pen.StartCap = pen.EndCap = LineCap.Round; g.DrawLine(pen, 23,27,37,27); g.DrawLine(pen,23,34,37,34); g.DrawLine(pen,23,41,32,41); }
            g.Restore(state);
        }
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
        static Icon MakeIcon() {
            using (var bitmap = new Bitmap(64,64)) { using (var g = Graphics.FromImage(bitmap)) Logo(g,64);
                IntPtr h = bitmap.GetHicon(); try { using (var icon = Icon.FromHandle(h)) return (Icon)icon.Clone(); } finally { DestroyIcon(h); }
            }
        }
    }
    class SoftButton : Button {
        bool hover;
        public SoftButton() { FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; BackColor = Theme.Mint; ForeColor = Theme.Ink; Cursor = Cursors.Hand; DoubleBuffered = true; }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Form owner=FindForm();
            if(owner!=null&&owner.BackgroundImage!=null&&Parent!=null&&Parent.BackColor.A<255){Point offset=Point.Empty;for(Control c=this;c!=null&&c!=owner;c=c.Parent){offset.X+=c.Left;offset.Y+=c.Top;}e.Graphics.DrawImage(owner.BackgroundImage,ClientRectangle,new Rectangle(offset,Size),GraphicsUnit.Pixel);}
            else e.Graphics.Clear(Parent==null||Parent.BackColor.A<255?Theme.Background:Parent.BackColor);
            using (var path = Theme.Rounded(new RectangleF(1,1,Width-3,Height-3),12))
            using (var brush = new SolidBrush(!Enabled ? Theme.Border : hover ? Theme.Pink : BackColor)) {
                e.Graphics.FillPath(brush,path);
                if (Focused) using(var pen = new Pen(Theme.AccentInk)) e.Graphics.DrawPath(pen,path);
            }
            TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled ? ForeColor : Theme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
        }
    }
}
