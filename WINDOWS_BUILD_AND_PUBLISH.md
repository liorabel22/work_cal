# Build and Publish on Windows 11

This source package contains everything needed to restore, build, and publish MAUI DB Manager on a Windows 11 x64 computer. Generated Mac build directories, IDE metadata, and temporary files are intentionally excluded.

## 1. Install the development tools

Install Visual Studio with the **.NET Multi-platform App UI development** workload. Ensure that the .NET 10 SDK is installed.

Open PowerShell in this folder and verify the SDK:

```powershell
dotnet --version
```

The project contains `global.json`, which requests .NET SDK 10 or a compatible newer SDK.

## 2. Build and test from source

Restore the required MAUI workload and packages:

```powershell
dotnet workload restore .\MauiDbManager\MauiDbManager.csproj
dotnet restore .\MauiDbManager\MauiDbManager.csproj
```

Build the Windows target:

```powershell
dotnet build .\MauiDbManager\MauiDbManager.csproj `
  -f net10.0-windows10.0.19041.0 `
  -c Release
```

Run from source:

```powershell
dotnet run --project .\MauiDbManager\MauiDbManager.csproj `
  -f net10.0-windows10.0.19041.0
```

## 3. Publish the distributable application

The included script publishes a self-contained, unpackaged Windows x64 application and then creates a ZIP containing the complete runnable output:

```powershell
.\Publish-Windows.ps1
```

If PowerShell blocks local scripts, run this command for the current PowerShell process only, then retry:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\Publish-Windows.ps1
```

Successful publication creates:

```text
artifacts\windows-x64\
artifacts\MauiDbManager-windows-x64.zip
```

Give `MauiDbManager-windows-x64.zip` to the application user. They should extract the entire archive and run `MauiDbManager.exe`. They do not need this source package, Visual Studio, or the .NET SDK.

Do not distribute only the `.exe`; keep all files from the published folder together.

## 4. SQL Server network requirements

The recipient's computer must be able to reach the SQL Server host. For the current environment, use:

```text
Server name: 192.168.0.56,1433
Connection method: SQL Server Authentication
User name: sa (or another SQL login with the necessary permissions)
```

The Windows computer must be on the same network or connected through an appropriate VPN. Port `1433` must be exposed by Podman and allowed through the server firewall.

## 5. Windows security warning

This direct ZIP distribution is not digitally signed. Windows SmartScreen may warn the recipient because the publisher is unknown. For polished or broad distribution, create and sign an MSIX installer with a certificate trusted by the recipients.

## 6. Important directories

- `MauiDbManager\` — application project and all source code
- `MauiDbManager\Platforms\Windows\` — Windows-specific startup and manifest files
- `MauiDbManager\Resources\` — styles, colors, icon, and splash image
- `Publish-Windows.ps1` — automated self-contained publication
- `MAUI_DB_MANAGER_COMPLETE_REPORT.md` — detailed code and SQL learning guide
- `ARCHITECTURE.md` — compact file map

Directories such as `bin`, `obj`, `.idea`, `.vs`, and `artifacts` are generated and should not be copied back when transferring source changes.
