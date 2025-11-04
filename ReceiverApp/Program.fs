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
      HidPath: string }

let defaultHidPath = "/dev/hidg0"

let parseOptions (argv: string[]) =
    let mutable port = 5000
    let mutable emulate = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
    let mutable hidPath = defaultHidPath

    for arg in argv do
        match arg with
        | "--emulate" -> emulate <- true
        | "--no-emulate" -> emulate <- false
        | _ when arg.StartsWith("--hid-path=", StringComparison.OrdinalIgnoreCase) ->
            hidPath <- arg.Substring("--hid-path=".Length)
        | _ ->
            match Int32.TryParse arg with
            | true, value -> port <- value
            | _ -> printfn "Ignoring unknown argument '%s'" arg

    { Port = port
      Emulate = emulate
      HidPath = hidPath }

let createSender (stream: FileStream) =
    fun (hid: HidKey) ->
        let press = Array.zeroCreate<byte> 8
        press.[0] <- hid.Modifier
        press.[2] <- hid.Key
        stream.Write(press, 0, press.Length)
        stream.Flush()
        Thread.Sleep 5

        let release = Array.zeroCreate<byte> 8
        stream.Write(release, 0, release.Length)
        stream.Flush()
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
        printfn "Using HID device at %s" options.HidPath

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

            TextProcessor.processText send logUnsupported text
        with ex ->
            eprintfn "Error while processing client: %s" ex.Message
            Thread.Sleep 500

        runLoop send

    if options.Emulate then
        let noop _ = ()
        runLoop noop
    else
        use hidStream = new FileStream(options.HidPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)
        let send = createSender hidStream
        runLoop send

    0
