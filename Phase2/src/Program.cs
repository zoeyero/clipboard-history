using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace ClipboardNext {
    static class Program {
        public static readonly uint ShowMessage = Native.RegisterWindowMessage("IrisClipboardNext.Show.v1");
        [STAThread] static int Main(string[] args) {
            Native.SetProcessDPIAware();TaskbarIdentity.Initialize(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length > 0 && args[0] == "--self-test") return SelfTests.Run(args.Length > 1 ? args[1] : "test-results");
            if (args.Length > 0 && args[0] == "--ui-smoke") return SelfTests.Render(args.Length > 1 ? args[1] : "test-results");
            if (args.Length > 0 && args[0] == "--startup-smoke") return SelfTests.Startup(args.Length > 1 ? args[1] : "test-results");
            if (args.Length > 0 && args[0] == "--phase2-test") return Phase2Tests.Run(args.Length > 1 ? args[1] : "test-results");
            if (args.Length > 0 && args[0] == "--capture-test") return CaptureTests.Run(args.Length > 1 ? args[1] : "test-results");
            if (args.Length > 0 && args[0] == "--language-test") return LanguageTests.Run(args.Length > 1 ? args[1] : "test-results");
            bool first;
            using (var mutex = new Mutex(true, "Local\\IrisClipboardNext.v1", out first)) {
                if (!first) { Native.PostMessage(new IntPtr(0xffff), ShowMessage, IntPtr.Zero, IntPtr.Zero); return 0; }
                try {
                    Application.Run(new AppContext()); return 0;
                } catch (Exception ex) { MessageBox.Show(L.T("启动失败，已有数据未被主动清空。\n") + ex.Message, L.T("clipboard历史剪切板 · 启动异常")); return 1; }
            }
        }
    }
}
