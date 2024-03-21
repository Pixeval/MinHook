using System;
using System.Collections.Generic;
using System.Text;

namespace MinHook.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class StaticLazyHookAttribute<T> : Attribute where T : Delegate
{
    public StaticLazyHookAttribute(string moduleName, string functionName)
    {

    }
}
