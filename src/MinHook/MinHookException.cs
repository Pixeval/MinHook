using System;
using System.Collections.Generic;
using System.Text;

namespace MinHook;
public class MinHookException(MinHookStatus status) : Exception(status.ToString())
{
    public MinHookStatus Status { get; private set; } = status;

    public static MinHookException Throw(MinHookStatus status)
    {
        throw new MinHookException(status);
    }
}
