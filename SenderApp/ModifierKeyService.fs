namespace SenderApp

module ModifierKeyService =
    open System
    
    type ModifierKeyMode =
        | Momentary
        | Sticky
    
    type ModifierKeyState =
        | Released
        | Pressed
    
    type ModifierKeyInfo =
        { Name: string
          Token: string
          Mode: ModifierKeyMode
          State: ModifierKeyState }
    
    /// Initialize default modifier keys with sticky mode for remote control
    let initializeModifierKeys () : ModifierKeyInfo list =
        [ { Name = "Shift"; Token = "{SHIFT}"; Mode = Sticky; State = Released }
          { Name = "Ctrl"; Token = "{CTRL}"; Mode = Sticky; State = Released }
          { Name = "Alt"; Token = "{ALT}"; Mode = Sticky; State = Released }
          { Name = "Win"; Token = "{WIN}"; Mode = Sticky; State = Released }
          { Name = "Caps Lock"; Token = "{CAPSLOCK}"; Mode = Sticky; State = Released } ]
    
    /// Toggle a modifier key's pressed state
    let toggleKey (key: ModifierKeyInfo) : ModifierKeyInfo =
        let newState = 
            match key.State with
            | Released -> Pressed
            | Pressed -> Released
        { key with State = newState }
    
    /// Check if a key is currently pressed
    let isPressed (key: ModifierKeyInfo) : bool =
        key.State = Pressed
    
    /// Get CSS class for visual feedback of key state
    let getKeyStateClass (key: ModifierKeyInfo) : string =
        match key.State with
        | Pressed -> "key-pressed"
        | Released -> "key-released"
    
    /// Get aria-pressed attribute value
    let getAriaPressed (key: ModifierKeyInfo) : string =
        match key.State with
        | Pressed -> "true"
        | Released -> "false"
