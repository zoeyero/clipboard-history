using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ClipboardNext {
    static class Native {
        [DllImport("user32.dll")] public static extern bool AddClipboardFormatListener(IntPtr h);
        [DllImport("user32.dll")] public static extern bool RemoveClipboardFormatListener(IntPtr h);
        [DllImport("user32.dll")] public static extern uint GetClipboardSequenceNumber();
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint process);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern uint RegisterWindowMessage(string name);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint message, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
        public delegate void WinEventCallback(IntPtr hook, uint evt, IntPtr window, int objectId, int childId, uint thread, uint time);
        [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventCallback callback, uint process, uint thread, uint flags);
        [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr handle, StringBuilder text, int size);
        public static bool IsDesktop(IntPtr window) { var text = new StringBuilder(256); GetClassName(window, text, text.Capacity); return text.ToString() == "Progman" || text.ToString() == "WorkerW" || text.ToString() == "Shell_TrayWnd" || text.ToString() == "Shell_SecondaryTrayWnd"; }
        [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint count, INPUT[] inputs, int size);
        [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public UNION data; }
        [StructLayout(LayoutKind.Explicit)] struct UNION {
            [FieldOffset(0)] public KEYBDINPUT keyboard;
            [FieldOffset(0)] public MOUSEINPUT mouse;
        }
        [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort key, scan; public uint flags, time; public UIntPtr extra; }
        [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int x, y; public uint mouseData, flags, time; public UIntPtr extra; }
        static INPUT Key(ushort key, bool up) { return new INPUT { type = 1, data = new UNION { keyboard = new KEYBDINPUT { key = key, flags = up ? 2u : 0u } } }; }
        public static bool CopySelection() {
            foreach(int key in new[]{0x10,0x11,0x12,0x5B,0x5C})if(GetAsyncKeyState(key)<0)return false;
            INPUT[] keys={Key(0x11,false),Key(0x43,false),Key(0x43,true),Key(0x11,true)};
            uint sent=SendInput((uint)keys.Length,keys,Marshal.SizeOf(typeof(INPUT)));
            if(sent>0&&sent<keys.Length){INPUT[] release={Key(0x43,true),Key(0x11,true)};SendInput(2,release,Marshal.SizeOf(typeof(INPUT)));}
            return sent==keys.Length;
        }
        public static bool Paste() {
            // Do not synthesize over a modifier the user is physically holding.
            foreach (int key in new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C }) if (GetAsyncKeyState(key) < 0) return false;
            INPUT[] keys = { Key(0x11, false), Key(0x56, false), Key(0x56, true), Key(0x11, true) };
            uint sent = SendInput((uint)keys.Length, keys, Marshal.SizeOf(typeof(INPUT)));
            if (sent > 0 && sent < keys.Length) { INPUT[] release = { Key(0x56, true), Key(0x11, true) }; SendInput(2, release, Marshal.SizeOf(typeof(INPUT))); }
            return sent == keys.Length;
        }
    }
}
