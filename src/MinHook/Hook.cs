using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MinHook;

public static class Hook
{


    public static Hook<T> Create<T>(string moduleName, string funcName, IntPtr detour) where T : Delegate
    {
        var handle = Kernel32.GetModuleHandle(moduleName);
        var target = Kernel32.GetProcAddress(handle, funcName);
        return new Hook<T>(target, detour);
    }
    public static Hook<T> Create<T>(string moduleName, string funcName, T detour) where T : Delegate
    {
        return Create<T>(moduleName, funcName, Marshal.GetFunctionPointerForDelegate(detour));
    }
    public static Hook<T> Create<T>(IntPtr target, T detour) where T : Delegate
    {
        return new Hook<T>(target, Marshal.GetFunctionPointerForDelegate(detour));
    }
    public static Hook<T> Create<T>(IntPtr target, IntPtr detour) where T : Delegate
    {
        return new Hook<T>(target, detour);
    }
}

public class Hook<T> where T : Delegate
{
    private readonly IntPtr _target;
    private readonly IntPtr _original;
    private readonly IntPtr _detour;
    private bool _enabled;

    public Hook(IntPtr target, IntPtr detour)
    {
        _target = target;
        _detour = detour;
        Initialize();
        var status = Native.CreateHook(target, _detour, out _original);
        if (status != MinHookStatus.Ok)
        {
            MinHookException.Throw(status);
        }
    }

    public T Original => Marshal.GetDelegateForFunctionPointer<T>(_original);

    public bool Enabled => _enabled;

    private void Initialize()
    {
        var status = Native.Initialize();
        if (status != MinHookStatus.Ok && status != MinHookStatus.ErrorAlreadyInitialized)
        {
            MinHookException.Throw(status);
        }
        status = Native.SetThreadFreezeMethod(MhThreadFreezeMethod.NoneUnsafe);
        if (status != MinHookStatus.Ok)
        {
            MinHookException.Throw(status);
        }

    }

    public void Enable()
    {
        var status = Native.EnableHook(_target);
        if (status != MinHookStatus.Ok && status != MinHookStatus.Enabled)
        {
            MinHookException.Throw(status);
        }
        _enabled = true;
    }

    public void Disable()
    {
        var status = Native.DisableHook(_target);
        if (status != MinHookStatus.Ok && status != MinHookStatus.Disabled)
        {
            MinHookException.Throw(status);
        }
        _enabled = false;
    }
}
