using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ClipboardNext {
    class ClipboardWindow : NativeWindow, IDisposable {
        public Action Changed, ShowRequested;
        public Action<int> HotkeyPressed;
        public ClipboardWindow() { CreateHandle(new CreateParams { Caption = "ClipboardNext.Listener" }); if (!Native.AddClipboardFormatListener(Handle)) throw new Win32Exception(); }
        protected override void WndProc(ref Message m) {
            if (m.Msg == 0x031D && Changed != null) Changed();
            if(m.Msg==0x0312&&HotkeyPressed!=null)HotkeyPressed(m.WParam.ToInt32());
            if ((uint)m.Msg == Program.ShowMessage && ShowRequested != null) ShowRequested();
            base.WndProc(ref m);
        }
        public void Dispose() { Native.RemoveClipboardFormatListener(Handle); DestroyHandle(); }
    }
    partial class AppContext : ApplicationContext {
        public Store Storage;
        public MainForm Panel;
        BallForm ball;
        NotifyIcon tray;
        ClipboardWindow listener;
        Timer clipboardRetry = new Timer(), targetTimer = new Timer();
        Timer cleanupTimer = new Timer();
        uint baseline, ownSequence;
        internal Func<uint> ReadClipboardSequence=Native.GetClipboardSequenceNumber;
        internal Func<IntPtr> ReadCaptureForeground=Native.GetForegroundWindow;
        internal Func<IDataObject> ReadClipboard=Clipboard.GetDataObject;
        int attempts;
        bool blocked, migrating, shuttingDown;
        IntPtr target;
        IntPtr foregroundHook;
        Native.WinEventCallback foregroundCallback;
        readonly uint processId = (uint)Process.GetCurrentProcess().Id;
        readonly string home, locatorPath;
        readonly bool isolatedTest;
        public bool Busy { get { return migrating; } }
        public void NotifyProblem(string message) {if(tray!=null&&!isolatedTest)tray.ShowBalloonTip(5000,L.T("clipboard历史剪切板"),message,ToolTipIcon.Warning);}

        public AppContext() : this(null, false) { }
        internal AppContext(string testHome, bool testMode) {
            isolatedTest = testMode;
            home = testHome ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipboardNext");
            Directory.CreateDirectory(home); locatorPath = Path.Combine(home, "location.xml");
            string root = Path.Combine(home, "Data"); bool existing = File.Exists(locatorPath);
            if(!existing&&!testMode){string legacy=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"IrisClipboardNext");string oldLocator=Path.Combine(legacy,"location.xml");if(File.Exists(oldLocator)){Disk.Write(locatorPath,Disk.Read<Locator>(oldLocator));existing=true;}else if(File.Exists(Path.Combine(legacy,"Data","application.marker"))){Disk.Write(locatorPath,new Locator{DataPath=Path.Combine(legacy,"Data")});existing=true;}}
            if (existing) root = Disk.Read<Locator>(locatorPath).DataPath;
            string problem = null;
            try { Storage = new Store(root, !existing); if (!existing) Disk.Write(locatorPath, new Locator { DataPath = root }); }
            catch (Exception ex) { blocked = true; problem = ex.Message; }
            L.Language=Storage!=null&&Storage.Settings.Language=="en"?"en":"zh-CN";
            Panel = new MainForm(this); ball = new BallForm(this);
            ball.RestorePosition();
            var menu = new ContextMenuStrip();
            menu.Items.Add(L.T("显示悬浮球"), null, (s, e) => ShowBall());
            menu.Items.Add(L.T("设置"), null, (s, e) => ShowSettings());
            menu.Items.Add(new ToolStripSeparator()); menu.Items.Add(L.T("退出软件"), null, (s, e) => Quit());
            tray = new NotifyIcon { Icon = Theme.AppIcon, Text = L.T("clipboard历史剪切板 · 本地剪贴板"), ContextMenuStrip = menu, Visible = true };
            tray.DoubleClick += (s, e) => ShowBall(); ball.ContextMenuStrip = menu;
            baseline = Native.GetClipboardSequenceNumber();
            if (!testMode) { listener = new ClipboardWindow(); listener.Changed = ClipboardChanged; listener.ShowRequested = OpenPanel;listener.HotkeyPressed=id=>{if(captureHotkey!=null&&captureHotkey.IsCurrent(id))BeginSelectiveCopy();};captureHotkey=new CaptureHotkey(listener.Handle); }
            selectiveTimer.Interval=30;selectiveTimer.Tick+=(s,e)=>AdvanceSelectiveCopy();
            try{ConfigureCapture(Storage==null?new Preferences():Storage.Settings);}catch(Exception ex){NotifyProblem(ex.Message);}
            clipboardRetry.Interval = 90; clipboardRetry.Tick += (s, e) => Capture();
            targetTimer.Interval = 60; targetTimer.Tick += (s, e) => TrackTarget(); targetTimer.Start(); TrackTarget();
            foregroundCallback = (hook, evt, window, obj, child, thread, time) => TrackTarget();
            foregroundHook = Native.SetWinEventHook(3, 3, IntPtr.Zero, foregroundCallback, 0, 0, 0);
            ball.Show(); OpenPanel(); if (problem != null) Fail(problem);
            SystemEvents.DisplaySettingsChanged += DisplayChanged;
            SystemEvents.SessionEnding += SessionEnding;
            cleanupTimer.Interval=60000; cleanupTimer.Tick+=(s,e)=>RunCleanup(); if(!testMode)cleanupTimer.Start();
        }
        void DisplayChanged(object sender, EventArgs args) { if (!ball.IsDisposed) ball.BeginInvoke((Action)(() => { ball.EnsureVisible(); Panel.EnsureVisible(); })); }
        internal void ApplyLanguage() {
            L.Language=Storage.Settings.Language=="en"?"en":"zh-CN";
            var old=Panel;var position=old.Location;bool shown=old.Visible;
            Panel=new MainForm(this);Panel.Location=position;old.Dispose();
            tray.Text=L.T("clipboard历史剪切板 · 本地剪贴板");
            tray.ContextMenuStrip.Items[0].Text=L.T("显示悬浮球");
            tray.ContextMenuStrip.Items[1].Text=L.T("设置");
            tray.ContextMenuStrip.Items[3].Text=L.T("退出软件");
            ball.Text=L.T("clipboard历史剪切板悬浮球");ball.ReloadAppearance();
            if(shown){Panel.Show();Panel.EnsureVisible();}
        }
        void SessionEnding(object sender, SessionEndingEventArgs args) { if (!migrating) SavePreferences(); }
        public void TrackTarget() {
            IntPtr current = Native.GetForegroundWindow(); uint pid;
            if (current == IntPtr.Zero) return; Native.GetWindowThreadProcessId(current, out pid);
            if (pid != processId) target = current;
        }
        public void ShowBall() { ball.EnsureVisible(); ball.Show(); if (Panel.Visible) Panel.Activate(); }
        public void OpenPanel() { TrackTarget(); ball.EnsureVisible(); ball.Show(); Panel.WindowState = FormWindowState.Normal; Panel.RefreshItems(); Panel.OpenNear(ball.Bounds); Panel.BringToFront(); }
        public void Collapse() { Panel.Hide(); ball.Show(); }
        void ClipboardChanged() {
            uint sequence = ReadClipboardSequence();
            if(Storage!=null&&Storage.Settings.HotkeyOnly&&!selectiveRequest.Allows(sequence,ReadCaptureForeground(),Environment.TickCount)){baseline=sequence;return;}
            if (migrating || blocked) { baseline = sequence; return; }
            if (sequence == baseline || sequence == ownSequence) { baseline = sequence; return; }
            attempts = 0; clipboardRetry.Start(); Capture();
        }
        void Capture() {
            if (blocked || migrating) { clipboardRetry.Stop(); return; }
            uint sequence = ReadClipboardSequence();
            if(Storage!=null&&Storage.Settings.HotkeyOnly&&!selectiveRequest.Allows(sequence,ReadCaptureForeground(),Environment.TickCount)){clipboardRetry.Stop();baseline=sequence;return;}
            if (sequence == ownSequence || sequence == baseline) { clipboardRetry.Stop(); return; }
            try {
                IDataObject data = ReadClipboard(); string text = null; Image image = null;
                if (data != null && !data.GetDataPresent(DataFormats.FileDrop)) {
                    if (data.GetDataPresent(DataFormats.Bitmap)) image = data.GetData(DataFormats.Bitmap) as Image;
                    else if (data.GetDataPresent(DataFormats.UnicodeText)) text = data.GetData(DataFormats.UnicodeText) as string;
                }
                if (sequence != ReadClipboardSequence()) { if (image != null) image.Dispose(); return; }
                using (image) {
                    if (image != null) Storage.AddImage(image); else if (!String.IsNullOrEmpty(text)) Storage.AddText(text);
                }
                baseline = sequence; clipboardRetry.Stop();bool requested=selectiveRequest.Active;CancelSelectiveCopy(); Panel.RefreshItems();
                if(requested)NotifyCaptureResult(image!=null||!String.IsNullOrEmpty(text)?L.T("已加入历史记录。"):L.T("所选内容不是文字或图片，未加入历史。"));
            } catch (ExternalException) {
                if (++attempts >= 8) { clipboardRetry.Stop(); baseline = sequence;CancelSelectiveCopy(); Panel.Status(L.T("剪贴板被其他软件占用，本次内容未保存。请重新复制。"),true); }
            } catch (Exception ex) { clipboardRetry.Stop(); baseline = sequence;CancelSelectiveCopy(); Fail(ex.Message); }
        }
        public void Fail(string message) { blocked = true; Panel.Status(L.T("记录已停止：") + message + L.T("  请到设置中重试。"), true); tray.ShowBalloonTip(5000, L.T("clipboard历史剪切板 · 未能保存"), message, ToolTipIcon.Warning); }
        public void Use(Clip clip) {
            if (migrating || clip == null || Storage == null) return;
            try {
                if (clip.Kind == "text") Clipboard.SetText(clip.Text, TextDataFormat.UnicodeText);
                else using (var image = Image.FromFile(Storage.ImagePath(clip))) Clipboard.SetImage(image);
                ownSequence = Native.GetClipboardSequenceNumber(); baseline = ownSequence;
                Storage.Touch(clip.Id);
                IntPtr destination = target; uint pid = 0;
                Native.GetWindowThreadProcessId(destination, out pid);
                if (destination == IntPtr.Zero || !Native.IsWindow(destination) || !Native.IsWindowVisible(destination) || Native.IsDesktop(destination) || pid == processId || !Native.SetForegroundWindow(destination)) {
                    Panel.Status(L.T("已复制，请选择输入位置后粘贴。")); AfterUse(); return;
                }
                var pasteTimer = new Timer { Interval = 100 };
                pasteTimer.Tick += (s, e) => {
                    pasteTimer.Stop(); pasteTimer.Dispose();
                    bool sent = Native.GetForegroundWindow() == destination && Native.GetClipboardSequenceNumber() == ownSequence && Native.Paste();
                    Panel.Status(sent ? L.T("已发起粘贴；如目标未接收，可手动 Ctrl+V。") : L.T("已复制，请选择输入位置后粘贴。")); AfterUse();
                }; pasteTimer.Start();
            } catch (Exception ex) { Panel.Status(L.T("复制失败，请重试：") + ex.Message, true); }
        }
        void AfterUse() { if (Storage != null && Storage.Settings.AutoCollapse) Collapse(); }
        public void SavePreferences() { if (Storage == null || migrating || blocked) return; try { Storage.SaveSettings(); } catch (Exception ex) { Fail(ex.Message); } }
        public void Delete(string[] ids) {
            if (migrating || Storage == null) return;
            try { Storage.Delete(ids); Panel.EndBatch(); Panel.Status(L.T("已永久删除 ") + ids.Length + L.T(" 条记录。")); }
            catch (Exception ex) { Panel.EndBatch(); Fail(L.T("删除未全部完成，请检查剩余记录。") + ex.Message); }
        }
        bool AutoStart() { if(isolatedTest)return false; using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) return key != null && (key.GetValue("ClipboardNext") != null || key.GetValue("IrisClipboardNext") != null); }
        void SetAutoStart(bool value) {
            if (isolatedTest) return;
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) {
                key.DeleteValue("IrisClipboardNext",false);
                if (value) key.SetValue("ClipboardNext", "\"" + Application.ExecutablePath + "\""); else key.DeleteValue("ClipboardNext", false);
            }
        }
        public void ShowSettings() {
            if (migrating) return;
            using(var dialog = BuildSettings()){FeatureDialogOpen=true;try{dialog.ShowDialog();}finally{FeatureDialogOpen=false;}} RunCleanup();
        }
        internal Form BuildSettings() { return BuildSettingsPage(0); }
        bool RetryStorage() {
            try {
                string path = Storage != null ? Storage.Root : Disk.Read<Locator>(locatorPath).DataPath;
                var recovered = new Store(path, false); recovered.SaveSettings(); Storage = recovered;
                blocked = false; baseline = Native.GetClipboardSequenceNumber(); Panel.RefreshItems(); Panel.Status(L.T("存储已恢复，开始记录之后新复制的内容。")); return true;
            } catch (Exception ex) { Panel.Status(L.T("仍无法使用原位置：") + ex.Message, true); return false; }
        }
        void Migrate(string selected) {
            migrating = true; clipboardRetry.Stop(); OpenPanel(); Panel.SetBusy(true); Panel.Status(L.T("正在复制并校验数据… 暂停记录，请等待。"));
            Store old = Storage; var worker = new BackgroundWorker();
            worker.DoWork += (s, e) => e.Result = old.CopyVerified(selected);
            worker.RunWorkerCompleted += (s, e) => {
                string result;
                if (e.Error != null) result = L.T("迁移失败，继续使用原位置：") + e.Error.Message;
                else {
                    try {
                        Store copied = (Store)e.Result; Disk.Write(locatorPath, new Locator { DataPath = copied.Root }); Storage = copied;
                        try { old.CleanMigratedCopy(); result = L.T("迁移完成，原应用数据已清理。"); }
                        catch (Exception ex) { result = L.T("已切换新位置，旧副本清理失败：") + ex.Message; }
                    } catch (Exception ex) { result = L.T("切换失败，继续使用原位置；目标副本仍保留：") + ex.Message; }
                }
                migrating = false; baseline = Native.GetClipboardSequenceNumber(); Panel.SetBusy(false); Panel.EndBatch(); Panel.Status(result); worker.Dispose();
            }; worker.RunWorkerAsync();
        }
        public void Quit() {
            if (migrating) { Panel.Status(L.T("正在迁移，请完成后退出。")); return; }
            if (shuttingDown) return; shuttingDown = true; SavePreferences();
            SystemEvents.DisplaySettingsChanged -= DisplayChanged; SystemEvents.SessionEnding -= SessionEnding;
            selectiveTimer.Dispose();if(captureHotkey!=null)captureHotkey.Dispose();cleanupTimer.Dispose(); targetTimer.Dispose(); clipboardRetry.Dispose(); if (listener != null) listener.Dispose(); tray.Visible = false; tray.Dispose();
            if (foregroundHook != IntPtr.Zero) Native.UnhookWinEvent(foregroundHook);
            ball.Dispose(); Panel.Dispose(); ExitThread();
        }
    }
}
