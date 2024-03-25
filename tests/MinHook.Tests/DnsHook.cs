using System.Collections;
using System.Diagnostics;
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
            NativeOverlapped* lpOverlapped,
            IntPtr lpCompletionRoutine,
            IntPtr* lpHandle
        );

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private unsafe delegate void CompletionRoutineDelegate(uint error, uint bytes, NativeOverlapped* overlapped);

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
        [DebuggerDisplay("{DebuggerDisplay(),nq}")]
        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct SockAddr
        {
            internal AddressFamily sa_family;

            public fixed byte sa_data[14];

            private string DebuggerDisplay() => $"{sa_data[0]}.{sa_data[1]}.{sa_data[2]}.{sa_data[3]}.{sa_data[4]}.{sa_data[5]}.{sa_data[6]}.{sa_data[7]}.{sa_data[8]}.{sa_data[9]}.{sa_data[10]}.{sa_data[11]}.{sa_data[12]}.{sa_data[13]}";
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NativeOverlapped
        {
            public IntPtr InternalLow;
            public IntPtr InternalHigh;
            public unsafe AddressInfoEx** Pointer;
            public IntPtr EventHandle;
        }

        private static unsafe partial int Detour(string pName, IntPtr pServiceName, int dwNameSpace, IntPtr lpNspId, IntPtr hints, AddressInfoEx** ppResult,
            IntPtr timeout, NativeOverlapped* lpOverlapped, IntPtr lpCompletionRoutine, IntPtr* lpHandle)
        {
            if (lpOverlapped == null)
            {
                int result = Original(pName, pServiceName, dwNameSpace, lpNspId, hints, ppResult, timeout, lpOverlapped,
                    lpCompletionRoutine, lpHandle);
                if (pName.Contains("pixiv"))
                {
                    (*ppResult)->ai_addr->sa_data[0] = 210;
                    (*ppResult)->ai_addr->sa_data[1] = 140;
                    (*ppResult)->ai_addr->sa_data[2] = 92;
                    (*ppResult)->ai_addr->sa_data[3] = 183;
                    return 0;
                }
                return result;
            }
            else
            {
                int result = Original(pName, pServiceName, dwNameSpace, lpNspId, hints, ppResult, timeout, lpOverlapped,
                    Marshal.GetFunctionPointerForDelegate<CompletionRoutineDelegate>(OnComplete), lpHandle);
                return result;
            }

            void OnComplete(uint error, uint bytes, NativeOverlapped* overlapped)
            {
                var complete = Marshal.GetDelegateForFunctionPointer<CompletionRoutineDelegate>(lpCompletionRoutine);
                if (pName.Contains("pixiv"))
                {
                    (*ppResult)->ai_addr->sa_data[0] = 210;
                    (*ppResult)->ai_addr->sa_data[1] = 140;
                    (*ppResult)->ai_addr->sa_data[2] = 92;
                    (*ppResult)->ai_addr->sa_data[3] = 183;
                    complete(0, 0, overlapped);
                    return;
                }
                complete(error, bytes, overlapped);
            }
        }
    }
}
