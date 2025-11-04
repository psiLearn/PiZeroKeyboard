open System
open System.Net.Sockets
open System.Text

[<EntryPoint>]
let main argv =
    let usage () =
        printfn "Usage: dotnet run -- <pi-ip> <port> \"text to send\""
        1

    match argv with
    | [| ip; port; text |] ->
        try
            use client = new TcpClient()
            client.Connect(ip, int port)
            use stream = client.GetStream()
            let payload = Encoding.UTF8.GetBytes(text)
            stream.Write(payload, 0, payload.Length)
            printfn "Sent %d bytes to %s:%s" payload.Length ip port
            0
        with ex ->
            eprintfn "Failed: %s" ex.Message
            1
    | _ -> usage ()
