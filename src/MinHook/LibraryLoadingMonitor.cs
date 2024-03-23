using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MinHook;
public class LibraryLoadingMonitor
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall, BestFitMapping = true, CharSet = CharSet.Ansi)]
    public delegate IntPtr LoadLibraryADelegate(string lpLibFileName);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, BestFitMapping = true, CharSet = CharSet.Unicode)]
    public delegate IntPtr LoadLibraryWDelegate(string lpLibFileName);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, BestFitMapping = true, CharSet = CharSet.Ansi)]
    public delegate IntPtr LoadLibraryExADelegate(string lpLibFileName, IntPtr hFile, uint dwFlags);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, BestFitMapping = true, CharSet = CharSet.Unicode)]
    public delegate IntPtr LoadLibraryExWDelegate(string lpLibFileName, IntPtr hFile, uint dwFlags);


    private static Hook<LoadLibraryADelegate> s_hook1 = Hook.Create<LoadLibraryADelegate>("kernel32", "LoadLibraryA", Hook_LoadLibraryA);
    private static Hook<LoadLibraryWDelegate> s_hook2 = Hook.Create<LoadLibraryWDelegate>("kernel32", "LoadLibraryW", Hook_LoadLibraryW);
    private static Hook<LoadLibraryExADelegate> s_hook3 = Hook.Create<LoadLibraryExADelegate>("kernel32", "LoadLibraryExA", Hook_LoadLibraryExA);
    private static Hook<LoadLibraryExWDelegate> s_hook4 = Hook.Create<LoadLibraryExWDelegate>("kernel32", "LoadLibraryExW", Hook_LoadLibraryExW);

    public static bool Enabled { get; private set; }

    private static IntPtr Hook_LoadLibraryA(string lpLibFileName)
    {
        OnLibraryLoading(lpLibFileName);
        var handle = s_hook1.Original(lpLibFileName);
        OnLibraryLoaded(handle);
        return handle;
    }

    private static IntPtr Hook_LoadLibraryW(string lpLibFileName)
    {
        OnLibraryLoading(lpLibFileName);
        var handle = s_hook2.Original(lpLibFileName);
        OnLibraryLoaded(handle);
        return handle;
    }
    private static IntPtr Hook_LoadLibraryExA(string lpLibFileName, IntPtr hFile, uint dwFlags)
    {
        OnLibraryLoading(lpLibFileName);
        var handle = s_hook3.Original(lpLibFileName, hFile, dwFlags);
        OnLibraryLoaded(handle);
        return handle;
    }
    private static IntPtr Hook_LoadLibraryExW(string lpLibFileName, IntPtr hFile, uint dwFlags)
    {
        OnLibraryLoading(lpLibFileName);
        var handle = s_hook4.Original(lpLibFileName, hFile, dwFlags);
        OnLibraryLoaded(handle);
        return handle;
    }

    public static void Enable()
    {
        s_hook1.Enable();
        s_hook2.Enable();
        s_hook3.Enable();
        s_hook4.Enable();
    }

    public static void Disable()
    {
        s_hook1.Disable();
        s_hook2.Disable();
        s_hook3.Disable();
        s_hook4.Disable();
    }

    private static void OnLibraryLoading(string libraryName)
    {
        LibraryLoading?.Invoke(null, new LibraryLoadingEventArgs(libraryName));
    }

    private static void OnLibraryLoaded(IntPtr handle)
    {
        LibraryLoaded?.Invoke(null, new LibraryLoadedEventArgs(handle));
    }

    public static event EventHandler<LibraryLoadingEventArgs>? LibraryLoading;
    public static event EventHandler<LibraryLoadedEventArgs>? LibraryLoaded;

    public record struct LibraryLoadingEventArgs(string LibraryName);
    public record struct LibraryLoadedEventArgs(IntPtr Handle);
}
