using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace MinHook;
public static class Kernel32
{
    
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
}
