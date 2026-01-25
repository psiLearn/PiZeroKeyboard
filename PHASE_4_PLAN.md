# LinuxKey Sender - UX Implementation Summary

## Phase 1+2+3: Completed (Quick Wins + Medium Impact + Polish)

### Phase 1: Quick Wins ✅
- **Copy IP:Port Button** - Added to connection banner with visual feedback (✓ checkmark)
- **Clarified Units** - Changed "bytes" to "characters" throughout UI
- **Collapsible Help** - Moved verbose suggestions behind `<details>` element with summary link

### Phase 2: Medium Impact ✅
- **Connection Banner Redesign** - Proper component with icon, target IP, copy button, expandable details
- **Status Indicators** - USB connection + Caps Lock dots with clear color states
- **Keyboard Organization** - Visual grouping by function (Function Keys, Arrows, Modifiers, etc.)

### Phase 3: Polish & Reliability ✅
- **Auto-Retry Checkbox** - Poll `/status` every 5s when disconnected, show countdown
- **Send Timing Feedback** - Display duration: "Sent 1024 characters in 45ms"
- **Material Symbols & Icons** - Font loaded from Google Fonts, ready for icon use
- **Static Files Configuration** - Fixed Giraffe to properly serve wwwroot assets

---

## Phase 4: Layout & Information Architecture

### Major Issues Identified

#### 1. **Wasted Horizontal Space** (CRITICAL)
- Content sits in narrow left column with huge empty right side
- Feels cramped despite plenty of available space
- **Solutions:**
  - Option A: 2-column layout (left: text+actions, right: keyboard)
  - Option B: Centered, wider main column (~900-1100px max-width)
  - **Recommendation:** Option A for desktop, responsive to single-column on mobile

#### 2. **Duplicate Connection UI** (HIGH)
- Connection state shown in TWO places:
  - Top-right status pill (USB dot + Refresh button)
  - Banner (target, copy, details)
- **Solution:** Consolidate into banner, remove top-right pill
- Move "Refresh" action into banner or remove if auto-retry handles it

#### 3. **Chunk Size Label/Unit Mismatch** (HIGH)
- Label says "(characters)" but presets show "256B", "512B", "1KB", "2KB"
- Inconsistent messaging
- **Solution:** Pick one approach:
  - **If characters-based:** Label "Chunk size (characters)" + presets 250/500/1000/2000
  - **If bytes-based:** Label "Chunk size (bytes, UTF-8)" + presets 256B/512B/1KB/2KB
  - Current: appears to be bytes → change label to match

#### 4. **Action Row Hierarchy** (MEDIUM)
- Send button is wide but visually muted (disabled when disconnected)
- Doesn't feel primary
- **Solution:**
  - When connected: stronger color + icon
  - Keyboard hint: "Ctrl+Enter"
  - Secondary actions (Clear/Prev/Next) smaller/dimmer
  - When disconnected: show clear disabled state

#### 5. **Options Card Density** (MEDIUM)
- Functional but busy, hard to scan
- **Solution:** Add section headers inside card:
  - "Typing" (delay slider/input)
  - "Reliability" (chunk size)
  - "Text Formatting" (newline, normalize)
  - "Privacy & Connection" (private send, auto-retry)

### Polish Wins

#### Small but Impactful
- **Details Link State:** Add chevron (▼/▶) to show collapsed/expanded clearly
- **History Placement:** Move ▼ button inline with Prev/Next OR directly under textarea
- **Status Line:** Under action row showing: "Ready" / "Sending…" / "Sent ✓ (3 chunks, 0ms)"
- **Keyboard Latching:** Show active modifiers with filled/pressed visual state
- **Send Icon:** Use Material Symbols (e.g., "send" or "keyboard_arrow_up")

---

## Implementation Priority

### Phase 4a: Layout & Information (Next)
1. Move keyboard to right column (desktop) / below textarea (mobile)
2. Consolidate connection UI into banner only
3. Center main content area or use 2-column grid
4. Fix chunk size label to match units

### Phase 4b: Hierarchy & Density (After 4a)
1. Add section headers to options card
2. Enhance Send button prominence (icon + stronger color)
3. Resize secondary actions (Clear/Prev/Next) to be clearly secondary
4. Add keyboard shortcut hint

### Phase 4c: Polish (After 4b)
1. Add status line under actions
2. Improve Details chevron state
3. Relocate History button
4. Implement keyboard modifier latching visual

---

## Technical Notes

- **No breaking changes** to Phase 1-3 features
- All HTML/CSS, no server-side logic changes needed
- Uses existing Material Symbols font
- Responsive design critical (mobile vs desktop layouts differ)

---

## Files Affected (Phase 4)

- `Views.fs` - HTML structure for 2-column layout
- `sender.css` - Layout grid, component sizing, hierarchy colors
- `sender.js` - Keyboard modifier tracking, status line updates
- `Handlers.fs` - No changes needed (logic is UI-independent)

