# Changelog

## 0.1.0 - 2026-05-12

### Added
- Added `efpilot diagnostics`.
- Detects DbContexts, design-time factories, startup projects, model snapshots, empty migrations, and `Database.Migrate()` usage.
- Added warning for design-time factories that depend on `Directory.GetCurrentDirectory()` and `appsettings.json`.
- `efpilot status` can now use design-time factories without `--startup-project` when safe.
- Added automatic fallback to the configured startup project.

### Changed
- Changed target framework from .NET 10 to .NET 8.
- Improved CLI output formatting for long project paths.

### Notes
- EfPilot now requires .NET 8 or later.
- EfPilot can still operate on newer .NET projects if the required SDK is installed.
