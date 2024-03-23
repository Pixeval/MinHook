using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MinHook.Attributes;

namespace MinHook.Tests
{

    [StaticLazyHook<GetAddrInfoExWDelegate>("ws2_32", "GetAddrInfoExW")]
    internal partial class DnsHook
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public unsafe delegate int GetAddrInfoExWDelegate(
            string pName,
            IntPtr pServiceName,
            int dwNameSpace,
            IntPtr lpNspId,
            IntPtr hints,
            AddressInfoEx** ppResult,
            IntPtr timeout,
            IntPtr lpOverlapped,
            IntPtr lpCompletionRoutine,
            IntPtr* lpHandle
        );

        [Flags]
        internal enum AddressInfoHints
        {
            AI_PASSIVE = 0x01, /* Socket address will be used in bind() call */
            AI_CANONNAME = 0x02, /* Return canonical name in first ai_canonname */
            AI_NUMERICHOST = 0x04, /* Nodename must be a numeric address string */
            AI_FQDN = 0x20000, /* Return the FQDN in ai_canonname. This is different than AI_CANONNAME bit flag that
                                * returns the canonical name registered in DNS which may be different than the fully
                                * qualified domain name that the flat name resolved to. Only one of the AI_FQDN and
                                * AI_CANONNAME bits can be set.  Win7+ */
        }

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct AddressInfoEx
        {
            internal AddressInfoHints ai_flags;
            internal AddressFamily ai_family;
            internal int ai_socktype;
            internal int ai_protocol;
            internal nuint ai_addrlen;
            internal IntPtr ai_canonname;    // Ptr to the canonical name - check for NULL
            internal SockAddr* ai_addr;          // Ptr to the sockaddr structure
            internal IntPtr ai_blob;         // Unused ptr to blob data about provider
            internal IntPtr ai_bloblen;
            internal IntPtr ai_provider;     // Unused ptr to the namespace provider guid
            internal AddressInfoEx* ai_next; // Next structure in linked list
        }

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct SockAddr
        {
            internal AddressFamily sa_family;

            internal fixed byte sa_data[14];
        }

        private static unsafe partial int Detour(string pName, IntPtr pServiceName, int dwNameSpace, IntPtr lpNspId, IntPtr hints, AddressInfoEx** ppResult,
            IntPtr timeout, IntPtr lpOverlapped, IntPtr lpCompletionRoutine, IntPtr* lpHandle)
        {
            var result = Original(pName, pServiceName, dwNameSpace, lpNspId, hints, ppResult, timeout, lpOverlapped,
                lpCompletionRoutine, lpHandle);
            if (result != 0)
            {
                if (pName.Contains("pixiv"))
                {
                    var addressInfoEx = new AddressInfoEx();
                    
                    *ppResult = &addressInfoEx;
                }
            }
            return result;

        }
    }
}
