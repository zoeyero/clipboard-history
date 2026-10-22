using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace ClipboardNext {
    partial class AppContext {
        internal void VerifySelectiveRecording(){
            uint sequence=100;IntPtr foreground=new IntPtr(123);IDataObject data=new DataObject(DataFormats.UnicodeText,"ordinary copy");int reads=0;
            ReadClipboardSequence=()=>sequence;ReadCaptureForeground=()=>foreground;ReadClipboard=()=>{reads++;return data;};
            Storage.Settings.HotkeyOnly=true;baseline=99;
            ClipboardChanged();if(reads!=0||Storage.Items.Count!=0)throw new Exception("Ordinary copies must not be read in selective mode.");
            selectiveRequest.Begin(foreground,Environment.TickCount);selectiveRequest.MarkSent(sequence,Environment.TickCount);ClipboardChanged();
            if(reads!=0)throw new Exception("Stale clipboard data was read.");
            sequence++;data=new DataObject(DataFormats.UnicodeText,"explicit saved selection");ClipboardChanged();
            if(Storage.Items.Count!=1||selectiveRequest.Active||reads!=1)throw new Exception("Explicit copy was not saved exactly once.");
            sequence++;data=new DataObject(DataFormats.UnicodeText,"ordinary second copy");ClipboardChanged();if(reads!=1||Storage.Items.Count!=1)throw new Exception("Following ordinary copy was recorded.");
            selectiveRequest.Begin(foreground,Environment.TickCount);selectiveRequest.MarkSent(sequence,Environment.TickCount);foreground=new IntPtr(124);sequence++;ClipboardChanged();if(reads!=1)throw new Exception("Changed foreground accepted.");
            CancelSelectiveCopy();selectiveRequest.Begin(foreground,Environment.TickCount);selectiveRequest.MarkSent(sequence,Environment.TickCount-2000);sequence++;ClipboardChanged();if(reads!=1)throw new Exception("Late clipboard accepted.");
            CancelSelectiveCopy();Storage.Settings.HotkeyOnly=false;sequence++;ClipboardChanged();if(Storage.Items.Count!=2)throw new Exception("Automatic mode no longer captures ordinary copies.");
            Storage.Settings.HotkeyOnly=true;using(var image=new Bitmap(12,12)){
                data=new DataObject();data.SetData(DataFormats.Bitmap,image);selectiveRequest.Begin(foreground,Environment.TickCount);selectiveRequest.MarkSent(sequence,Environment.TickCount);sequence++;ClipboardChanged();
                if(Storage.Items.Count!=3||!Storage.Items.Any(c=>c.Kind=="image"))throw new Exception("Selective image capture failed.");
            }
            int before=reads;selectiveRequest.Begin(foreground,Environment.TickCount);selectiveRequest.MarkSent(sequence,Environment.TickCount);sequence++;ownSequence=sequence;ClipboardChanged();if(reads!=before)throw new Exception("Own clipboard write recorded.");
            CancelSelectiveCopy();ReadClipboardSequence=Native.GetClipboardSequenceNumber;ReadCaptureForeground=Native.GetForegroundWindow;ReadClipboard=Clipboard.GetDataObject;
        }
    }
    static class CaptureTests {
        public static int Run(string folder){
            Directory.CreateDirectory(folder);AppContext app=null;
            try{
                var request=new SelectiveCopyRequest();request.Begin(new IntPtr(1),Int32.MaxValue-20);request.MarkSent(9,Int32.MaxValue-20);
                if(!request.Allows(10,new IntPtr(1),Int32.MinValue+20))throw new Exception("Tick wrap rejected valid request.");
                bool rejected=false;try{CaptureHotkey.Validate(2,67);}catch(ArgumentException){rejected=true;}if(!rejected)throw new Exception("Recursive Ctrl+C binding allowed.");CaptureHotkey.Validate(2,88);
                using(var first=new Form())using(var second=new Form())using(var a=new CaptureHotkey(first.Handle))using(var b=new CaptureHotkey(second.Handle)){
                    var prefs=new Preferences{HotkeyOnly=true,CaptureModifiers=7,CaptureKey=122};a.Apply(prefs);
                    rejected=false;try{b.Apply(prefs);}catch(InvalidOperationException){rejected=true;}if(!rejected)throw new Exception("Hotkey conflict was not detected.");
                    var other=new Preferences{HotkeyOnly=true,CaptureModifiers=7,CaptureKey=121};b.Apply(other);
                    rejected=false;try{a.Apply(other);}catch(InvalidOperationException){rejected=true;}if(!rejected||!a.IsCurrent(0x4110))throw new Exception("Conflict lost previous binding.");
                    a.Apply(new Preferences());b.Apply(prefs);b.Apply(new Preferences());
                }
                app=new AppContext(Path.Combine(Path.GetFullPath(folder),"capture-"+Guid.NewGuid().ToString("N")),true);app.VerifySelectiveRecording();
                using(var settings=app.BuildSettingsPage(5)){
                    settings.StartPosition=FormStartPosition.Manual;settings.Location=new Point(-10000,-10000);settings.Show();Application.DoEvents();
                    var shortcut=(TextBox)settings.Controls.Find("captureShortcut",true).Single();
                    using(var bitmap=new Bitmap(settings.Width,settings.Height)){settings.DrawToBitmap(bitmap,new Rectangle(Point.Empty,settings.Size));bitmap.Save(Path.Combine(folder,"capture-settings.png"),ImageFormat.Png);}
                    ((Button)settings.Controls.Find("applySettings",true).Single()).PerformClick();Application.DoEvents();
                    var saved=new Store(app.Storage.Root,false).Settings;if(!saved.HotkeyOnly||saved.CaptureKey!=67||saved.CaptureModifiers!=6)throw new Exception("Capture settings did not persist.");
                    ((RadioButton)settings.Controls.Find("captureAutomatic",true).Single()).Checked=true;
                    ((Button)settings.Controls.Find("saveSettings",true).Single()).PerformClick();Application.DoEvents();
                    if(new Store(app.Storage.Root,false).Settings.HotkeyOnly)throw new Exception("Automatic mode did not persist.");
                }
                app.Quit();app=null;
                File.WriteAllText(Path.Combine(folder,"capture-tests.txt"),"PASS: no reads for ordinary copies in selective mode; fresh explicit text and image capture; stale, late, wrong-focus and own writes rejected; auto mode restored; tick rollover; native registration/conflict/rebind/release; settings persistence. Synthetic clipboard only, no SendInput or user clipboard changes.");return 0;
            }catch(Exception ex){File.WriteAllText(Path.Combine(folder,"capture-tests.txt"),ex.ToString());return 1;}
            finally{if(app!=null)app.Quit();}
        }
    }
}
