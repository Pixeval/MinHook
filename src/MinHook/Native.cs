using System.Runtime.InteropServices;
// ReSharper disable StringLiteralTypo

namespace MinHook
{
    public enum MinHookStatus
    {
        Unknown = -1,
        Ok = 0,
        ErrorAlreadyInitialized = 1,
        ErrorNotInitialized = 2,
        ErrorAlreadyCreated = 3,
        ErrorNotCreated = 4,
        Enabled = 5,
        Disabled = 6,
        ErrorNotExecutable = 7,
        ErrorUnsupportedFunction = 8,
        ErrorMemoryAlloc = 9,
        ErrorMemoryProtect = 10,
        ErrorModuleNotFound = 11,
        ErrorFunctionNotFound = 12,
        ErrorMutexFailure = 13
    }

    enum MhThreadFreezeMethod
    {
        /// <summary>
        /// The original MinHook method, using CreateToolhelp32Snapshot. Documented
        /// and supported on all Windows versions, but very slow and less reliable.
        /// </summary>
        Original = 0,

        /// <summary>
        /// A much faster and more reliable, but undocumented method, using
        /// NtGetNextThread. Supported since Windows Vista, on older versions falls
        /// back to MH_ORIGINAL.
        /// </summary>
        FastUndocumented = 1,

        /// <summary>
        /// Threads are not suspended and instruction pointer registers are not
        /// adjusted. Don't use this method unless you understand the implications
        /// and know that it's safe.
        /// </summary>
        NoneUnsafe = 2
    }

    internal static class Native
    {
        [DllImport("minhook", EntryPoint = "MH_Initialize", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        public static extern MinHookStatus Initialize();

        [DllImport("minhook", EntryPoint = "MH_Uninitialize", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        public static extern MinHookStatus Uninitialize();

        [DllImport("minhook", EntryPoint = "MH_SetThreadFreezeMethod", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        public static extern MinHookStatus SetThreadFreezeMethod(MhThreadFreezeMethod method);

        [DllImport("minhook", EntryPoint = "MH_CreateHook", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        public static extern MinHookStatus CreateHook(IntPtr pTarget, IntPtr pDetour, out IntPtr ppOriginal);

        [DllImport("minhook", EntryPoint = "MH_CreateHookApi", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        public static extern MinHookStatus CreateHookApi([MarshalAs(UnmanagedType.LPWStr)] string pszModule, string pszProcName, IntPtr pDetour, out IntPtr ppOriginal);

        [DllImport("minhook", EntryPoint = "MH_CreateHookApiEx", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        public static extern MinHookStatus CreateHookApiEx([MarshalAs(UnmanagedType.LPWStr)] string pszModule, string pszProcName, IntPtr pDetour, out IntPtr ppOriginal, out IntPtr ppTarget);

        [DllImport("minhook", EntryPoint = "MH_RemoveHook", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        public static extern MinHookStatus RemoveHook(IntPtr pTarget);

        [DllImport("minhook", EntryPoint = "MH_EnableHook", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        public static extern MinHookStatus EnableHook(IntPtr pTarget);

        [DllImport("minhook", EntryPoint = "MH_DisableHook", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        public static extern MinHookStatus DisableHook(IntPtr pTarget);

        [DllImport("minhook", EntryPoint = "MH_QueueEnableHook", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        public static extern MinHookStatus QueueEnableHook(IntPtr pTarget);

        [DllImport("minhook", EntryPoint = "MH_QueueDisableHook", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        public static extern MinHookStatus QueueDisableHook(IntPtr pTarget);

        [DllImport("minhook", EntryPoint = "MH_ApplyQueued", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        public static extern MinHookStatus ApplyQueued();
    }
}
