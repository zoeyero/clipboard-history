using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace ClipboardNext {
    class ThemeForm : Form {
        Panel caption; Label title; Button minimize,maximize,close;
        readonly Dictionary<Control,ResizeChildWindow> resizeChildren=new Dictionary<Control,ResizeChildWindow>();
        bool disposingResizeChildren;
        int resizeEdge=12,resizeCorner=26;
        float chromeScale=1;
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h,int msg,IntPtr w,IntPtr l);
        [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr h);
        public ThemeForm() {
            DoubleBuffered=true;
            FormBorderStyle=FormBorderStyle.None; Padding=new Padding(1); BackColor=Theme.Background;
            caption=new Panel { Dock=DockStyle.Top,Height=42,BackColor=Theme.Pink };
            title=new Label { Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(10,0,0,0),AutoEllipsis=true,ForeColor=Theme.Ink };
            close=new SoftButton { Text="×",Dock=DockStyle.Right,Width=42 };maximize=new SoftButton { Text="□",Dock=DockStyle.Right,Width=42 };minimize=new SoftButton { Text="−",Dock=DockStyle.Right,Width=42 };
            caption.Controls.Add(title);caption.Controls.Add(minimize);caption.Controls.Add(maximize);caption.Controls.Add(close);Controls.Add(caption);
            close.Click+=(s,e)=>Close();minimize.Click+=(s,e)=>WindowState=FormWindowState.Minimized;maximize.Click+=(s,e)=>ToggleMax();
            MouseEventHandler drag=(s,e)=>{if(e.Button==MouseButtons.Left){ReleaseCapture();SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}};
            title.MouseDown+=drag;caption.MouseDown+=drag;title.DoubleClick+=(s,e)=>ToggleMax();
            TextChanged+=(s,e)=>title.Text=Text;
        }
        void ToggleMax(){if(!MaximizeBox)return;MaximizedBounds=Screen.FromControl(this).WorkingArea;WindowState=WindowState==FormWindowState.Maximized?FormWindowState.Normal:FormWindowState.Maximized;}
        public void ApplyChromePalette(){BackColor=Theme.Background;caption.BackColor=Theme.Pink;title.BackColor=Theme.Pink;foreach(var b in new[]{minimize,maximize,close})b.BackColor=Theme.Mint;Invalidate();}
        protected override void OnLoad(EventArgs e){FormBorderStyle=FormBorderStyle.None;bool shifted=false;foreach(Control c in Controls){if(c!=caption&&c.Dock==DockStyle.None){c.Top+=caption.Height;shifted=true;}}if(shifted)Height+=caption.Height;caption.SendToBack();maximize.Visible=MaximizeBox;minimize.Visible=MinimizeBox;title.Text=Text;base.OnLoad(e);}
        protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);UpdateCaptionMetrics();}
        void UpdateCaptionMetrics(){if(caption!=null){caption.Font=Font;caption.Height=Math.Max((int)Math.Round(42*chromeScale),Font.Height+(int)Math.Round(12*chromeScale));foreach(var button in new[]{minimize,maximize,close})if(button!=null)button.Width=(int)Math.Round(42*chromeScale);}}
        void UpdateWindowShape(){
            if(ClientSize.Width<2||ClientSize.Height<2)return;
            Region previous=Region;
            if(WindowState==FormWindowState.Maximized)Region=null;
            else using(var path=Theme.Rounded(new RectangleF(0,0,ClientSize.Width,ClientSize.Height),18))Region=new Region(path);
            if(previous!=null)previous.Dispose();
        }
        protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);UpdateWindowShape();}
        protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);UpdateResizeMetrics();UpdateWindowShape();}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(ClientSize.Width<2||ClientSize.Height<2)return;e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using(var p=new Pen(Theme.Pink))using(var path=Theme.Rounded(new RectangleF(.5f,.5f,ClientSize.Width-1,ClientSize.Height-1),WindowState==FormWindowState.Maximized?1:18))e.Graphics.DrawPath(p,path);}
        protected override void WndProc(ref Message m){
            base.WndProc(ref m);
            if(m.Msg==0x84){int hit=ResizeHit(m.LParam);if(hit!=0)m.Result=new IntPtr(hit);}
            else if(m.Msg==0x02E0)UpdateResizeMetrics(); // WM_DPICHANGED
        }

        void UpdateResizeMetrics(){
            if(!IsHandleCreated)return;
            float dpi=96;
            try{uint value=GetDpiForWindow(Handle);if(value>0)dpi=value;}
            catch(EntryPointNotFoundException){using(var graphics=CreateGraphics())dpi=graphics.DpiX;}
            chromeScale=dpi/96;
            resizeEdge=Math.Max(12,(int)Math.Round(12*dpi/96));
            resizeCorner=Math.Max(26,(int)Math.Round(26*dpi/96));
            UpdateCaptionMetrics();
        }
        int ResizeHit(IntPtr packedPoint){
            if(disposingResizeChildren||IsDisposed||!IsHandleCreated||!Enabled||WindowState!=FormWindowState.Normal||!MaximizeBox)return 0;
            long point=packedPoint.ToInt64();
            Point p=PointToClient(new Point((short)(point&0xffff),(short)((point>>16)&0xffff)));
            if(!ClientRectangle.Contains(p))return 0;
            bool left=p.X<resizeEdge,right=p.X>=ClientSize.Width-resizeEdge,top=p.Y<resizeEdge,bottom=p.Y>=ClientSize.Height-resizeEdge;
            // Corners extend along the edge, without consuming a square inside
            // the caption buttons. Windows still owns the sizing/moving loop.
            if((left&&p.Y<resizeCorner)||(top&&p.X<resizeCorner))return 13;
            if((right&&p.Y<resizeCorner)||(top&&p.X>=ClientSize.Width-resizeCorner))return 14;
            if((left&&p.Y>=ClientSize.Height-resizeCorner)||(bottom&&p.X<resizeCorner))return 16;
            if((right&&p.Y>=ClientSize.Height-resizeCorner)||(bottom&&p.X>=ClientSize.Width-resizeCorner))return 17;
            return left?10:right?11:top?12:bottom?15:0;
        }
        protected override void OnControlAdded(ControlEventArgs e){base.OnControlAdded(e);WatchResizeChild(e.Control);}
        protected override void OnControlRemoved(ControlEventArgs e){UnwatchResizeChild(e.Control);base.OnControlRemoved(e);}
        void WatchResizeChild(Control child){
            if(disposingResizeChildren||resizeChildren==null||child.IsDisposed||resizeChildren.ContainsKey(child))return;
            resizeChildren.Add(child,new ResizeChildWindow(this,child));
            child.ControlAdded+=ResizeChildAdded;child.ControlRemoved+=ResizeChildRemoved;
            foreach(Control nested in child.Controls)WatchResizeChild(nested);
        }
        void UnwatchResizeChild(Control child){
            if(resizeChildren==null)return;
            foreach(Control nested in child.Controls)UnwatchResizeChild(nested);
            ResizeChildWindow window;
            if(!resizeChildren.TryGetValue(child,out window))return;
            child.ControlAdded-=ResizeChildAdded;child.ControlRemoved-=ResizeChildRemoved;
            resizeChildren.Remove(child);window.Dispose();
        }
        void ResizeChildAdded(object sender,ControlEventArgs e){WatchResizeChild(e.Control);}
        void ResizeChildRemoved(object sender,ControlEventArgs e){UnwatchResizeChild(e.Control);}
        protected override void Dispose(bool disposing){
            if(disposing&&!disposingResizeChildren){
                disposingResizeChildren=true;
                if(resizeChildren!=null){
                    foreach(var pair in resizeChildren){pair.Key.ControlAdded-=ResizeChildAdded;pair.Key.ControlRemoved-=ResizeChildRemoved;pair.Value.Dispose();}
                    resizeChildren.Clear();
                }
            }
            base.Dispose(disposing);
        }
        // Child HWNDs cover almost the entire borderless form. Returning
        // HTTRANSPARENT there lets Windows ask their parent for the same hit,
        // instead of leaving only the one-pixel uncovered form border usable.
        sealed class ResizeChildWindow : NativeWindow,IDisposable {
            readonly ThemeForm owner;readonly Control child;
            public ResizeChildWindow(ThemeForm owner,Control child){
                this.owner=owner;this.child=child;
                child.HandleCreated+=HandleCreated;child.HandleDestroyed+=HandleDestroyed;
                if(child.IsHandleCreated)AssignHandle(child.Handle);
            }
            void HandleCreated(object sender,EventArgs e){if(Handle==IntPtr.Zero)AssignHandle(child.Handle);}
            void HandleDestroyed(object sender,EventArgs e){if(Handle!=IntPtr.Zero)ReleaseHandle();}
            protected override void WndProc(ref Message m){
                if(m.Msg==0x84&&owner.ResizeHit(m.LParam)!=0){m.Result=new IntPtr(-1);return;}
                base.WndProc(ref m);
            }
            public void Dispose(){child.HandleCreated-=HandleCreated;child.HandleDestroyed-=HandleDestroyed;if(Handle!=IntPtr.Zero)ReleaseHandle();}
        }
    }
}
