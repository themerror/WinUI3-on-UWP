# UWPTest

[English](README.md) | **中文**

**WinUI 3 (Windows App SDK) 跑在 UWP 容器里——UWP 没有死，只是被藏进了一个注册表开关后面。**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%201809%2B-blue)]()
[![WASDK](https://img.shields.io/badge/WindowsAppSDK-2.5.1-purple)]()

一个研究性演示项目：**同一份 .NET 10 + WinUI 3 代码、同一个 exe**，通过 appxmanifest 里的两个入口，分别以 **UWP 容器（CoreWindow 激活 + AppContainer 沙箱 + PLM 托管）** 和 **经典 Win32（完全信任桌面应用）** 两种身份运行，并在界面上实时展示进程被系统赋予了哪份"激活契约"。

> ⚠️ **免责声明**：本项目使用未公开、不受微软支持的开关（`EnableUWPWindow`）。仅供学习研究，**严禁用于生产环境**。任何 Windows App SDK 更新都可能使其失效。

## 它演示了什么

- **WinUI 3 的 UWP 支持从未被移除**——它完整存在于 `Microsoft.UI.Xaml.dll` 里，只是在 `Application` 激活时被一个注册表检查门控。本项目用 `EnableUWPWindow=1` 解锁它，无需任何 hook 或二进制补丁。
- **"UWP vs Win32" 不是两种应用，而是一份清单声明的契约**。同一进程二进制，换一行 `EntryPoint`，系统就给它换一整套运行环境：token、容器、窗口模型、生命周期管理。
- **进程可以随时向 OS 读回自己的契约**：窗口模型、PLM 策略、信任等级、包身份——全部动态查询，主页实时显示。
- **完整的 UWP 生命周期**：`CoreApplication` 的 Suspending / Resuming / EnteredBackground / LeavingBackground 事件订阅，挂起时保存状态、被系统终止后重启时还原——标准 UWP 应用的全套动作，在 WinUI 3 上照样成立。

## 原理：清单属性与激活契约

Windows 区分应用类型的全部信息都在 appxmanifest 的 `<Application>` 元素上。这些属性回答六个问题：

| 属性 | 回答的问题 | 取值/示例 |
|---|---|---|
| `Id` | **是谁**（包内逻辑标识） | AUMID = `PackageFamilyName!Id` |
| `Executable` | **跑什么**（哪个 PE 文件） | `$targetnametoken$.exe` |
| `EntryPoint` | **从哪进入**（激活入口点） | UWP = 可激活类名；经典应用 = `windows.fullTrustApplication` / `windows.partialTrustApplication` 魔法值 |
| `uapXX:RuntimeBehavior` | **走哪条激活管线**（窗口模型/进程模型） | `windowsApp`（UWP）/ `packagedClassicApp` / `win32App` / `appSilo` |
| `uapXX:TrustLevel` | **关在哪**（安全边界） | `appContainer`（沙箱低 IL）/ `mediumIL`（完全信任） |
| `desktop11:AppLifecycleBehavior` | **谁管生死**（PLM 生命周期） | `systemManaged`（挂起/恢复/终止）/ `unmanaged` |

`EntryPoint` 与 `RuntimeBehavior` 描述的是同一根"激活"轴，但一个是 RS1 时代的编码、一个是 20H1 起的显式枚举，二者是新旧两套表达方式（详见下一节）。

### EntryPoint 的三重身份与版本分层

`EntryPoint` 是历史最久也最复杂的属性，一身三职：

1. 对 UWP：它是**可激活类的类名**，CoreApplication 激活管线用它实例化 Application 对象（UWP 激活必须提供）；
2. 对经典应用：两个魔法值是 RS1（1607）时代的**契约编码**——`fullTrustApplication` ≡ `packagedClassicApp + mediumIL`，`partialTrustApplication` ≡ `packagedClassicApp + appContainer`；
3. 对扩展：`<Extension>` 缺省时从父 `<Application>` 继承。

后续的 uap 命名空间把这个编码逐步解码为显式属性，并支持**按 OS 版本分流**：

| 层 | 引入版本 | 能力 |
|---|---|---|
| `EntryPoint` 魔法值 | RS1 (1607) | 两种经典信任等级的字符串编码 |
| `uap10:RuntimeBehavior/TrustLevel` | 20H1 (19041) | 正交显式属性，可自由组合 |
| `uap18:*`（含 `uap18:EntryPoint`） | 24H2 (26100) | **覆盖层**：新系统读新值、老系统回落旧值；新增 `appSilo`（Win32 应用隔离） |

经典的分流技巧——同一个包在老系统跑桌面桥、在 24H2+ 跑 AppSilo 沙箱：

```xml
<Application Id="App" Executable="$targetnametoken$.exe"
  EntryPoint="Windows.FullTrustApplication"
  uap18:EntryPoint="Isolated.App"
  uap18:TrustLevel="appContainer"
  uap18:RuntimeBehavior="appSilo">
```

### 本项目的清单

```xml
<Application Id="App" EntryPoint="$targetentrypoint$"
  uap18:RuntimeBehavior="windowsApp" uap18:TrustLevel="appContainer" />   <!-- UWP 容器 -->
<Application Id="Win32" EntryPoint="windows.fullTrustApplication" />       <!-- 经典 Win32 -->
```

同一个 exe，两个 Id，两份契约。

## 实测发现

这个项目在构建过程中验证了几件文档没写清楚的事：

1. **`EnableUWPWindow` 门控的是 CoreWindow 路径**：`HKLM\SOFTWARE\Microsoft\WinUI\Xaml\EnableUWPWindow` (DWORD=1)。只影响 UWP 容器模式；Win32 入口不需要它。
2. **UWP 模式下窗口必须用 `Window.Current`**：CoreWindow 由系统创建，`new Window()` 会失败；Win32 模式才允许 `new Window()`。
3. **`CoreApplication` 生命周期事件在 UWP 容器模式下对 WinUI 3 真实可用**，尽管 WinUI 3 自己的 `Application` 类裁掉了这套事件；`UWPLaunchActivatedEventArgs` 属性是 WASDK 留下的桥。
4. **`AppLifecycleBehavior="systemManaged"` 覆写对经典应用半残**：实测 classic 进程的策略确实变为 `Managed`（`AppPolicyGetLifecycleManagement` 返回 1），但最小化**不会**触发挂起——自动挂起的后台检测依赖 CoreWindow 可见性信号，HWND 应用进不了这条链。
5. **工程细节**：MSIX 清单生成任务不接受带密码的 pfx；slnx 中 wapproj 等非 SDK 项目必须显式写 `Type` GUID，平台映射必须显式声明。

## 快速开始

**前置条件**：Windows 10 1809+（建议 24H2）、Visual Studio 2022（17.13+）、.NET 10 SDK、启用开发者模式。

```powershell
# 1. 以管理员身份导入注册表开关
reg import Enable-UWPWindow.reg

# 2. 编译
dotnet build UWPTest.slnx -p:Configuration=Debug -p:Platform=x64
```

用 Visual Studio 打开 `UWPTest.slnx`，直接 **F5**（自动打包、签名、以 UWP 容器部署调试）。也可以从开始菜单分别启动 "UWPTest"（UWP 入口）和 "UWPTest (Win32)"（经典入口），对照主页显示的运行环境差异。

主页实时显示：系统版本、设备族、架构、.NET 运行时、激活入口、窗口模型、PLM 策略、信任等级、包身份。

## 项目结构

```
├── UWPTest.slnx                 # XML 解决方案（VS 2022 17.13+ / .NET SDK 9.0.200+）
├── Enable-UWPWindow.reg         # 注册表开关（需管理员导入）
└── UWPTest\
    ├── UWPTest.csproj           # net10.0-windows + UseWinUI + WASDK 2.5.1 + 单项目 MSIX
    ├── Package.appxmanifest     # 双入口：UWP($targetentrypoint$) + Win32(fullTrust)
    ├── Program.cs               # 自定义 MTA 入口 + appmodel 策略查询 + 注册表开关检查
    ├── App.xaml[.cs]            # CoreApplication 生命周期订阅 + 挂起保存/终止还原 + toast 通知
    ├── MainPage.xaml[.cs]       # 运行环境信息展示 + 计数器（PLM 状态演示）
    ├── SystemInfo.cs            # 动态采集：系统版本/信任等级/包身份等
    └── UWPTest_TemporaryKey.pfx # 开发签名证书（无密码，仅限开发）
```

## 致谢与参考

- [wherewhere/CoreAppUWP](https://github.com/wherewhere/CoreAppUWP) —— 本项目工程结构的直接参考
- [driver1998/HookCoreAppWinUI](https://github.com/driver1998/HookCoreAppWinUI) —— `EnableUWPWindow` 开关与 Detours 替代方案
- [wherewhere 的博客](https://www.cnblogs.com/wherewhere/p/18447226) —— 注册表门控机制的首次公开分析
- [microsoft/microsoft-ui-xaml Discussion #11341](https://github.com/microsoft/microsoft-ui-xaml/discussions/11341) —— 请求官方转正 UWP 支持的讨论
- [Application 元素 schema](https://learn.microsoft.com/uwp/schemas/appxpackage/uapmanifestschema/element-f-application) —— RuntimeBehavior/TrustLevel/AppLifecycleBehavior 官方定义

## License

[MIT](LICENSE)
