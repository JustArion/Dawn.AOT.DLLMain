> [!WARNING]
> This feature is [officially](https://github.com/dotnet/runtime/pull/109699) unsupported by .NET!<br/>
> Doing so; you accept full responsibility for all side-effects that may happen.

That being said, lets get into it!<br/>
Here are some resources that may come in handy!
- [DllMain entry point](https://learn.microsoft.com/en-us/windows/win32/dlls/dllmain)
- [Best Practices](https://learn.microsoft.com/en-us/windows/win32/dlls/dynamic-link-library-best-practices)
- [Vanara.PInvoke.Kernel32](https://www.nuget.org/packages/Vanara.PInvoke.Kernel32) is a great package for any Win32 API related stuff!
    - I recommend adding a `global using static Vanara.PInvoke.Kernel32;` to your projects to have things feel just like C++

---

Basically the package is adding [these](../src/nuget/Dawn.AOT.DLLMain.targets) targets to your .csproj
```xml
    <Target Name="RemoveDLLMainObject" AfterTargets="SetupOSSpecificProps" >
        <PropertyGroup>
            <MicrosoftDLLMain>$(IlcSdkPath)dllmain$(ObjectSuffix)</MicrosoftDLLMain>
        </PropertyGroup>

        <ItemGroup Condition="'$(_targetOS)' == 'win'">
            <LinkerArg Remove="&quot;$(MicrosoftDLLMain)&quot;"/>
        </ItemGroup>

    </Target>
    ```