# Fable Migration Analysis
## Identifying JavaScript Files for F# Replacement

**Date:** January 25, 2026  
**Project:** PiZeroKeyboard - SenderApp  
**Analysis:** Which JS files are good candidates for Fable migration

---

## Executive Summary

| File | Lines | Complexity | Fable Suitability | Priority | Recommendation |
|------|-------|-----------|-------------------|----------|-----------------|
| **history.js** | 132 | ⭐⭐ Low | 🟢 Excellent | 1️⃣ **HIGH** | ✅ **MIGRATE NOW** |
| **sender.js** | 305 | ⭐⭐⭐⭐ High | 🟡 Medium | 2️⃣ Medium | ⏳ Defer Phase 2 |

**Total JavaScript:** 437 lines  
**Easy to Migrate (Fable):** 132 lines (30%)  
**Candidate for Later:** 305 lines (70%)

---

## Detailed Analysis

### 1. ✅ HISTORY.JS — BEST CANDIDATE FOR IMMEDIATE MIGRATION

**File:** `SenderApp/wwwroot/history.js` (132 lines)

#### Characteristics
- **Purpose:** localStorage abstraction + history management API
- **Pattern:** UMD module exporting a namespace object
- **State:** Stateless functions (pure functions)
- **Complexity:** Low to Medium

#### What It Does
```javascript
✅ Safe localStorage access (try-catch wrapped)
✅ Item serialization/deserialization (JSON)
✅ Format migration (old string → new object)
✅ Index clamping and validation
✅ History item formatting with timestamps
```

#### Why It's Easy to Migrate

1. **No DOM Dependencies**
   - Doesn't interact with HTML elements
   - Doesn't use event listeners
   - Works entirely with data structures

2. **Pure Function Patterns**
   - readHistory() → parse + return
   - writeHistory() → stringify + save
   - loadHistoryState() → combine read operations
   - All functions are deterministic

3. **Simple Type Signatures**
   ```fsharp
   // Can map directly to F# types
   readHistory: Storage → string → Item list
   writeHistory: Storage → string → Item list → unit
   clampIndex: int → int → int
   // etc.
   ```

