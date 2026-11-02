# Dragonfly 44 — Manga Translation Studio

> **Dragonfly 44** is a modern, high-performance desktop application for manga translation, typesetting preparation, and localization. Built on .NET 8, WinUI 3, hardware-accelerated Win2D Direct2D canvas, and Clean Architecture.

---

## 🚀 Quick Start

You can run Dragonfly 44 directly without installing development dependencies:

### Method 1: Desktop Shortcut
- Double-click the **Dragonfly 44** shortcut on your Desktop.

### Method 2: One-Click Launcher
- Double-click [`Launch-Dragonfly44.bat`](file:///c:/Users/HMED%20OPEN/Documents/Dragonfly%2044/Launch-Dragonfly44.bat) in this folder.

### Method 3: Direct Executable
- Run [`App\Dragonfly44.Presentation.exe`](file:///c:/Users/HMED%20OPEN/Documents/Dragonfly%2044/App/Dragonfly44.Presentation.exe).

---

## 📁 Directory Structure

```
Dragonfly 44/
├── App/                      # Self-contained standalone application (executable & runtime)
│   ├── Dragonfly44.Presentation.exe  # Main desktop application executable
│   ├── Assets/               # Application icons and branding assets
│   └── appsettings.json      # Desktop configuration file
│
├── src/                      # Clean Architecture source code
│   ├── Dragonfly44.Domain/          # Core entities, value objects, and contracts
│   ├── Dragonfly44.Application/     # Use cases, interfaces, and translation engine
│   ├── Dragonfly44.Infrastructure/  # EF Core, SQLite, DPAPI security, AI providers
│   └── Dragonfly44.Presentation/    # WinUI 3 desktop UI, Win2D canvas, MVVM viewmodels
│
├── tests/                    # Automated testing suite
│   └── Dragonfly44.Tests/    # 204 unit & integration tests
│
├── Assets/                   # High-resolution logos and icon files
├── Dragonfly44.sln           # Visual Studio solution file
├── Launch-Dragonfly44.bat    # Quick application launcher
├── build-and-run.ps1         # Developer build, test, and run script
└── README.md                 # Project documentation
```

---

## 🛠️ Developer Commands

To build the solution or run tests using PowerShell:

```powershell
# Run the automated test suite (204 tests)
.\build-and-run.ps1 -Test -NoLaunch

# Build and launch in Debug mode
.\build-and-run.ps1

# Publish a fresh Release deployment into App/
.\build-and-run.ps1 -Configuration Release
```

---

## 🌟 Implemented Capabilities

1. **Foundation & Clean Architecture**: SQLite database with WAL mode and EF Core migrations.
2. **Custom UI Architecture**: Custom dark title bar, design tokens, and window management.
3. **Project & Chapter Management**: Ingestion of manga chapters, pages, and metadata.
4. **Hardware-Accelerated Manga Canvas**: High-performance Win2D Direct2D rendering surface with pan, zoom, and continuous vertical strip layout.
5. **Dialogue Feed & Card Editing**: Virtualized feed with TypeR prefix isolation, RTL Arabic text support, and reading order management.
6. **Bidirectional Canvas/Feed Synchronization**: 4-state bubble highlighting, zoom-preserving viewport jumps, and loop prevention.
7. **AI Translation Engine & Provider Architecture**:
   - Provider-independent infrastructure supporting OpenAI and Anthropic Claude.
   - Operating system secure credential storage (Windows DPAPI).
   - Manga-specific Japanese-to-Arabic localization prompt engineering.
   - Token usage and cost tracking ($ USD).
   - **Data Integrity Guarantee**: Manual translations are never silently overwritten; includes an explicit review box with Accept, Discard, and Revert controls.

---

## 📄 License
Internal proprietary translation software. All rights reserved.
