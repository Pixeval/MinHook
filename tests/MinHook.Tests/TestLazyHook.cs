using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using MinHook.Attributes;

namespace MinHook.Tests;

[UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
public delegate uint GetTickCountDelegate();

[StaticLazyHook<GetTickCountDelegate>("Kernel32", "GetTickCount")]

internal partial class TestLazyHook
{
    private static partial uint Detour()
    {
        return 0;
    }
}
