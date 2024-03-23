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
        Assert.Equal(0u, GetTickCount());
        TestLazyHook.Disable();
        Assert.NotEqual(0u, GetTickCount());
    }

    [Fact]
    public async Task Test2()
    {
        SslHook.Enable();
        DnsHook.Enable();
        var httpClient = new HttpClient();
        await httpClient.GetStringAsync("https://pixiv.net/artworks/101648429");
    }
}
