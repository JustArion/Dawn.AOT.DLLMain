using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TestTarget;

internal static class Entrypoint
{
    // https://learn.microsoft.com/en-us/windows/win32/dlls/dllmain
    [UnmanagedCallersOnly(EntryPoint = nameof(DllMain), CallConvs = [typeof(CallConvStdcall)])]
    internal static bool DllMain(nint hinstDLL, int fdwReason, nint lpvReserved)
    {
        throw new UnreachableException(nameof(DllMain));
    }
}