4. **Minimal Browser API Usage**
   - Only uses `localStorage` (JavaScript's Storage interface)
   - Only uses `Date.now()` for timestamps
   - Only uses standard `JSON.parse()` / `JSON.stringify()`
   - **All available in Fable!** ✅

5. **Great for Testing**
   - Mock Storage interface easily
   - Test all branches without DOM
   - No async operations
   - Fast test execution

#### Migration Effort

**Estimated: 1-2 hours**

```
Preparation:        15 min
  └─ Create History.fs stub
  └─ Define F# types

Implementation:     45 min
  ├─ readHistory function
  ├─ writeHistory function
  ├─ Helper functions (clamp, format, etc.)
  └─ Module interface

Testing:            15 min
  ├─ Unit tests for each function
  ├─ Edge cases (empty, null, migration)
  └─ Storage error handling

Integration:        15 min
  ├─ Wire into sender.js handler
  ├─ Test in browser
  └─ Verify persistence
```

#### F# Implementation Sketch

```fsharp
// SenderApp/Client/src/History.fs
module LinuxKeyHistory

open System
open Browser.Storage
open Browser.Dom

type HistoryItem = {
    text: string
    timestamp: float option
}

let safeGetItem (storage: Storage) (key: string) : string option =
    try
        match storage.getItem key with
        | null -> None
        | value -> Some value
    with _ -> None

let safeSetItem (storage: Storage) (key: string) (value: string) : unit =
    try
        storage.setItem(key, value)
    with _ -> () // Ignore failures

let parseItems (raw: string option) : HistoryItem list =
    match raw with
    | None -> []
    | Some json ->
        try
            let parsed = JS.JSON.parse json |> Array.ofObj
            parsed
            |> Array.choose (fun item ->
                if isNull item then None
                else
                    // Support both old string and new object format
                    if typeof<string> = item.GetType() then
                        Some { text = string item; timestamp = Some (DateTime.Now.GetTime()) }
                    elif item?text <> undefined then
                        Some { text = item?text; timestamp = item?timestamp }
                    else None
            )
            |> Array.toList
        with _ -> []

let formatHistoryPreview (item: HistoryItem) : string =
    let preview =
        if item.text.Length > 30
        then item.text.[0..29] + "…"
        else item.text
    
    match item.timestamp with
    | None -> preview
    | Some ts ->
        let date = DateTime.ofMilliseconds ts
        let timeStr = sprintf "%02d:%02d:%02d | " date.Hours date.Minutes date.Seconds
        timeStr + preview

let readHistory (storage: Storage) (key: string) : HistoryItem list =
    key |> safeGetItem storage |> parseItems

let writeHistory (storage: Storage) (key: string) (items: HistoryItem list) : unit =
    items |> JS.JSON.stringify |> safeSetItem storage key

let clampIndex (index: int) (maxIndex: int) : int =
    if maxIndex < 0 then 0
    elif isNaN index then maxIndex
    else Math.Min(Math.Max(index, 0), maxIndex)

let readHistoryIndex (storage: Storage) (key: string) (maxIndex: int) : int =
    if maxIndex < 0 then 0
    else
        match safeGetItem storage key with
        | None | Some "" -> maxIndex
        | Some raw ->
            match System.Int32.TryParse raw with
            | true, index -> clampIndex index maxIndex
            | false, _ -> clampIndex 0 maxIndex

let writeHistoryIndex (storage: Storage) (key: string) (index: int) : unit =
    index.ToString() |> safeSetItem storage key

let loadHistoryState (storage: Storage) (historyKey: string) (indexKey: string) =
    let items = readHistory storage historyKey
    if items.IsEmpty then
        { items = []; index = 0 }
    else
        let maxIndex = items.Length - 1
        let index = readHistoryIndex storage indexKey maxIndex
        { items = items; index = index }

let addHistoryEntry (storage: Storage) (historyKey: string) (indexKey: string) (text: string) =
    let trimmed = (text |> string).Trim()
    if trimmed = "" then
        loadHistoryState storage historyKey indexKey
    else
        let items = readHistory storage historyKey
        let shouldAdd =
            items.IsEmpty ||
            (match items |> List.tryLast with
             | Some lastItem -> lastItem.text <> trimmed
             | None -> true)
        
        if shouldAdd then
            let newItems = items @ [{ text = trimmed; timestamp = Some (DateTime.Now.GetTime()) }]
            writeHistory storage historyKey newItems
            { items = newItems; index = newItems.Length - 1 }
        else
            { items = items; index = if items.IsEmpty then 0 else items.Length - 1 }
```

#### Browser API Requirements

✅ Available in Fable:
- `Browser.Storage` - localStorage access
- `Browser.Dom` - Date/time functions
- `JS.JSON.parse()` / `JS.JSON.stringify()`

#### Why High Priority

1. **Simplest Migration** - Pure functions, no DOM
2. **Tests Will Verify** - Easy to unit test
3. **Demonstrates Fable** - Great proof-of-concept
4. **Independent** - Doesn't block other work
5. **Reusable** - Can be used by other modules

---

### 2. ⏳ SENDER.JS — DEFERRABLE FOR PHASE 2

**File:** `SenderApp/wwwroot/sender.js` (305 lines)

#### Characteristics
- **Purpose:** Event handlers, DOM manipulation, WebSocket connection
- **Pattern:** Immediate execution on DOMContentLoaded
- **State:** Multiple stateful closures (autoRetryTimer, historyItems, etc.)
- **Complexity:** High (DOM, async, timing)

#### What It Does

```javascript
✅ Copy button interaction (navigator.clipboard)
✅ Status fetch + WebSocket connection
✅ Auto-retry countdown state machine
✅ Event listeners (checkbox, buttons, form)
✅ History navigation with UI updates
✅ Form submission handling
✅ Keyboard shortcuts (Ctrl+Enter)
✅ DOM element state management
```

#### Why It's More Complex

1. **Heavy DOM Dependencies**
   ```javascript
   document.getElementById()              // DOM queries
   document.querySelectorAll()            // DOM queries  
   element.addEventListener()            // Event binding
   element.classList.toggle()             // Class manipulation
   textarea.setSelectionRange()           // Textarea manipulation
   ```
   → Requires extensive `fable-browser-dom` wrappers

2. **Multiple Closures with State**
   ```javascript
   let autoRetryEnabled = false;          // Closure state
   let autoRetryTimer = null;             // Closure state
   let retryCountdownTimer = null;        // Closure state
   let nextRetryCountdown = 0;            // Closure state
   ```
   → F# needs clear state machine design

3. **Async Operations**
   ```javascript
   fetch('/status')                       // HTTP
   new WebSocket(url)                     // WebSocket
   setTimeout()                           // Timers
   globalThis.setTimeout()                // Global timers
   ```
   → Needs Promises / async/await mapping

4. **Event Coordination**
   ```javascript
   form.submit = function() { ... }       // Override method
   socket.onmessage = (event) => { ... }  // Event handler
   button.addEventListener(...)           // Multiple handlers
   ```
   → Complex event flow to track

#### Migration Effort (If Attempted)

**Estimated: 6-10 hours** (Deferred - too complex now)

```
Analysis:           1 hour
  └─ Untangle closures
  └─ Map event flow

Design:             1.5 hours
  ├─ State machine (auto-retry)
  ├─ WebSocket lifecycle
  └─ Event handler patterns

Implementation:     3-4 hours
  ├─ DOM query wrappers
  ├─ Event handlers
  ├─ Async/await flows
  └─ State transitions

Testing:            1-2 hours
  ├─ Mock DOM elements
  ├─ Mock WebSocket
  └─ Test state transitions

Debugging/Polish:   1 hour
```

#### Why Defer This

1. **History.js is simpler** - Get quick win first
2. **Needs refactoring first** - Untangle closures in F#
3. **Complex async patterns** - Better to understand in JS first
4. **Working well now** - Not broken, no urgency
5. **Blocks other work** - Needs focused attention

#### When to Migrate sender.js

**Recommended: Phase 2 (Future Sprint)**

Prerequisites:
- ✅ history.js successfully migrated
- ✅ Team comfortable with Fable browser patterns
- ✅ More experience with fable-browser-dom
- ✅ Potential refactoring to reduce closure complexity

Approach:
```fsharp
// Split into modules
Modules.History         // Already migrated ✅
Modules.Status          // status fetch + WebSocket
Modules.AutoRetry       // Retry state machine
Modules.FormHandling    // Form submit logic
Modules.Editor          // Textarea interaction
Modules.Shortcuts       // Keyboard handling
Modules.Initialization  // DOMContentLoaded setup
```

---

## Recommendation Summary

### ✅ IMMEDIATE ACTION (This Sprint)

**Migrate: history.js → History.fs**

| Aspect | Rating | Reason |
|--------|--------|--------|
| Complexity | ⭐⭐ Low | Pure functions, no DOM |
| Risk | 🟢 Low | Isolated, well-tested |
| Value | 🟢 High | Proof of concept |
| Time | 🟢 1-2 hrs | Quick win |
| **Recommendation** | **✅ GO** | **Migrate now** |

**Next Steps:**
1. Create `SenderApp/Client/src/History.fs`
2. Implement all functions from history.js
3. Add unit tests in `SenderApp.Tests/`
4. Update sender.js to call `window.LinuxKeyHistory.readHistory()` etc.
5. Verify in browser

---

### ⏳ PHASE 2 (Future Sprint)

**Defer: sender.js → Sender.fs**

| Aspect | Rating | Reason |
|--------|--------|--------|
| Complexity | ⭐⭐⭐⭐ High | DOM, async, closures |
| Risk | 🟡 Medium | Complex state flows |
| Value | 🟡 Medium | Better code org |
| Time | 🟡 6-10 hrs | Significant effort |
| **Recommendation** | **⏳ DEFER** | **Do after history.js** |

**When Ready:**
- After successful history.js migration
- When team has more Fable-browser experience
- Possibly after refactoring to reduce closure complexity

---

## Files Currently NOT JS (Already F#)

✅ Views are in F# (Views.fs)
✅ Handlers are in F# (Handlers.fs)  
✅ Services are in F# (ConnectionService.fs, etc.)
✅ Types are in F# (Types.fs)
✅ Startup is in F# (Startup.fs)

### Why JS Remains Necessary

```
Current Architecture:

Backend (F#)                    Frontend (JS/HTML)
├─ Server rendering            ├─ sender.html (Giraffe views)
│  └─ Views.fs                 ├─ sender.js (DOM + events)
├─ HTTP handlers               ├─ history.js (localStorage)
│  └─ Handlers.fs              ├─ sender.css (styling)
├─ Business logic              └─ (static files)
│  └─ Types.fs, Services.fs
└─ Startup/Config

Connection Method:
  Giraffe renders HTML with embedded JS
  JS adds interactivity and event handling
  WebSocket connection for real-time status
```

**Why We Can't Remove JS Entirely:**
- Browser only executes JavaScript
- Fable transpiles F# → JavaScript
- Until JS removed, need JS execution layer
- **Solution:** Fable IS the solution (F# → JS compilation)

---

## Technology Stack for Migration

### Fable 4.8.0 + fable-browser

**Available Now:**
```fsharp
Browser.Dom              // DOM access
Browser.Types            // Type definitions
Browser.Storage          // localStorage ✅
Browser.Api              // Fetch, WebSocket ✅
Browser.Event            // Event handling
Browser.Css              // Style manipulation
```

**Example Usage:**
```fsharp
open Browser.Dom

// localStorage
localStorage.setItem("key", "value")
let value = localStorage.getItem("key")

// Fetch
let promise = fetch "/status" |> Promise.bind (fun r -> r.json())

// WebSocket
let ws = Browser.WebSocket.WebSocket("ws://example.com")
ws.onmessage <- fun evt -> printfn "%O" evt.data
```

---

## Implementation Checklist for history.js Migration

### Phase 1: Setup (15 min)
- [ ] Create `SenderApp/Client/src/History.fs`
- [ ] Add History module to project
- [ ] Create unit test file
- [ ] Set up Fable build target

### Phase 2: Core Implementation (45 min)
- [ ] Implement readHistory()
- [ ] Implement writeHistory()
- [ ] Implement formatHistoryPreview()
- [ ] Implement clampIndex()
- [ ] Implement readHistoryIndex() / writeHistoryIndex()
- [ ] Implement loadHistoryState()
- [ ] Implement addHistoryEntry()

### Phase 3: Testing (30 min)
- [ ] Unit test readHistory() with various inputs
- [ ] Unit test format migration (old → new)
- [ ] Unit test edge cases (empty, null, max)
- [ ] Integration test with mock Storage
- [ ] Browser test (verify localStorage works)

### Phase 4: Integration (15 min)
- [ ] Update sender.js to use Fable output
- [ ] Verify history buttons still work
- [ ] Verify persistence across sessions
- [ ] Verify Ctrl+Enter still works
- [ ] Check console for errors

### Phase 5: Cleanup (15 min)
- [ ] Update documentation
- [ ] Commit to design branch
- [ ] Create PR notes
- [ ] Update CHANGELOG.md

---

## Conclusion

| Task | Easy? | Benefit | Timeline |
|------|-------|---------|----------|
| **Migrate history.js** | ✅ YES | Medium | **Start immediately** |
| **Migrate sender.js** | ❌ NO | Higher | Phase 2 (future) |

**Next Action:** Begin history.js migration using F# + Fable 🚀

---

## References

- Fable Browser Docs: https://fable.io/docs/javascript/browser.html
- JavaScript File: [SenderApp/wwwroot/sender.js](SenderApp/wwwroot/sender.js)
- History Module: [SenderApp/wwwroot/history.js](SenderApp/wwwroot/history.js)
- Fable Config: [SenderApp/Client/fable.json](SenderApp/Client/fable.json)
