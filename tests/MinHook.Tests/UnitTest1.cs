using System.Runtime.InteropServices;

namespace MinHook.Tests;

public class UnitTest1
{

    [DllImport("Kernel32.dll")]
    public static extern uint GetTickCount();

    [Fact]
    public void Test1()
    {
        StaticLazyHookManager.Enable();
        Assert.Equal(0u, GetTickCount());
        StaticLazyHookManager.Disable(alsoDisableAllEnabledHooks: true);
        Assert.NotEqual(0u, GetTickCount());
    }
}
