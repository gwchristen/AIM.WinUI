# AIM (WinUI 3) Portable Starter — GitHub‑ready

- **Portable** (no installer). Output is **xcopy‑deployable**; settings.json is saved **next to the EXE**.
- WinUI 3 + **Windows App SDK** configured for **unpackaged + self‑contained** deployment.
- **Choose Root** (FolderPicker), **TreeView** on the left, **Tabs** on the right.
- **FileSystemWatcher** watches **.csv, .log, .txt**; **Indexer** indexes **.csv + .log** contents; Preview supports **.txt/.csv/.log** (5 MB cap).
- **Archive/Shipped** moves go to protected directories **outside** the Root; **Backup** creates a ZIP of Root only.

## Build
Open AIM.WinUI.sln in **Visual Studio 2022** (Windows App SDK workload) and run.

## Publish (portable)
The project is **unpackaged** (WindowsPackageType=None) and **self‑contained** (WindowsAppSDKSelfContained=true, SelfContained=true), enabling **xcopy** deployment (no runtime installer).  
See Microsoft’s Windows App SDK docs: **self‑contained deployment**.  
Refs: Windows App SDK self‑contained deployment; overview.  
 
## Pickers in WinUI 3 desktop (important)
For File/Folder pickers in desktop WinUI 3, initialize the picker with your window using WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd) before calling Pick*Async(). This starter uses that pattern.

## References
- Windows App SDK self‑contained deployment (xcopy): https://learn.microsoft.com/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps  
- Windows App SDK deployment overview: https://learn.microsoft.com/windows/apps/package-and-deploy/deploy-overview  
- File/Folder pickers with InitializeWithWindow (WinUI 3 desktop): https://github.com/microsoft/WindowsAppSDK/issues/1188  
- Tree view design guidance (WinUI): https://learn.microsoft.com/windows/apps/design/controls/tree-view  
- FileSystemWatcher API: https://learn.microsoft.com/dotnet/api/system.io.filesystemwatcher  
- FileSystemWatcher.Filters (multiple patterns): https://learn.microsoft.com/dotnet/api/system.io.filesystemwatcher.filters  