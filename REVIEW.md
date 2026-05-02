# Code Review: Trackmania2020Toolbox

## Summary
The codebase has been significantly improved in terms of testability, performance, and robustness. Key architectural enhancements include dependency injection for time-based operations and asynchronous UI optimizations.

---

## 🔴 Blockers
*None identified.*

---

## 🟡 Suggestions (Implemented)

### 1. Configurable Download Delay
**Status:** ✅ Implemented.
**Implementation:** `DownloadDelayMs` added to `DownloaderConfig` and integrated into `MapDownloader.DownloadAndFixMapsAsync` and `ToolboxApp.HandleExportCampaignMedalsAsync`.

### 2. Improve Interactive Selection Robustness
**Status:** ✅ Implemented.
**Implementation:** `RealConsole.SelectItemAsync` now uses a `while` loop to re-prompt users until a valid selection or explicit cancellation is provided.

### 3. Specific Exception Handling in Medal Export
**Status:** ✅ Implemented.
**Implementation:** `HandleExportCampaignMedalsAsync` now distinguishes between API-reported "no record" (404/500) and actual network/system errors.

---

## 💭 Nits (Implemented)

### 1. Inconsistent User-Agent
**Status:** ✅ Implemented.
**Implementation:** Standardized on `Trackmania2020Toolbox/1.0 (+https://github.com/AI-Citizen/Trackmania2020Toolbox)`.

### 2. Magic Numbers/Strings
**Status:** ✅ Implemented.
**Implementation:** Moved magic strings in `RealMapFixer` to public constants.

---

## 🚀 Architectural Improvements (New)

### 1. Enhanced Testability via IDateTime
**Context:** Added `IDateTime` injection to `CachedTrackmaniaApi`.
**Benefit:** Allows for exact, deterministic testing of cache expiration policies without relying on real-world system clock drift.

### 2. Asynchronous Browser Performance
**Context:** Offloaded browser file listing to background threads in the Desktop UI.
**Benefit:** Prevents the UI from freezing when navigating large map folders or over slow file systems.

### 3. Robust Range Parsing
**Context:** Improved `InputParser` to handle cross-year and cross-month rollovers in Track of the Day date ranges (e.g., `2024.12.30-01.05`).
**Benefit:** Provides a more intuitive and reliable experience for users downloading maps across calendar boundaries.

### 4. Resource Lifecycle Management
**Context:** Ensured `RealConfigService` is properly disposed of in both CLI and Desktop environments.
**Benefit:** Guarantees that the underlying `SemaphoreSlim` is cleaned up, preventing potential resource leaks or sync issues during app shutdown.
