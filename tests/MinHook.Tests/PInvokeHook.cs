using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using MinHook.Attributes;

namespace MinHook.Tests;

[StaticLazyHook<GetProcAddressDelegate>("kernel32", "GetProcAddress")]
internal partial class PInvokeHook
{

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate IntPtr GetProcAddressDelegate(IntPtr hModule, IntPtr procName);

    private static List<string> s_methods = new List<string>();

    private static unsafe partial IntPtr Detour(IntPtr hModule, IntPtr procName)
    {
        s_methods.Add(Marshal.PtrToStringUTF8(procName));
        return Original(hModule, procName);
    }
}
