# **UE FROM SOURCES: MINIMAL & MANUAL**

## INTRO
The purpose of this guide is to give you a quick, high-level overview of how to build Unreal Engine from sources *somewhat manually* and with some hacks&tricks that can *potentially* speedup the process and, *potentially*, save on disk space (no IncrediBuild/XGE and all that enterprise jazz). You may find it useful if you iterate on engine sources a lot or just want some explicit building process for troubleshooting purposes.

### Terminology

`$(EngineRoot)` - root directory of the engine sources, the one you fetch from UE's git repo.  
`$(EngineSource)` - the `$(EngineRoot)\Engine\Source\` directory  
`AT`  - AutomationTool(s), Epic's C#/.NET UE pipeline app  
`UBT` - UnrealBuildTool, Epic's C#/.NET UE pipeline app  
`UHT` - UnrealHeaderTool, C++ pipeline app  


### Limitations

* It is assumed, by default, that you are building UE on Windows *for* Windows. Linux and MacOS -- both as a host and as a target -- are out of the scope of this how-to, simply because I don't have a separate test machine (VM is non-trivial task due to CPU/RAM/Storage requirements). That being said, everything here should be applicable to both Linux and MacOS.  
* This guide is only for building bare-bones Editor/Game targets. Building whole toolset for the purposes of fully-functional, multi-platform project-packaging and testing pipeline from the Editor is outside of the scope as well.

&nbsp;  

## I. PRE-REQUISITES

### Tooling
As per [UE repo](https://github.com/EpicGames/UnrealEngine), install the required tools and dependencies, relevant to your  UE version (switch the tag/branch for relevant `readme.md`) This guide assumes you want to build UE 5.x by default but it attempts to cover building older versions as well, like aforementioned 4.23. Because of this, after installing recommended by Epic pre-requisites, it is strongly *advised* that you install or ensure that you already have installed: 

* DotNet 6 or 7 SDK (SDK comes with Runtime)
* .NET Framework 4.8 SDK and .NET Framework 4.8 Targeting Pack  
(for older UE; it brings on-board MSBUILD for 4.x .Net projects)
* NuGet Package Manager (for older UE)

**DO NOT** run `Setup.bat` or `GenerateProjectFiles.bat` yet.  



### Binary and 3rd-party UE dependencies

```
Do you want to save on some disk space?
[Yes/No]_
```
If the answer is `No`, then run the `Setup.bat` and go to part **II**.

If the answer is `Yes`, then you must know that this part is for **advanced users**, in one way or another it will|might require you to tinker with the setup until desired working state is achieved.  
When you run `Setup.bat`, under the hood it just runs `GitDependencies.exe`, which is basically a homegrown binary cvs tool that fetches appropriate dependency content from Epic's CDN according to your current UE version. By default, this executable binary is in:  
UE5: `Engine\Binaries\DotNET\GitDependencies\win-x64\GitDependencies.exe`  
UE4: `Engine\Binaries\DotNET\GitDependencies.exe`  

There are **2 ways** to cut down on the downloaded binary content:

 * command-line arguments to `GitDependencies.exe` to exclude namespaces
 * via `.gitdepsignore`

 **Command-line arguments** allows you to submit some namespaces that will be excluded from the downloaded dependencies, provided there is a match. I don't know yet how to get list of ALL valid namespaces for given UE version, the code for this in decompiled `GitDependencies.exe` is very generic, it just adds the submitted namespaces as a string match to the collection to check against. Here are some examples that should give you a hint:

 `GitDependencies.exe -exclude=WinRT -exclude=HoloLens -exclude=Lumin -exclude=Linux -exclude=Linux32 -exclude=osx64 -exclude=IOS -exclude=Android -exclude=Mac -exclude=HTML5 -exclude=Win32 -exclude=TVOS`

 You can submit as many as you want, invalid ones will be a noop.

 The second method, via **.gitdepsignore** is a bit more useful, however, by definition, in order to even understand what do you want to (and can) skip, you need to download *ALL* the default content (`GitDependencies.exe` with no args) beforehand and analyze it against previous state of the repo/engine dir. This repo includes my own version that is aimed at `Win64` target platform only and tries to skip as much unnecessary stuff as possible. More info on `.gitdepsignore` you can find [**HERE**](https://github.com/EpicGames/UnrealEngine/pull/1687)

It is important to note here, that UE deps are messy. *Very messy*. You think that it makes perfect sense to skip Win32 and IOS stuff if you target Win64? Why yes, it does, but not for UE. If you completely skip these dependencies when building UE 4.x, a lot of things will fail, like AutomationTools (hard dependencies on some of the IOS stuff), or BuildGraph's default build config (some odd hard-dependencies on some Win32 stuff in copy-files). My recommendation is: do not use `-exclude=IOS` and `-exclude=Android` and `-exclude=HTML5` for UE 4.x, they tend to break stuff. My `.gitdepsignore` already skips non-critical content from these.

Oh and yes, you **can combine** both methods mentioned above.

## II. BUILDING UE

If you've skipped last step from previous chapter - run `Setup.bat` now.  

### Things Unreal Engine does not care about

Generated project files a-la MSVS's `UE5.sln`. At all.  
A lot of people having hard time to grasp it but project gen for "IDE X" simply exist for user's convenience sake (i.e. if you plan to work with sources and want a familiar IDE setup). UE building pipeline does not use nor your `.sln`, nor any other built-in project files stuff from any other IDE (well, technically there is an asterisk, but we will skip it). Everything is being built via two C# tools: `AutomationsTools` and `UnrealBuiltTool`. Think of them like homegrown `cmake` alternatives. They are used for Engine source building as well as project packaging, platfrom-specific/agnostic pipeline tools.

### Building UnrealBuildTool and AutomationTools

This is why we needed dotnet core SDK from earlier - to build C#/.NET tooling. UE5 comes with bundled 6.0 sdk but we will skip it for now.  
Open command line, either `cmd.exe` or your wrapper over it (Windows Terminal, whatever). Navigate to engine root:
```
cd $(EngineRoot)`
```

