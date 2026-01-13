open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Runtime.InteropServices
open System.Text
open System.Threading
open ReceiverApp
open ReceiverApp.HidMapping
open ReceiverApp.TextProcessor

type Options =
    { Port: int
      Emulate: bool
      HidPath: string
      Layout: HidMapping.KeyboardLayout }

let defaultHidPath = "/dev/hidg0"

let parseOptions (argv: string[]) =
    let mutable port = 5000
    let mutable emulate = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
    let mutable hidPath = defaultHidPath
    let defaultLayout =
        Environment.GetEnvironmentVariable "RECEIVER_LAYOUT"
        |> Option.ofObj
        |> Option.bind HidMapping.tryParseLayout
        |> Option.defaultValue HidMapping.KeyboardLayout.En

    let mutable layout = defaultLayout

    for arg in argv do
        match arg with
        | "--emulate" -> emulate <- true
        | "--no-emulate" -> emulate <- false
        | _ when arg.StartsWith("--hid-path=", StringComparison.OrdinalIgnoreCase) ->
            hidPath <- arg.Substring("--hid-path=".Length)
        | _ when arg.StartsWith("--layout=", StringComparison.OrdinalIgnoreCase) ->
            let value = arg.Substring("--layout=".Length)
            match HidMapping.tryParseLayout value with
            | Some parsed -> layout <- parsed
            | None -> printfn "Unknown layout '%s'. Supported: en, de." value
        | _ ->
            match Int32.TryParse arg with
            | true, value -> port <- value
            | _ -> printfn "Ignoring unknown argument '%s'" arg

    { Port = port
      Emulate = emulate
      HidPath = hidPath
      Layout = layout }

let openHidStream path =
    printfn "Waiting for HID device at %s..." path
    let rec loop () =
        if not (File.Exists path) then
            Thread.Sleep 1000
            loop ()
        else
            try
                new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)
            with
            | :? IOException
            | :? UnauthorizedAccessException ->
                Thread.Sleep 1000
                loop ()
    loop ()

let createSender hidPath =
    let mutable stream: FileStream option = None

    let rec ensureStream () =
        match stream with
        | Some current when current.CanWrite -> current
        | Some current ->
            current.Dispose()
            stream <- None
            ensureStream ()
        | None ->
            let opened = openHidStream hidPath
            stream <- Some opened
            opened

    let writeReport (payload: byte[]) =
        let rec attempt () =
            try
                let current = ensureStream ()
                current.Write(payload, 0, payload.Length)
                current.Flush()
            with
            | :? FileNotFoundException
            | :? IOException
            | :? UnauthorizedAccessException
            | :? ObjectDisposedException ->
                eprintfn "HID write failed. Reopening %s..." hidPath
                stream |> Option.iter (fun current -> current.Dispose())
                stream <- None
                Thread.Sleep 250
                attempt ()
        attempt ()

    fun (hid: HidKey) ->
        let press = Array.zeroCreate<byte> 8
        press.[0] <- hid.Modifier
        press.[2] <- hid.Key
        writeReport press
        Thread.Sleep 5

        let release = Array.zeroCreate<byte> 8
        writeReport release
        Thread.Sleep 5

let logUnsupported c =
    printfn "Skipping unsupported char '%c' (0x%04X)" c (int c)

[<EntryPoint>]
let main argv =
    let options = parseOptions argv
    let listener = new TcpListener(IPAddress.Any, options.Port)
    listener.Start()
    printfn "Receiver listening on port %d" options.Port
    if options.Emulate then
        printfn "Running in emulate mode. Incoming text will be displayed instead of typing."
    else
        let layoutLabel =
            match options.Layout with
            | HidMapping.KeyboardLayout.En -> "en"
            | HidMapping.KeyboardLayout.De -> "de"

        printfn "Using HID device at %s (layout: %s)" options.HidPath layoutLabel

    let rec runLoop send =
        try
            printfn "Waiting for client..."
            use client = listener.AcceptTcpClient()
            printfn "Client connected."
            use stream = client.GetStream()
            use reader = new StreamReader(stream, Encoding.UTF8, false)
            let text = reader.ReadToEnd()
            printfn "Received %d characters" text.Length

            if options.Emulate then
                printfn "[EMULATED OUTPUT]\n%s" text

            TextProcessor.processText send logUnsupported options.Layout text
        with ex ->
            eprintfn "Error while processing client: %s" ex.Message
            Thread.Sleep 500

        runLoop send

    if options.Emulate then
        let noop _ = ()
        runLoop noop
    else
        let send = createSender options.HidPath
        runLoop send

    0
