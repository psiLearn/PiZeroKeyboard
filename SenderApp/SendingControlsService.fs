namespace SenderApp

module SendingControlsService =
    /// Configuration constraints
    let private maxDelayMs = 10000
    let private maxChunkSize = 10000

    /// Configuration for how text is sent to the receiver
    type SendingMode =
        | AllAtOnce
        | ChunkedWithDelay of chunkSize: int * delayMs: int
    
    /// Per-character typing delay configuration
    type TypingDelayConfig =
        { DelayPerCharacterMs: int
          ChunkSize: int }
    
    /// Sending control settings
    type SendingControls =
        { TypingDelayMs: int
          ChunkSize: int
          SendMode: SendingMode
          AppendNewlineAtEnd: bool
          NormalizeLineEndings: bool }
    
    /// Default sending controls: all at once with no delay
    let defaultControls : SendingControls =
        { TypingDelayMs = 0
          ChunkSize = 1000
          SendMode = AllAtOnce
          AppendNewlineAtEnd = false
          NormalizeLineEndings = false }
    
    /// Validate delay configuration
    let validateDelay (ms: int) : Result<int, string> =
        match ms with
        | ms when ms < 0 -> Error "Delay must be non-negative"
        | ms when ms > maxDelayMs -> Error (sprintf "Delay cannot exceed %dms" maxDelayMs)
        | _ -> Ok ms
    
    /// Validate chunk size
    let validateChunkSize (size: int) : Result<int, string> =
        match size with
        | size when size < 1 -> Error "Chunk size must be at least 1"
        | size when size > maxChunkSize -> Error (sprintf "Chunk size cannot exceed %d" maxChunkSize)
        | _ -> Ok size
    
    /// Calculate estimated send time for a text
    let estimateSendTime (text: string) (controls: SendingControls) : int =
        match controls.SendMode with
        | AllAtOnce -> 0
        | ChunkedWithDelay (_, delayMs) ->
            let numChunks = (text.Length + controls.ChunkSize - 1) / controls.ChunkSize
            (numChunks - 1) * delayMs + (text.Length * controls.TypingDelayMs)
    
    /// Normalize line endings to LF (Unix style)
    let normalizeLineEndings (text: string) : string =
        // Normalize all line ending variants to LF
        text
        |> fun t -> t.Replace("\r\n", "\n")  // CRLF → LF
        |> fun t -> t.Replace("\r", "\n")    // CR → LF
    
    /// Process text according to sending controls
    let processText (text: string) (controls: SendingControls) : string =
        let result = 
            if controls.NormalizeLineEndings then
                normalizeLineEndings text
            else
                text
        
        if controls.AppendNewlineAtEnd && not (result.EndsWith("\n")) then
            result + "\n"
        else
            result
