# **UE FROM SOURCES: MINIMAL & MANUAL**

**[INTRO](#intro)**   
 -- [Terminology](#terminology)  
 -- [Limitations](#limitations)  

 **[I PRE-REQUISITES](#i-pre-requisites)**   
 -- [Tooling](#tooling)  
 -- [Binary and 3rd party UE dependencies](#binary-and-3rd-party-ue-dependencies)  

 **[II BUILDING UE](#ii-building-ue)**   
 -- [Things Unreal Engine does not care about](#things-unreal-engine-does-not-care-about)  
 -- [Building UnrealBuildTool and AutomationTools](#building-unrealbuildtool-and-automationtools)  
 -- [Building UnrealHeaderTool](#building-unrealheadertool)  
 -- [Building Unreal Game target](#building-unreal-game-target)  
 -- [Building Unreal Editor target](#building-unreal-editor-target)  
 -- [Minimal working Editor](#minimal-working-editor)  
   
 **[III ADVANCED BUILD](#iii-advanced-build)**   
 -- [Other platforms and configurations](#other-platforms-and-configurations)  
 -- [Available targets](#available-targets)  
 -- [Skipping Debug symbols and PDB gen)](#skipping-debug-symbols-and-pdb-gen)  
 -- [Skipping plugins](#skipping-plugins)  
 -- [Skipping modules](#skipping-modules)   

  **[IV TROUBLESHOOTING](#iv-troubleshooting)**   

  **[V BONUS](#v-bonus)**   
 -- [Marketplace content with Legendary](#marketplace-content-with-legendary)  



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

## I PRE-REQUISITES

### Tooling
As per [UE repo](https://github.com/EpicGames/UnrealEngine), install the required tools and dependencies, relevant to your  UE version (switch the tag/branch for relevant `readme.md`) This guide assumes you want to build UE 5.x by default but it attempts to cover building older versions as well, like aforementioned 4.23. Because of this, after installing recommended by Epic pre-requisites, it is strongly *advised* that you install or ensure that you already have installed: 

* DotNet 6 or 7 SDK (SDK comes with Runtime)
* .NET Framework 4.8 SDK and .NET Framework 4.8 Targeting Pack  
(for older UE; it brings on-board MSBUILD for 4.x .Net projects)
* NuGet Package Manager (for older UE)

**DO NOT** run `Setup.bat` or `GenerateProjectFiles.bat` yet.  



### Binary and 3rd party UE dependencies

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

## II BUILDING UE

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
Engine\Binaries\DotNET\UnrealBuildTool\UnrealBuildTool.exe UnrealHeaderTool Win64 Development
```

UE4 --  
```
Engine\Binaries\DotNET\UnrealBuildTool.exe UnrealHeaderTool Win64 Development
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

### Minimal working Editor

An example for UE5:

```
dotnet build Engine\Source\Programs\UnrealBuildTool\UnrealBuildTool.sln
dotnet build Engine\Source\Programs\AutomationTool\AutomationTool.csproj
dotnet build Engine\Source\Programs\AutomationTool\AutomationUtils\AutomationUtils.Automation.csproj
dotnet build Engine\Source\Programs\AutomationTool\IOS\IOS.Automation.csproj
dotnet build Engine\Source\Programs\AutomationTool\Scripts\AutomationScripts.Automation.csproj

UnrealBuildTool.exe UnrealHeaderTool Win64 Development
UnrealBuildTool.exe UnrealPAK Win64 Development
UnrealBuildTool.exe ShaderCompileWorker Win64 Development

UnrealBuildTool.exe UnrealEditor Win64 Development
UnrealBuildTool.exe UnrealGame Win64 Development
```

This build should run, work and package projects.

## III ADVANCED BUILD

### Other platforms and configurations

As you may have noticed, `UBT` usage is fairly straightforward, you give it a `target` name, UE-compliant `platform` name and a `configuration` and if it a valid build object/target it will be build within the context of UE pipeline/source setup:

```
UnrealBuildTool.exe CrashReportClient Win32 Shipping
```

If you want to build your project-specific targets (*foreign project*):  

```
UnrealBuildTool.exe -project=full\path\to\my.uproject Target Platform Configuration [-game or -editor]
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

### Skipping Debug symbols and PDB gen
By default, `UBT` has a hardcoded flag to always generate `DebugInfo` as well as PDBs. I assume this was done on purpose by Epic to make their life easier and ensure users always provide some kind of meaningful stack crash trace, sacrificing user choice in the process. Let us fix that.

Open file
`$(EngineSource)\Programs\UnrealBuildTool\Platform\Windows\UEBuildWindows.cs`

Find something that looks like this (code here changes eery version):
```csharp
GlobalLinkEnvironment.bCreateDebugInfo = true;
```
and replace with
```csharp
GlobalLinkEnvironment.bCreateDebugInfo = GlobalCompileEnvironment.bCreateDebugInfo;
```

We've just made `UBT` to respect our configuration inside `BuildConfiguration.xml`. Let us capitalize on that right away.
Open existing or create file `BuildConfiguration.xml` inside:
`%APPDATA\Unreal Engine\UnrealBuildTool\`
and add a section `<BuildConfiguration>`:
```xml
<BuildConfiguration>
	<bDisableDebugInfo>true</bDisableDebugInfo>
	<bDisableDebugInfoForGeneratedCode>true</bDisableDebugInfoForGeneratedCode>
	<bOmitPCDebugInfoInDevelopment>true</bOmitPCDebugInfoInDevelopment>
</BuildConfiguration>
```

[For more information about about build configuration file see its documentation page](https://docs.unrealengine.com/4.26/en-US/ProductionPipelines/BuildTools/UnrealBuildTool/BuildConfiguration/)


Now all that's left is to **rebuild** `UBT` and you are done. PDB and debug info no longer will be generated during build process. This save both on overall build time and disk space.

### Skipping plugins
The best and easiest way to skip building specific plugins is to simply remove/move them somewhere else from `$(EngineSource)\Plugins`. There is technically a way to somehow add them to blacklist/do-not-build list but I never found a working method. I think it used to work once but with time that code broke and got abandoned/forgotten.

Each UE version has its own requirements to the minimal set of plugins required to build and successfully run `Editor/Game` targets. You will have to experiment, because it constantly changes, but in general, here is minimal set for 5.x that works:

```
Plugins/Compression/OodleNetwork                Plugins/Editor/EditorDebugTools
Plugins/Developer/BlankPlugin                   Plugins/Editor/EditorScriptingUtilities
Plugins/Developer/Concert                       Plugins/Editor/GameplayTagsEditor
Plugins/Developer/NullSourceCodeAccess          Plugins/Editor/PluginBrowser
Plugins/Developer/PluginUtils                   Plugins/Editor/WorldPartitionHLODUtilities
Plugins/Developer/PropertyAccessNode            Plugins/EnhancedInput/Binaries
Plugins/Developer/TextureFormatOodle            Plugins/EnhancedInput/Config
Plugins/Developer/VisualStudioSourceCodeAccess  Plugins/EnhancedInput/Content
Plugins/Editor/AssetManagerEditor               Plugins/EnhancedInput/EnhancedInput.uplugin
Plugins/Editor/AssetReferenceRestrictions       Plugins/EnhancedInput/Intermediate
Plugins/Editor/AssetSearch                      Plugins/EnhancedInput/Resources
Plugins/Editor/BlueprintHeaderView              Plugins/EnhancedInput/Source
Plugins/Editor/ConsoleVariablesEditor           Plugins/Messaging/TcpMessaging
Plugins/Editor/ContentBrowser                   Plugins/Runtime/Database
Plugins/Editor/CryptoKeys                       Plugins/Runtime/PropertyAccess
Plugins/Editor/DataValidation
```

**To add a plugin back,** you simply can copy it back into the `$(EngineSource)\Plugins` and rebuild the targets, if you are working with source build. *If you are using a foreign project* and/or `installed build`, copy the engine plugin into **project's** `Plugins/` folder and rebuild your project. In 99% cases this will work just fine, but there can be some engine plugins that are hardcoded to only work properly when build inside the Engine space, like `ReplicationGraph` plugin, so keep that in mind.

**Do note** that example projects like `Lyra` will require a wider variety of plugins and often indirectly, i.e. when one plugins or module requires another and it is not immediately obvious.

### Skipping modules
This is an uncharted territory. Although the process and setup is similar to previous section describing plugins, one thing you need to keep in mind is that modules are intertwined way too deep between each other and UE itself (hello *modul*arity) and for the most part you can't remove anything without consequences cascading down. Still, if you desire, you can experiement by removing module folder and files from `$(EngineSource)\Developer` or `$(EngineSource)\Editor` or `$(EngineSource)\Runtime`. Don't forget to remove them from other module's dependencies as well, via respectitive `*.Build.cs` descriptor.  
You will also most likely need to rebuild `UBT` afterwards, before you can build engine targets.

### Passing extra compiler/linker flags
TODO

### BuildGraph
TODO


## IV TROUBLESHOOTING
TODO

## V BONUS

### Marketplace content with Legendary  

So now that you have source engine built and running, you probably are wondering how can you get your purchased marketplace content added to it? A terrible option would be to install a launcher engine of the same version and then download marketplace assets/content for it via Epic Launcher and copy over, but this is a waste of space and time and simply not our way to do things.  

Instead, we can use a CLI-tool made to interact with for Epic Store, called [Legendary](https://github.com/derrod/legendary). It allows you to login with your credentials and download subscribed content directly. The usage is fairly simple and it has an extensive `--help` output, but here is a quick-start:

* `legendary list --include-ue` will list all the downloadable UE content (along with the rest)

* `legendary install AppNameHere --download-only --base-path D:\UEStuff` will download the subscription/app content

One thing to note - if you want to download some of the content from UE5.x apps, you can't do it on a freshly made account. You actually need to install Epic Launcher first, login with this account, initiate any UE5 version download, then cancel it and uninstall. This will "subscribe" you to UE5 feed and now it will be accessible in `Legendary` as well.