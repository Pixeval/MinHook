using System.Runtime.InteropServices;
using MinHook.Attributes;

namespace MinHook.Tests
{
    [StaticLazyHook<InitializeSecurityContextWDelegate>("sspicli", "InitializeSecurityContextW")]
    internal partial class SslHook
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public delegate int InitializeSecurityContextWDelegate(
            IntPtr phCredential,
            IntPtr phContext,
            string pTargetName,
            uint fContextReq,
            uint Reserved1,
            uint TargetDataRep,
            IntPtr pInput,
            uint Reserved2,
            out IntPtr phNewContext,
            IntPtr pOutput,
            out uint pfContextAttr,
            out long ptsExpiry
        );

        private static unsafe partial int Detour(IntPtr phCredential, IntPtr phContext, string pTargetName, uint fContextReq, uint Reserved1,
            uint TargetDataRep, IntPtr pInput, uint Reserved2, out IntPtr phNewContext, IntPtr pOutput, out uint pfContextAttr,
            out long ptsExpiry)
        {
            if (pTargetName.Contains("pixiv"))
            {
                pTargetName = "";
            }

            return Original(phCredential, phContext, pTargetName, fContextReq, Reserved1, TargetDataRep, pInput,
                Reserved2, out phNewContext, pOutput, out pfContextAttr, out ptsExpiry);
        }
    }
}
