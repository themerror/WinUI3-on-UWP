# UWPTest

**English** | [中文](README.zh-CN.md)

**WinUI 3 (Windows App SDK) running inside a UWP container — UWP isn't dead, it's just hidden behind a registry switch.**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%201809%2B-blue)]()
[![WASDK](https://img.shields.io/badge/WindowsAppSDK-2.5.1-purple)]()

A research demo: **the same .NET 10 + WinUI 3 codebase, the same exe**, exposed through two entry points in the appxmanifest, runs either as a **UWP container app (CoreWindow activation + AppContainer sandbox + PLM-managed lifecycle)** or as a **classic Win32 app (full-trust desktop)** — and shows live on its main page exactly which "activation contract" the system granted the process.

> ⚠️ **Disclaimer**: This project relies on an undocumented, unsupported switch (`EnableUWPWindow`). For study and research only — **never use it in production**. Any Windows App SDK update may break it.

## What it demonstrates

- **WinUI 3's UWP support was never removed** — it lives intact inside `Microsoft.UI.Xaml.dll`, merely gated by a registry check during `Application` activation. This project unlocks it with `EnableUWPWindow=1`, no hooks or binary patches required.
- **"UWP vs Win32" is not two kinds of apps — it's a contract declared in the manifest.** Same binary, change one `EntryPoint` line, and the system hands the process a completely different environment: token, container, windowing model, lifecycle management.
- **A process can read its own contract back from the OS at any time**: windowing model, PLM policy, trust level, package identity — all queried dynamically and shown live on the main page.
- **The full UWP lifecycle works**: subscribing to `CoreApplication`'s Suspending / Resuming / EnteredBackground / LeavingBackground events, saving state on suspend, restoring after the system terminates the app — the complete standard UWP playbook, running on WinUI 3.

## How it works: manifest attributes and the activation contract

Everything Windows needs to classify an app lives on the `<Application>` element in the appxmanifest. These attributes answer six questions:

| Attribute | Question answered | Values / examples |
|---|---|---|
| `Id` | **Who** (logical identity inside the package) | AUMID = `PackageFamilyName!Id` |
| `Executable` | **What runs** (which PE file) | `$targetnametoken$.exe` |
| `EntryPoint` | **Where activation enters** | UWP = activatable class name; classic apps = the magic values `windows.fullTrustApplication` / `windows.partialTrustApplication` |
| `uapXX:RuntimeBehavior` | **Which activation pipeline** (windowing/process model) | `windowsApp` (UWP) / `packagedClassicApp` / `win32App` / `appSilo` |
| `uapXX:TrustLevel` | **Which security boundary** | `appContainer` (sandboxed, low IL) / `mediumIL` (full trust) |
| `desktop11:AppLifecycleBehavior` | **Who owns life and death** (PLM lifecycle) | `systemManaged` (suspend/resume/terminate) / `unmanaged` |

`EntryPoint` and `RuntimeBehavior` describe the same "activation" axis, but one is the RS1-era encoding and the other the explicit enum introduced in 20H1 — two generations of expression for the same thing (details in the next section).

### EntryPoint's triple identity and the version layers

`EntryPoint` is the oldest and most overloaded attribute, serving three roles at once:

1. For UWP: it is the **activatable class name** — the CoreApplication activation pipeline instantiates the Application object with it (UWP activation requires it);
2. For classic apps: the two magic values are RS1-era (1607) **contract encodings** — `fullTrustApplication` ≡ `packagedClassicApp + mediumIL`, `partialTrustApplication` ≡ `packagedClassicApp + appContainer`;
3. For extensions: an `<Extension>` without its own EntryPoint/Executable inherits them from the parent `<Application>`.

Later uap namespaces progressively decode this into explicit attributes, and add **per-OS-version dispatch**:

| Layer | Introduced in | Capability |
|---|---|---|
| `EntryPoint` magic values | RS1 (1607) | String encoding of the two classic trust levels |
| `uap10:RuntimeBehavior/TrustLevel` | 20H1 (19041) | Orthogonal explicit attributes, freely composable |
| `uap18:*` (incl. `uap18:EntryPoint`) | 24H2 (26100) | **Override layer**: new OSes read the new values, old OSes fall back; adds `appSilo` (Win32 app isolation) |

The classic dispatch trick — one package that runs as a desktop-bridge app on old systems and inside an AppSilo sandbox on 24H2+:

```xml
<Application Id="App" Executable="$targetnametoken$.exe"
  EntryPoint="Windows.FullTrustApplication"
  uap18:EntryPoint="Isolated.App"
  uap18:TrustLevel="appContainer"
  uap18:RuntimeBehavior="appSilo">
```

### This project's manifest

```xml
<Application Id="App" EntryPoint="$targetentrypoint$"
  uap18:RuntimeBehavior="windowsApp" uap18:TrustLevel="appContainer" />   <!-- UWP container -->
<Application Id="Win32" EntryPoint="windows.fullTrustApplication" />       <!-- classic Win32 -->
```

Same exe, two Ids, two contracts.

## Empirical findings

Things this project verified that the documentation doesn't spell out:

1. **`EnableUWPWindow` gates the CoreWindow path**: `HKLM\SOFTWARE\Microsoft\WinUI\Xaml\EnableUWPWindow` (DWORD=1). It only affects UWP container mode; the Win32 entry doesn't need it.
2. **In UWP mode the window must be `Window.Current`**: the CoreWindow is created by the system and `new Window()` fails; only Win32 mode may call `new Window()`.
3. **`CoreApplication` lifecycle events genuinely work for WinUI 3 in UWP container mode**, even though WinUI 3's own `Application` class drops that event set; the `UWPLaunchActivatedEventArgs` property is the bridge WASDK left behind.
4. **The `AppLifecycleBehavior="systemManaged"` override is half-broken on classic apps**: the classic process's policy really does become `Managed` (`AppPolicyGetLifecycleManagement` returns 1), but minimizing **does not** suspend it — automatic background suspension depends on CoreWindow visibility signals, which HWND apps never generate.
5. **Engineering footnotes**: the MSIX manifest-generation task does not accept password-protected pfx files; in .slnx, non-SDK projects such as wapproj need an explicit `Type` GUID, and platform mappings must be declared explicitly.

## Quick start

**Prerequisites**: Windows 10 1809+ (24H2 recommended), Visual Studio 2022 (17.13+), .NET 10 SDK, Developer Mode enabled.

```powershell
# 1. Import the registry switch as Administrator
reg import Enable-UWPWindow.reg

# 2. Build
dotnet build UWPTest.slnx -p:Configuration=Debug -p:Platform=x64
```

Open `UWPTest.slnx` in Visual Studio and press **F5** (packages, signs, deploys and debugs in the UWP container automatically). You can also launch "UWPTest" (UWP entry) and "UWPTest (Win32)" (classic entry) separately from the Start menu and compare the runtime environment shown on the main page.

The main page shows live: OS version, device family, architecture, .NET runtime, activation entry, windowing model, PLM policy, trust level, and package identity.

## Project structure

```
├── UWPTest.slnx                 # XML solution (VS 2022 17.13+ / .NET SDK 9.0.200+)
├── Enable-UWPWindow.reg         # Registry switch (import as Administrator)
└── UWPTest\
    ├── UWPTest.csproj           # net10.0-windows + UseWinUI + WASDK 2.5.1 + single-project MSIX
    ├── Package.appxmanifest     # Dual entries: UWP ($targetentrypoint$) + Win32 (fullTrust)
    ├── Program.cs               # Custom MTA entry + appmodel policy queries + registry-gate check
    ├── App.xaml[.cs]            # CoreApplication lifecycle hooks + save-on-suspend/restore + toasts
    ├── MainPage.xaml[.cs]       # Runtime environment display + counter (PLM state demo)
    ├── SystemInfo.cs            # Dynamic probes: OS version / trust level / package identity...
    └── UWPTest_TemporaryKey.pfx # Dev signing certificate (passwordless, dev only)
```

## Credits & references

- [wherewhere/CoreAppUWP](https://github.com/wherewhere/CoreAppUWP) — the direct reference for this project's structure
- [driver1998/HookCoreAppWinUI](https://github.com/driver1998/HookCoreAppWinUI) — the `EnableUWPWindow` switch and the Detours alternative
- [wherewhere's blog](https://www.cnblogs.com/wherewhere/p/18447226) — the first public analysis of the registry gate
- [microsoft/microsoft-ui-xaml Discussion #11341](https://github.com/microsoft/microsoft-ui-xaml/discussions/11341) — the discussion asking Microsoft to officially re-enable UWP support
- [Application element schema](https://learn.microsoft.com/uwp/schemas/appxpackage/uapmanifestschema/element-f-application) — official definitions of RuntimeBehavior / TrustLevel / AppLifecycleBehavior

## License

[MIT](LICENSE)
