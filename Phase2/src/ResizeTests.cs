using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace IrisClipboardNext {
    // Uses real window handles and WM_NCHITTEST, without moving the user's
    // pointer or entering the blocking native drag loop.
    static class ResizeTests {
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr wParam,IntPtr lParam);
        [StructLayout(LayoutKind.Sequential)] struct MinMaxInfo {
            public Point Reserved,MaxSize,MaxPosition,MinTrackSize,MaxTrackSize;
        }
        sealed class RecreatedPanel : Panel {
            public void RecreateForTest(){RecreateHandle();}
        }
        static IntPtr Pack(Point point){return new IntPtr(unchecked((point.Y<<16)|(point.X&0xffff)));}
        static int Hit(Control control,Point screenPoint){return SendMessage(control.Handle,0x84,IntPtr.Zero,Pack(screenPoint)).ToInt32();}
        static Control Deepest(Control root,Point screenPoint){
            Control next=root.GetChildAtPoint(root.PointToClient(screenPoint),GetChildAtPointSkip.Invisible);
            return next==null?root:Deepest(next,screenPoint);
        }
        static void Check(bool condition,string failure){if(!condition)throw new Exception("Resize regression: "+failure);}
        internal static void Verify(ThemeForm form){
            Check(form.IsHandleCreated&&form.WindowState==FormWindowState.Normal,"test requires a normal created form");
            int width=form.ClientSize.Width,height=form.ClientSize.Height;
            // 8px is deliberately beyond the old 6px band, and within the
            // child panels that previously consumed the hit-test message.
            Point[] points={new Point(8,height/2),new Point(width-9,height/2),new Point(width/2,8),new Point(width/2,height-9),new Point(20,8),new Point(width-21,8),new Point(20,height-9),new Point(width-21,height-9)};
            int[] hits={10,11,12,15,13,14,16,17};int covered=0;
            for(int i=0;i<points.Length;i++){
                Point screen=form.PointToScreen(points[i]);
                Check(Hit(form,screen)==hits[i],"wrong form edge/corner at "+points[i]);
                for(Control child=Deepest(form,screen);child!=form;child=child.Parent){
                    Check(Hit(child,screen)==-1,"child HWND consumed the resize band: "+child.GetType().Name);
                    covered++;
                }
            }
            Check(covered>=4,"did not exercise actual controls covering all window edges");
            Button close=FindButton(form,"×");
            Check(close!=null,"caption close button missing");
            Point closeCenter=close.PointToScreen(new Point(close.Width/2,close.Height/2));
            Check(Hit(close,closeCenter)!=-1&&Hit(form,closeCenter)<10,"resize band consumed caption button center");
            Point interior=form.PointToScreen(new Point(width/2,height/2));
            Check(Hit(form,interior)<10,"resize band consumed the content interior");
            IntPtr memory=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(MinMaxInfo)));
            try{
                Marshal.StructureToPtr(new MinMaxInfo(),memory,false);
                SendMessage(form.Handle,0x24,IntPtr.Zero,memory);
                var limits=(MinMaxInfo)Marshal.PtrToStructure(memory,typeof(MinMaxInfo));
                Check(limits.MinTrackSize.X>=form.MinimumSize.Width&&limits.MinTrackSize.Y>=form.MinimumSize.Height,"native sizing did not preserve minimum size");
            }finally{Marshal.FreeHGlobal(memory);}
            bool canMaximize=form.MaximizeBox;form.MaximizeBox=false;
            try{
                Point edge=form.PointToScreen(points[0]);
                Check(Hit(form,edge)<10,"nonresizable dialog exposes sizing handles");
            }finally{form.MaximizeBox=canMaximize;}
        }
        internal static void VerifyChildLifecycle(){
            using(var form=new ThemeForm{Text="Resize test",Size=new Size(520,480),MinimumSize=new Size(420,430),ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-10000,-10000)}){
                form.Show();Application.DoEvents();
                using(var host=new Panel{Dock=DockStyle.Fill})using(var nested=new RecreatedPanel{Dock=DockStyle.Fill}){
                    form.Controls.Add(host);host.Controls.Add(nested);host.BringToFront();Application.DoEvents();
                    Point screen=form.PointToScreen(new Point(8,form.ClientSize.Height/2));
                    Check(Deepest(form,screen)==nested,"dynamic nested control does not cover test edge");
                    Check(Hit(nested,screen)==-1&&Hit(host,screen)==-1&&Hit(form,screen)==10,"late-added descendants were not hooked");
                    nested.RecreateForTest();Application.DoEvents();
                    Check(Hit(nested,screen)==-1,"child handle recreation lost resizing");
                    // Reparenting must release the old form's native hook.
                    using(var other=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=form.Location,Size=form.Size,FormBorderStyle=FormBorderStyle.None}){
                        other.Controls.Add(nested);other.Show();Application.DoEvents();
                        Point otherEdge=other.PointToScreen(new Point(8,other.ClientSize.Height/2));
                        Check(Hit(nested,otherEdge)!=-1,"removed descendant retained an old form hook");
                        host.Controls.Add(nested);Application.DoEvents();
                        Check(Hit(nested,screen)==-1,"reattached descendant failed to reacquire its hook");
                        other.Close();
                    }
                }
                Application.DoEvents();form.Close();
            }
        }
        static Button FindButton(Control parent,string text){
            foreach(Control child in parent.Controls){var button=child as Button;if(button!=null&&button.Text==text)return button;button=FindButton(child,text);if(button!=null)return button;}
            return null;
        }
    }
}
