Design review

What’s working well

Clear primary flow: layout picker → paste area → Send.

Simple, uncluttered page: users won’t get lost.

On-screen special keys are a nice touch for remote-control scenarios.

Biggest usability gaps (high impact)

No obvious “status / target / connection” indicator

Users need confidence that the receiver is connected and where input will go.

Add a small banner: Connected to: hostname/IP + latency + last activity. If not connected, show “Not connected” with fix steps.

No feedback after Send

After pressing Send, show progress and outcome:

“Sending… (34%)”

“Sent ✓” / “Failed ✕ (reason)”

Also consider a “Stop/Cancel” if sending long text.

Back / Forward is unclear

Back/Forward looks like browser navigation, but likely means “history of sent texts”.

Rename to History ‹ / › or use icons with tooltips: “Previous snippet / Next snippet”.

Show a small “History” list/dropdown (last 10 items) for faster access.

Modifier keys need “toggle” behavior

For remote keyboard apps, users expect Shift/Ctrl/Alt/Win to latch (sticky) or be momentary.

Add a visible pressed state and an option:

Tap = latch, tap again to release

Hold = momentary

Same for Caps Lock.

UI layout improvements

Keyboard section is visually dense and not grouped like a real keyboard.

Group into blocks: Function keys, navigation cluster, modifiers, arrows, editing keys.

Use spacing and subtle separators.

Put the most used actions near the textarea:

“Send” + “Clear” + “Copy” + “Paste” (if allowed) + “Send as clipboard” (optional).

Consider making the on-screen keyboard collapsible (“Show keyboard” / “Hide keyboard”) so the text area can be taller.

Accessibility & keyboard navigation

Ensure every button has:

Proper semantic <button> elements

Clear focus rings (visible)

aria-label where text is ambiguous (e.g., PgUp, Win)

Tab order should be logical: layout → textarea → Send → history → keys.

Contrast check: the dark gray buttons on light background likely pass, but verify WCAG AA (especially small text on gray keys).

The text area placeholder is fine, but consider adding a label above it (“Text to send”) for screen readers.

Functionality suggestions (very relevant to this kind of tool)

Send modes

“Send as keystrokes (typed)”

“Send as clipboard + paste” (often faster/more reliable if you control receiver)

Rate control

Typing delay (ms per character) and “chunking” for apps that drop keys under load.

Newline behavior

Option: “Append Enter at end”

Option: “Normalize line endings” (LF vs CRLF)

Escape / abort

A dedicated “Abort” button if something goes wrong mid-send.

Layout edge cases

Many layouts differ for punctuation; show a quick warning if layout mismatch is detected or likely.

Safety / security considerations (don’t skip)

If this app sends keystrokes to a machine, it’s effectively a remote control tool:

Require authentication (even basic password/token)

Use HTTPS

Avoid exposing it openly on a LAN without access control

If there’s a “connected host” concept, make it explicit to avoid accidental sending to the wrong target.

Visual polish (quick wins)

Add a small subtitle under the title explaining what it does (“Send text/keys to a Linux receiver”).

Move Refresh to align with other controls or make it contextual (“Reconnect”).

Add subtle states:

Disabled keys when not connected

Loading spinner on Send

Success toast

A prioritized “next sprint” list

P0 (must-have):

Connection/target status + Send success/fail feedback

Rename Back/Forward or clarify with tooltips + history UI

Modifier latch behavior + visible pressed state

P1:

Collapsible keyboard + better grouping

Typing speed/delay controls

Append Enter option

P2:

Clipboard mode, presets, and accessibility refinements