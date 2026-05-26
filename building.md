# Building ViVeTool GUI (fork)

### Build Requirements
- Visual Studio 2022
- Windows 10 Version 2004 / Windows 11

### NuGet packages
Open the solution in VS 2022. Visual Studio will automatically restore NuGet packages (Newtonsoft.Json 13.0.4) on first build. No manual steps needed.

### Albacore.ViVe
The pre-built `lib\Albacore.ViVe.dll` is included in the repository. No action required unless you want to rebuild it from [thebookisclosed/ViVe](https://github.com/thebookisclosed/ViVe).

### Telerik UI for WinForms
The required Telerik binaries (version 2021.3.1109.40) are included in `lib\RCWF\2021.3.1109.40`. The solution references them directly - no Telerik installation is required to build.

You will not be able to use the Visual Studio designer without the full Telerik UI for WinForms Suite installed, but code changes and builds work fine with the bundled DLLs.

### Known limitations
- The build dropdown in the main window may show only older Windows builds, or no builds at all. The upstream mach2 feature list repository was archived in December 2024 and its directory structure changed; the current parser may not find any usable `.txt` files.
- Use **F12** to enter feature IDs manually, or **"Load manually..."** to import a local features file.
