using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace IrisClipboardNext {
    sealed class CaptureHotkey:IDisposable {
        readonly IntPtr window;int currentId,modifiers,key;
        [DllImport("user32.dll",SetLastError=true)]static extern bool RegisterHotKey(IntPtr window,int id,uint modifiers,uint key);
        [DllImport("user32.dll",SetLastError=true)]static extern bool UnregisterHotKey(IntPtr window,int id);
        public CaptureHotkey(IntPtr window){this.window=window;}
        internal bool IsCurrent(int id){return currentId!=0&&id==currentId;}
        internal static void Validate(int modifiers,int key){
            bool letter=key>=65&&key<=90,number=key>=48&&key<=57,function=key>=112&&key<=122;
            if(modifiers<1||(modifiers&~7)!=0||(!letter&&!number&&!function))throw new ArgumentException(L.T("请选择 Ctrl、Alt 或 Shift 加字母、数字或 F1–F11 的组合。"));
            if(modifiers==2&&key==67)throw new ArgumentException(L.T("Ctrl+C 请使用自动记录模式；仅快捷键记录可选择 Ctrl+Shift+C 或其他组合。"));
        }
        internal static string Format(int modifiers,int key){return ((modifiers&2)!=0?"Ctrl+":"")+((modifiers&1)!=0?"Alt+":"")+((modifiers&4)!=0?"Shift+":"")+((Keys)key).ToString();}
        public void Apply(Preferences next){
            if(!next.HotkeyOnly){if(currentId!=0&&!UnregisterHotKey(window,currentId))throw new Win32Exception();currentId=0;return;}
            Validate(next.CaptureModifiers,next.CaptureKey);
            if(currentId!=0&&modifiers==next.CaptureModifiers&&key==next.CaptureKey)return;
            int nextId=currentId==0x4110?0x4111:0x4110;
            if(!RegisterHotKey(window,nextId,(uint)next.CaptureModifiers|0x4000,(uint)next.CaptureKey))throw new InvalidOperationException(L.T("快捷键 ")+Format(next.CaptureModifiers,next.CaptureKey)+L.T(" 被占用或不可用，请换一个组合。原设置保持不变。"));
            if(currentId!=0&&!UnregisterHotKey(window,currentId)){UnregisterHotKey(window,nextId);throw new Win32Exception();}
            currentId=nextId;modifiers=next.CaptureModifiers;key=next.CaptureKey;
        }
        public void Dispose(){if(currentId!=0){UnregisterHotKey(window,currentId);currentId=0;}}
    }
    // A request accepts only a new clipboard sequence from the same foreground
    // window after the explicit copy. It never falls back to old clipboard data.
    sealed class SelectiveCopyRequest {
        public bool Active,Sent;public IntPtr Window;uint initial;int started;
        public void Begin(IntPtr window,int now){Active=true;Sent=false;Window=window;started=now;}
        public void MarkSent(uint sequence,int now){initial=sequence;Sent=true;started=now;}
        public bool Expired(int now){return Active&&unchecked((uint)(now-started))>(Sent?1800u:2500u);}
        public bool Allows(uint sequence,IntPtr window,int now){return Active&&Sent&&sequence!=initial&&window==Window&&!Expired(now);}
        public void Reset(){Active=Sent=false;Window=IntPtr.Zero;}
    }
    partial class AppContext {
        CaptureHotkey captureHotkey;
        readonly SelectiveCopyRequest selectiveRequest=new SelectiveCopyRequest();
        readonly Timer selectiveTimer=new Timer();
        void ConfigureCapture(Preferences preferences){
            if(preferences.HotkeyOnly)CaptureHotkey.Validate(preferences.CaptureModifiers,preferences.CaptureKey);
            if(captureHotkey!=null)captureHotkey.Apply(preferences);
            CancelSelectiveCopy();clipboardRetry.Stop();baseline=Native.GetClipboardSequenceNumber();
        }
        void CancelSelectiveCopy(){selectiveTimer.Stop();selectiveRequest.Reset();}
        void NotifyCaptureResult(string message){if(tray!=null&&!isolatedTest)tray.ShowBalloonTip(2500,L.T("clipboard历史剪切板"),message,ToolTipIcon.Info);}
        void BeginSelectiveCopy(){
            if(Storage==null||!Storage.Settings.HotkeyOnly||blocked||migrating||selectiveRequest.Active)return;
            IntPtr window=Native.GetForegroundWindow();uint pid;Native.GetWindowThreadProcessId(window,out pid);
            if(window==IntPtr.Zero||pid==processId||Native.IsDesktop(window)){NotifyCaptureResult(L.T("请先在其他软件中选中要保存的内容，再按快捷键。"));return;}
            clipboardRetry.Stop();selectiveRequest.Begin(window,Environment.TickCount);selectiveTimer.Start();
        }
        void AdvanceSelectiveCopy(){
            if(!selectiveRequest.Active){selectiveTimer.Stop();return;}
            if(blocked||migrating||Storage==null||!Storage.Settings.HotkeyOnly){CancelSelectiveCopy();return;}
            int now=Environment.TickCount;
            if(Native.GetForegroundWindow()!=selectiveRequest.Window||selectiveRequest.Expired(now)){
                clipboardRetry.Stop();CancelSelectiveCopy();NotifyCaptureResult(L.T("未取得新的复制内容，未添加记录。请保持原窗口，选中内容后重试。"));return;
            }
            if(!selectiveRequest.Sent){
                foreach(int key in new[]{0x10,0x11,0x12,0x5B,0x5C,Storage.Settings.CaptureKey})if(Native.GetAsyncKeyState(key)<0)return;
                baseline=Native.GetClipboardSequenceNumber();selectiveRequest.MarkSent(baseline,now);
                if(!Native.CopySelection()){CancelSelectiveCopy();NotifyCaptureResult(L.T("无法向当前软件发起复制，未添加记录。"));}
            }
        }
    }
}
