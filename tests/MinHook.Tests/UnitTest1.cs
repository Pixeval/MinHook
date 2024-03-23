using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MinHook.Tests;

public class UnitTest1
{

    [DllImport("kernel32.dll")]
    public static extern uint GetTickCount();


    [Fact]
    public void Test1()
    {
        TestLazyHook.Enable();
        Assert.Equal(0u,GetTickCount());
        TestLazyHook.Disable();
        Assert.NotEqual(0u,GetTickCount());
    }
}
