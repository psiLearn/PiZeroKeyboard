# Plan: Substitute JS Files

Goal: replace the handwritten JS files in `SenderApp/wwwroot` with Fable-generated client code while preserving behavior and tests.

Scope
- Replace `SenderApp/wwwroot/sender.js` and `SenderApp/wwwroot/history.js` with F# client modules.
- Keep the same DOM IDs and behavior to avoid breaking existing views and tests.

Steps
1. Inventory JS behaviors and map them to F# modules (history storage, status WebSocket, history UI, tokens, send UI state).
2. Implement Fable modules in `SenderApp/Client/src` that mirror the JS exports (`LinuxKeyHistory` API + UI bootstrapping).
3. Wire the Fable output bundle in `Views.renderHead` and remove direct JS references once parity is confirmed.
4. Move/port any helper functions (history previews, list rendering) and keep localStorage + max history logic.
5. Update tests that depend on JS (HistoryTests) to load the Fable output or switch to F#-level tests where possible.
6. Remove old JS files and update documentation / build notes.

Acceptance
- All current UI features still work (history, status, caps lock, keyboard shortcuts).
- Tests pass (unit + history integration tests).
- No direct references to `sender.js`/`history.js` remain in production paths.
