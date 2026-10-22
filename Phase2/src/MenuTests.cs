using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ClipboardNext {
    partial class MainForm {
        [DllImport("user32.dll",EntryPoint="SendMessageW")]
        static extern IntPtr MenuTestSendMessage(IntPtr handle,uint message,IntPtr wParam,IntPtr lParam);

        internal ContextMenuStrip VerifyRecordMenuRegression() {
            Exception uiError=null;
            ThreadExceptionEventHandler capture=(s,e)=>{uiError=e.Exception;};
            Application.ThreadException+=capture;
            try {
                SwitchShelf(false);search.Clear();filter.SelectedIndex=0;EndBatch();
                string id=visible.First(c=>c.Kind=="text").Id;
                bool pinned=Data.Items.Single(c=>c.Id==id).Pinned,favorite=Data.Items.Single(c=>c.Id==id).Favorite;
                Action checkUi=()=>{Application.DoEvents();if(uiError!=null)throw new Exception("Menu event raised an unhandled UI exception.",uiError);};
                Action open=()=>{
                    int index=visible.FindIndex(c=>c.Id==id);list.TopIndex=index;
                    Rectangle row=list.GetItemRectangle(index);Point dots=new Point(row.Right-40,row.Top+70);
                    // Follow the actual card mouse-down / mouse-up handlers.
                    list.TestPointerDown(dots);list.TestPointerUp(dots);checkUi();
                    if(recordMenu==null||recordMenu.IsDisposed||!recordMenu.Visible)throw new Exception("Record menu failed to open from the three-dot button.");
                };
                Action<int> click=index=>{
                    // Native control messages exercise ToolStrip mouse dispatch,
                    // including its Close-before-Click sequencing and focus cleanup.
                    Rectangle bounds=recordMenu.Items[index].Bounds;
                    int x=bounds.Left+bounds.Width/2,y=bounds.Top+bounds.Height/2;
                    IntPtr position=new IntPtr((y<<16)|(x&0xffff));IntPtr handle=recordMenu.Handle;
                    MenuTestSendMessage(handle,0x0200,IntPtr.Zero,position);
                    MenuTestSendMessage(handle,0x0201,new IntPtr(1),position);
                    MenuTestSendMessage(handle,0x0202,IntPtr.Zero,position);checkUi();
                    if(recordMenu.IsDisposed||recordMenu.Visible)throw new Exception("Menu click did not close safely.");
                };
                open();ContextMenuStrip retained=recordMenu;
                for(int i=0;i<8;i++) {
                    recordMenu.Close(ToolStripDropDownCloseReason.AppClicked);checkUi();open();
                    if(!Object.ReferenceEquals(retained,recordMenu))throw new Exception("Record menu was not reused.");
                }
                click(0);if(Data.Items.Single(c=>c.Id==id).Pinned==pinned)throw new Exception("Pin menu click failed.");
                open();click(0);if(Data.Items.Single(c=>c.Id==id).Pinned!=pinned)throw new Exception("Unpin menu click failed.");
                open();click(1);if(Data.Items.Single(c=>c.Id==id).Favorite==favorite)throw new Exception("Favorite menu click failed.");
                open();click(1);if(Data.Items.Single(c=>c.Id==id).Favorite!=favorite)throw new Exception("Unfavorite menu click failed.");
                open();click(3);if(!preview.Visible||list.Visible)throw new Exception("Preview menu click failed.");
                ((Button)preview.Controls[1]).PerformClick();checkUi();open();
                ApplyFontSize(18);checkUi();if(recordMenu.Font.Size!=18)throw new Exception("Open menu retained an obsolete font.");
                recordMenu.Close();ApplyFontSize(22);checkUi();open();recordMenu.Close();checkUi();
                return retained;
            } finally {Application.ThreadException-=capture;if(recordMenu!=null&&!recordMenu.IsDisposed)recordMenu.Close();}
        }
    }
}
