# Plan: Phase 1 — Foundation and Architecture

**Date**: 2026-11-01  
**Plan Title**: Dragonfly 44 Phase 1 Foundation and Architecture  
**Document Identifier**: `2026-11-01_plan_phase-1-foundation-and-architecture.md`

## 1. Objective
Establish the solid technical foundation and Clean Architecture for Dragonfly 44 (native Windows desktop manga scanlation and Arabic translation workbench) using C#, WinUI 3, Windows App SDK, and SQLite.
No premature UI/UX, canvas rendering, dialogue feed, or AI scanning providers will be implemented in this phase.

## 2. Proposed Implementation Steps

### Step 1: Tooling & Prerequisite Setup
- Verify and install the modern `.NET 8 SDK` via `winget install Microsoft.DotNet.SDK.8` to enable the `dotnet` CLI toolchain.
- Verify `dotnet --version` and `dotnet --list-sdks`.

### Step 2: Solution & Project Structure Creation
- Create solution `Dragonfly44.sln` with Clean Architecture separation:
  - `src/Dragonfly44.Domain`: Zero-dependency domain layer containing core entities, value objects, and domain contracts.
  - `src/Dragonfly44.Application`: Use cases, service contracts, engine registries, and DTOs.
  - `src/Dragonfly44.Infrastructure`: SQLite persistence with EF Core, centralized logging, configuration, and global exception handling.
  - `src/Dragonfly44.Presentation`: Unpackaged WinUI 3 desktop application (`<WindowsPackageType>None</WindowsPackageType>`), App host, DI configuration, and diagnostic window.
  - `tests/Dragonfly44.Tests`: Automated verification test suite.

### Step 3: Domain Models & Engine Contracts
- Implement entities:
  - `Project`
  - `Chapter`
  - `Page`
  - `Bubble`
  - `Character`
  - `GlossaryEntry`
  - `ChapterContext`
- Define `IScanEngine` interface:
  - `string ProviderId { get; }`
  - `Task<ScanResult> ScanPageAsync(Page page, CancellationToken cancellationToken = default);`
  - `Task<AuditResult> AuditChapterAsync(Chapter chapter, CancellationToken cancellationToken = default);`

### Step 4: Infrastructure & Persistence
- Configure SQLite with `Microsoft.EntityFrameworkCore.Sqlite`.
- Implement `Dragonfly44DbContext` with entity relationships and cascade rules.
- Implement `SqliteDatabaseInitializer` for automated database creation and migrations in `%LOCALAPPDATA%\Dragonfly44\dragonfly44.db`.
- Implement async repositories with full `CancellationToken` support.
- Implement centralized logging (Console, Debug, rolling local file).
- Implement `GlobalExceptionHandler` with hooks into `AppDomain`, `TaskScheduler`, and WinUI `App`.

### Step 5: Presentation Layer (WinUI 3 Diagnostic View)
- Configure `App.xaml.cs` with `Microsoft.Extensions.Hosting.IHost` and DI.
- Connect global exception handlers.
- Create minimal diagnostic `MainWindow.xaml` to display foundation health (DB connection, DI resolution, logging status) without inventing final UI/UX.

### Step 6: Verification & Automated Tests
- Build entire solution with `dotnet build`.
- Execute automated tests verifying:
  - SQLite entity persistence (save and retrieve test entity).
  - Database schema initialization.
  - Dependency Injection resolution of all registered services.
  - Logging output to file.
  - Global exception handling capture.
  - `IScanEngine` contract and stub behavior.

## 3. Technical Decisions and Reasoning
- **Clean Architecture**: Clear separation of concerns guarantees the domain and business logic remain independent of WinUI 3, Windows App SDK, or specific database drivers.
- **EF Core SQLite**: Provides robust relational mapping, schema evolution/migrations, and local-first execution with zero server processes.
- **Unpackaged WinUI 3**: Configured with `<WindowsPackageType>None</WindowsPackageType>` to avoid MSIX package signing friction during development while retaining native Windows 11 controls.
- **Diagnostic-only UI**: Per instructions, no canvas or manga viewer is built; a clean diagnostic view verifies runtime functionality.

## 4. Dependencies and Potential Risks
- **Dependencies**:
  - `Microsoft.DotNet.SDK.8` (via Winget)
  - `Microsoft.WindowsAppSDK` (v1.6 / 1.7)
  - `Microsoft.Windows.SDK.BuildTools`
  - `Microsoft.EntityFrameworkCore.Sqlite`
  - `Microsoft.Extensions.Hosting`
  - `Microsoft.Extensions.Logging`
  - `xUnit`, `FluentAssertions`
- **Risks**:
  - First-time SDK installation via Winget requires path refresh in shell session.
  - Mitigation: Explicitly invoke the installed dotnet binary or refresh environment variables in PowerShell sessions.

## 5. Expected Completion Criteria
1. Solution builds cleanly with zero errors.
2. 100% of automated tests pass (SQLite persistence, DI, logging, exception handling, contracts).
3. The WinUI 3 desktop application launches and confirms diagnostic status.
4. All mandatory task completion logs saved to `logs 44`.