Build UBT:
```
dotnet build Engine\Source\Programs\UnrealBuildTool\UnrealBuildTool.csproj -c Development
```

Build AutomationTools, (if needed):  
UE5 --  
```
dotnet build Engine\Source\Programs\AutomationTool\AutomationTool.csproj -c Development  
```
UE4 --  
```
dotnet build Engine\Source\Programs\DotNETCommon\DotNETUtilities\DotNETUtilities.csproj
dotnet build Engine\Source\Programs\AutomationTool\AutomationTool.csproj -c Development
```

If UBT or AT fails to build, see **[Troubleshooting]** section.

### Building UnrealHeaderTool

Once you have `UBT` built, it is time to test if our dev environment set up correctly and if we can compile and link UE modules and programs. From `$(EngineRoot)` directory:  
UE5 --  
```
cd Engine\Binaries\DotNET\UnrealBuildTool\
UnrealBuildTool.exe UnrealHeaderTool Win64 Development
```

UE4 --  
```
cd Engine\Binaries\DotNET\UnrealBuildTool.exe
UnrealBuildTool.exe UnrealHeaderTool Win64 Development
```

If `UHT` build succeeds, congratulations, your dev environment should be good. If it fails, revise the **I. [Tooling]** or check **[Troubleshooting]** sections.

### Building Unreal Game target  

This is what I call "UE player" target, it what loads and processes your content runs your game/it's logic. The `Engine`.

UE5 --  
```
UnrealBuildTool.exe UnrealGame Win64 Development
```

UE4 --  
```
UnrealBuildTool.exe UE4Game Win64 Development
```

Once it is built, you technically can run your Blueprint-only project as a standalone:
```
UnrealGame.exe full\path\to\mygame.uproject
```

### Building Unreal Editor target  

The Editor itself.

UE5 --  
```
UnrealBuildTool.exe UnrealEditor Win64 Development
```

UE4 --  
```
UnrealBuildTool.exe UE4Editor Win64 Development
```

Although the built Editor is technically fully functional, it depends, at minimum, on ShaderCompilerWorker program to run.

```
UnrealBuildTool.exe ShaderCompileWorker Win64 Development
```

Now you can run the Editor.  
That is pretty much it for the basic UE build.

## III. ADVANCED BUILD

### Other platforms and configurations

As you may have noticed, `UBT` usage is fairly straightforward, you give it a `target` name, UE-compliant `platform` name and a `configuration` and if it a valid build object/target it will be build within the context of UE pipeline/source setup:

```
UnrealBuildTool.exe CrashReportClient Win32 Shipping
```

If you want to build your project-specific targets (*foreign project*):  

```
UnrealBuildTool.exe -project=full\path\to\my.uproject Target Platform Configuration
```

### Available targets

`UBT` will find all valid targets in `$(EngineSource)` and in your project's `Source/` directory and will try to build what you have requested. Said targets are described via `*.Target.cs` files, which contain raw C# code (`UBT` compiles them dynamically at runtime). You can simply search for `*.Target.cs` or, alternatively, you can generate project files:  

**Engine** --  
```
UnrealBuildTool.exe -projectfiles -currentplatform
```
**Your project** --  
```
UnrealBuildTool.exe -projectfiles -project=path:\to\my\game.uproject -currentplatform
```

and then just go to `\Intermediate\ProjectFiles\` directory and open any `*.vcxproj` file to find out what it tries to build and how. For example, inside `UE5.vcxproj` we can find:  
```xml
  <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|x64'">
    <!-- snip -->
    <NMakeBuildCommandLine>..\..\Build\BatchFiles\Build.bat UnrealGame Win64 Debug -WaitMutex -FromMsBuild</NMakeBuildCommandLine>
    <!-- snip -->
    <NMakeOutput>..\..\Binaries\Win64\UnrealGame-Win64-Debug.exe</NMakeOutput>
    <AdditionalOptions>/std:c++17</AdditionalOptions>
  </PropertyGroup>
```
which should be self-evident. Note that this applies to project generated for MSVS format, which is the default one for Windows. If you chose a different project format, like `VisualStudioCode` or `XCode` then your aggregated targets will in `.vscode\tasks.json` and so on.

As you may now have fully realized - generated project files is just a convenient way to do batch compilation of what we did manually, using default "UE preset".

### Skipping Debug symbols/PDB gen (speedup)
TODO

### Skipping plugins (speedup)
TODO

### Skipping modules (speedup)
TODO

### Passing extra compiler/linker flags
TODO

### BuildGraph
TODO


## IV. TROUBLESHOOTING
TODO