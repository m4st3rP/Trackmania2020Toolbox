# Code Review: Trackmania2020Toolbox

## Summary
The codebase is well-structured with a clear separation of concerns between core logic, CLI, and Desktop GUI. It uses modern C# features and follows good practices like interface-based design for easier testing. However, there are some areas where robustness, configurability, and idiomatic usage can be improved.

---

## 🔴 Blockers
*None identified.* The code appears logically sound and follows basic security practices (like sanitizing filenames).

---

## 🟡 Suggestions

### 1. Configurable Download Delay
**File:** `src/Trackmania2020Toolbox.Core/Trackmania2020Toolbox.cs`
**Lines:** 1198, 1417, 1426
**Why:** The 1000ms delay is hardcoded. Users with better API standing or those wanting to respect different rate limits might want to adjust this.
**Proposed Implementation:** Add `DownloadDelayMs` to `DownloaderConfig` and use it in `Task.Delay()`.

### 2. Improve Interactive Selection Robustness
**File:** `src/Trackmania2020Toolbox.CLI/TrackmaniaCLI.cs`
**Line:** 17
**Why:** `SelectItemAsync` currently returns 0 on any invalid input and doesn't explain why it failed. A better UX would be to loop until a valid selection or explicit cancel is made.
**Proposed Implementation:** Use a `while` loop to re-prompt on invalid input.

### 3. Specific Exception Handling in Medal Export
**File:** `src/Trackmania2020Toolbox.Core/Trackmania2020Toolbox.cs`
**Line:** 1450
**Why:** Catching all exceptions with a generic message makes debugging harder. Distinguishing between "No Record Found" (API 404/500) and other network issues is beneficial.
**Proposed Implementation:** Catch `HttpRequestException` specifically and log more details if it's not a standard "no record" scenario.

---

## 💭 Nits

### 1. Inconsistent User-Agent
**File:** `src/Trackmania2020Toolbox.CLI/TrackmaniaCLI.cs`
**Line:** 36
**Why:** The User-Agent in code uses a different GitHub URL than what is recommended in the project's documentation/memory.
**Proposed Implementation:** Update to `Trackmania2020Toolbox/1.0 (+https://github.com/AI-Citizen/Trackmania2020Toolbox)`.

### 2. Magic Numbers/Strings
**File:** `src/Trackmania2020Toolbox.Core/Trackmania2020Toolbox.cs`
**Line:** 295
**Why:** The `OrbitalDev@falguiere` string is a magic string used in the fixer. While functional, it could be a constant.
