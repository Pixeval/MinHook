using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MinHook;
public class Hook<T> where T : Delegate
{
    private readonly IntPtr _target;
    private readonly IntPtr _original;
    private readonly IntPtr _detour;

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

    public Hook(IntPtr target, T detour)
    {
        _target = target;
        _detour = Marshal.GetFunctionPointerForDelegate(detour);
    }

    protected T CallOriginal => Marshal.GetDelegateForFunctionPointer<T>(_original);

    private void Initialize()
    {
        var status = Native.Initialize();
        if (status != MinHookStatus.Ok && status != MinHookStatus.ErrorAlreadyInitialized)
        {
            MinHookException.Throw(status);
        }
    }

    public void Enable()
    {
        var status = Native.EnableHook(_target);
        if (status != MinHookStatus.Ok)
        {
            MinHookException.Throw(status);
        }
    }

    public void Disable()
    {
        var status = Native.DisableHook(_target);
        if (status != MinHookStatus.Ok)
        {
            MinHookException.Throw(status);
        }
    }
}
