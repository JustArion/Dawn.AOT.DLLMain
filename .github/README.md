> [!WARNING]
> This feature is [officially](https://github.com/dotnet/runtime/pull/109699) unsupported by .NET!
> Doing so; you accept full responsibility for all side-effects that may happen.

That being said, lets get into it!
Here are some resources that may come in handy!
- [DllMain entry point](https://learn.microsoft.com/en-us/windows/win32/dlls/dllmain)
- [Best Practices](https://learn.microsoft.com/en-us/windows/win32/dlls/dynamic-link-library-best-practices)
- [Vanara.PInvoke.Kernel32](https://www.nuget.org/packages/Vanara.PInvoke.Kernel32) is a great package for any Win32 API related stuff!
    - I recommend adding a `global using static Vanara.PInvoke.Kernel32;` to your projects to have things feel just like C++

> [!WARNING]
> DllMain should still be re-linked by some non-C# language. Exporting a C# DllMain will deadlock your program in .NET 10. More info [here](#why-does-my-c-dllmain-deadlock)

---

Basically the package is adding [these](../src/nuget/Dawn.AOT.DLLMain.targets) targets to your .csproj
```xml
<Target Name="RemoveDLLMainObjects" AfterTargets="SetupOSSpecificProps">
    <PropertyGroup>
        <MicrosoftDllMain>$(IlcSdkPath)dllmain$(ObjectSuffix)</MicrosoftDllMain>
        <MicrosoftDllMainGuardCF>$(IlcSdkPath)dllmain.GuardCF$(ObjectSuffix)</MicrosoftDllMainGuardCF>
        <EnableControlFlowGuard>false</EnableControlFlowGuard>
    </PropertyGroup>

    <ItemGroup Condition="'$(_targetOS)' == 'win'">
        <LinkerArg Remove="&quot;$(MicrosoftDllMain)&quot;"/>
        <LinkerArg Remove="&quot;$(MicrosoftDllMainGuardCF)&quot;"/>
        <NativeLibrary Remove="$(MicrosoftDllMain)"/>
        <NativeLibrary Remove="$(MicrosoftDllMainGuardCF)"/>
    </ItemGroup>
</Target>
```

### Why does my C# DllMain deadlock in .NET 10+?

The main thread is [waiting](https://github.com/dotnet/runtime/blob/v10.0.0/src/coreclr/nativeaot/Runtime/FinalizerHelpers.cpp#L106) for the finalizer thread to start.
The runtime is initializing the GC, then [waiting](https://github.com/dotnet/runtime/blob/v10.0.0/src/coreclr/nativeaot/Runtime/GCHelpers.cpp#L95) for the finalizer thread to start, which was not the case in [.NET 6-9](https://github.com/dotnet/runtime/blob/v9.0.0/src/coreclr/nativeaot/Runtime/GCHelpers.cpp#L82)

The `.NET Finalizer` thread will start when the loader lock is lifted. While we're in the loader lock, we're waiting for the `.NET Finalizer` thread to start. 

#### Native Stack Trace of .NET 10 deadlock under loader lock

| Thread ID           | Address          | To               | From             | Size | Party  | Comment                                                      |
|---------------------|------------------|------------------|------------------|------|--------|--------------------------------------------------------------|
| 31208 - Main Thread |                  |                  |                  |      |        |                                                              |                                                           
|                     | 00000000000CF088 | 00007FFD52E1DF83 | 00007FFD55A22714 | 2F0  | System | ntdll.ZwWaitForMultipleObjects+14                            |
|                     | 00000000000CF378 | 00007FFBDFDCC4EA | 00007FFD52E1DF83 | 40   | User   | kernelbase.WaitForMultipleObjectsEx+123                      |
|                     | 00000000000CF3B8 | 00007FFBDFDD3CA6 | 00007FFBDFDCC4EA | 40   | User   | <native_aot_library>.PalCompatibleWaitAny+3A                 |
|                     | 00000000000CF3F8 | 00007FFBDFDC477B | 00007FFBDFDD3CA6 | 30   | User   | <native_aot_library>.CLREventStatic::Wait+C6                 |
|                     | 00000000000CF428 | 00007FFBDFDC021B | 00007FFBDFDC477B | 30   | User   | <native_aot_library>.RhWaitForFinalizerThreadStart+1B        |
|                     | 00000000000CF458 | 00007FFBDFDC715B | 00007FFBDFDC021B | 40   | User   | <native_aot_library>.InitializeGC+DB                         |
|                     | 00000000000CF498 | 00007FFBDFDBF911 | 00007FFBDFDC715B | 50   | User   | <native_aot_library>.RhInitialize+16B                        |
|                     | 00000000000CF4E8 | 00007FFBDFDC2539 | 00007FFBDFDBF911 | 30   | User   | <native_aot_library>.InitializeRuntime+11                    |
|                     | 00000000000CF518 | 00007FFBDFBF30D9 | 00007FFBDFDC2539 | 50   | User   | <native_aot_library>.RhpReversePInvokeAttachOrTrapThread2+69 |
|                     | 00000000000CF568 | 00007FFBDFE2640A | 00007FFBDFBF30D9 | 60   | User   | <native_aot_library>.Managed__DllMain+29                     |
|                     | 00000000000CF5C8 | 00007FFD55A1F86E | 00007FFBDFE2640A | 30   | System | <native_aot_library>.dllmain_dispatch+8A                     |
|                     | 00000000000CF5F8 | 00007FFD558CBCAE | 00007FFD55A1F86E | 70   | System | ntdll.LdrpCallInitRoutineInternal+22                         |
|                     | 00000000000CF668 | 00007FFD558C97AC | 00007FFD558CBCAE | 110  | System | ntdll.LdrpCallInitRoutine+10E                                |
|                     | 00000000000CF778 | 00007FFD559576EA | 00007FFD558C97AC | 40   | System | ntdll.LdrpInitializeNode+19C                                 |
|                     | 00000000000CF7B8 | 00007FFD55956203 | 00007FFD559576EA | 40   | System | ntdll.LdrpInitializeGraphRecurse+6A                          |
|                     | 00000000000CF7F8 | 00007FFD558D6414 | 00007FFD55956203 | 90   | System | ntdll.LdrpPrepareModuleForExecution+EF                       |
|                     | 00000000000CF888 | 00007FFD558D6020 | 00007FFD558D6414 | 1D0  | System | ntdll.LdrpLoadDllInternal+284                                |
|                     | 00000000000CFA58 | 00007FFD558FFA20 | 00007FFD558D6020 | F0   | System | ntdll.LdrpLoadDll+100                                        |
|                     | 00000000000CFB48 | 00007FFD52E1CA5F | 00007FFD558FFA20 | 70   | System | ntdll.LdrLoadDll+170                                         |
|                     | 00000000000CFBB8 | 00007FF6E18B76E9 | 00007FFD52E1CA5F | 8    | User   | kernelbase.LoadLibraryExW+FF                                 |
|                     | 00000000000CFBC0 | 0000000000000000 | 00007FF6E18B76E9 |      | User   | 00007FF6E18B76E9                                             |