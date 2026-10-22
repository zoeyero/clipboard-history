using System;
using System.Runtime.InteropServices;

namespace ClipboardNext {
    static class TaskbarIdentity {
        // Stable product identity, independent of the old automatically inferred Next identity.
        internal const string Id="ClipboardNext.History";
        [DllImport("shell32.dll",CharSet=CharSet.Unicode)]
        static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
        [DllImport("shell32.dll")]
        static extern int GetCurrentProcessExplicitAppUserModelID(out IntPtr appId);
        public static void Initialize(){Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(Id));}
        internal static void Verify(){
            IntPtr value;Marshal.ThrowExceptionForHR(GetCurrentProcessExplicitAppUserModelID(out value));
            try{if(Marshal.PtrToStringUni(value)!=Id)throw new Exception("Taskbar application identity was not applied.");}
            finally{Marshal.FreeCoTaskMem(value);}
        }
    }
}
