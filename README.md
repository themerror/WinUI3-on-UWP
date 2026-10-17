# UWPTest — UWP 容器中的 WinUI 3 (Windows App SDK)

本工程演示在 **UWP 应用容器**（CoreWindow 激活模型、AppContainer 沙箱）中运行 **WinUI 3 / Windows App SDK**，
工程结构对齐 [wherewhere/CoreAppUWP](https://github.com/wherewhere/CoreAppUWP)（单项目 MSIX）。

原理：WinUI 3 原生保留了 UWP 支持，仅在 `Application` 激活时被注册表开关
`HKLM\SOFTWARE\Microsoft\WinUI\Xaml\EnableUWPWindow` (DWORD) 门控；置 1 后即可运行。

> **警告**：这是未公开、不受微软支持的路径，任何 WASDK 更新都可能破坏它。仅供研究，严禁用于生产环境。
> 参考：microsoft/microsoft-ui-xaml Discussion #11341、driver1998/HookCoreAppWinUI、wherewhere/CoreAppUWP。

## 结构

```
UWPTest\
├── UWPTest.slnx                  # XML 解决方案文件（VS 2022 17.13+ / .NET SDK 9.0.200+）
├── Enable-UWPWindow.reg          # 注册表开关（需管理员导入）
└── UWPTest\                      # 应用工程（单项目 MSIX，无需 wapproj）
    ├── UWPTest.csproj            # net10.0-windows + UseWinUI + WASDK 2.5.1 + EnableMsixTooling
    ├── Package.appxmanifest      # 双入口：UWP ($targetentrypoint$) + Win32 (PartialTrust)
    ├── Program.cs                # 自定义入口（MTA）+ EnableUWPWindow 开关检查
    ├── App.xaml / App.xaml.cs    # OnLaunched：UWP 模式用 Window.Current，Win32 模式才 new Window()
    ├── MainPage.xaml / .cs       # 示例界面
    ├── Assets\                   # 磁贴/启动画面 PNG
    ├── Properties\PublishProfiles\win10-{x86,x64,arm64}.pubxml
    └── UWPTest_TemporaryKey.pfx  # 开发签名证书（CN=UWPTestDev，无密码，勿用于生产）
```

## 关键工程配置（容易踩的坑）

| 配置 | 作用 |
|---|---|
| `EnableMsixTooling=true` | 单项目 MSIX，清单/打包都在应用工程内，**不要**用 wapproj |
| `ApplicationEntryPoint=UWPTest.App` | 解析清单里的 `EntryPoint="$targetentrypoint$"` |
| `DISABLE_XAML_GENERATED_MAIN` | 自动生成的入口是 STAThread，UWP 容器要求 MTA |
| `Window.Current` | UWP（CoreWindow）模式下窗口由系统创建，`new Window()` 会直接失败；Win32 模式才允许 new |
| 双 `Application` 入口 | `App`（UWP 容器）+ `Win32`（`Windows.PartialTrustApplication`），清单**不含** `runFullTrust` |
| `WindowsAppSdkDeploymentManagerInitialize=false` | 关闭自动 Bootstrap；框架包依赖由 MSIX 工具链注入清单 |
| `GenerateTemporaryStoreCertificate` + pfx | 开发签名自动化，VS F5 与命令行打包均可用。注意：pfx 必须无密码——MSIX 清单生成任务不支持带密码的证书文件 |

## 构建与运行

1. 以管理员身份导入注册表开关：
   ```powershell
   reg import Enable-UWPWindow.reg
   ```
2. 编译：
   ```powershell
   dotnet build UWPTest\UWPTest.csproj -p:Platform=x64
   ```
3. 部署运行（任选其一）：
   - **VS 2022**：打开 `UWPTest.slnx`，直接 F5（自动打包、签名、部署到本机并调试）；
   - **命令行**：`msbuild UWPTest.slnx /p:Configuration=Debug /p:Platform=x64 /p:AppxPackageDir=AppPackages\` 生成 appx 后安装。
4. 安装前需信任开发证书（一次性，管理员）：
   ```powershell
   Import-Certificate -FilePath <导出的cer> -CertStoreLocation Cert:\LocalMachine\TrustedPeople
   ```

## 备注

- 不想改全局注册表，可用 Detours 在进程内拦截 `RegQueryValueExW` 对 `EnableUWPWindow` 的读取（参见 driver1998/HookCoreAppWinUI 与 wherewhere 博客中的 `HookRegistry` 实现）。
- 该路径在 Xbox Dev Mode 上同样可用（UWP 容器 + WinUI 3），未验证。
