namespace SenderApp

module UsbStatusWatchers =
    open System
    open System.IO
    open System.Threading.Channels

    type UsbStatusWatchers =
        { Channel: Channel<unit>
          RefreshStateWatcher: unit -> unit
          Dispose: unit -> unit }

    let private createStatusChannel () =
        let options = BoundedChannelOptions(1)
        options.FullMode <- BoundedChannelFullMode.DropOldest
        options.SingleReader <- true
        options.SingleWriter <- false
        Channel.CreateBounded<unit>(options)

    let private tryCreateWatcher directory filter notifyFilter (enqueue: unit -> unit) =
        if String.IsNullOrWhiteSpace directory || not (Directory.Exists directory) then
            None
        else
            let watcher = new FileSystemWatcher(directory, filter)
            watcher.NotifyFilter <- notifyFilter
            watcher.Changed.Add (fun _ -> enqueue())
            watcher.Created.Add (fun _ -> enqueue())
            watcher.Renamed.Add (fun _ -> enqueue())
            watcher.Deleted.Add (fun _ -> enqueue())
            watcher.EnableRaisingEvents <- true
            Some watcher

    let private disposeWatcher (watcher: FileSystemWatcher option) =
        watcher |> Option.iter (fun entry -> entry.Dispose())

    let create (tryGetStatePath: unit -> string option) (eventPath: string option) (capsPath: string option) =
        let channel = createStatusChannel ()
        let enqueue () = channel.Writer.TryWrite(()) |> ignore

        let udcWatcher =
            tryCreateWatcher
                "/sys/class/udc"
                "*"
                NotifyFilters.DirectoryName
                enqueue

        let devWatcher =
            tryCreateWatcher
                "/dev"
                "hidg0"
                (NotifyFilters.FileName ||| NotifyFilters.CreationTime)
                enqueue

        let eventWatcher =
            match eventPath with
            | None -> None
            | Some path ->
                let directory = Path.GetDirectoryName path
                let fileName = Path.GetFileName path
                tryCreateWatcher
                    directory
                    fileName
                    (NotifyFilters.FileName ||| NotifyFilters.LastWrite ||| NotifyFilters.CreationTime)
                    enqueue

        let capsWatcher =
            match capsPath with
            | None -> None
            | Some path ->
                let directory = Path.GetDirectoryName path
                let fileName = Path.GetFileName path
                tryCreateWatcher
                    directory
                    fileName
                    (NotifyFilters.FileName ||| NotifyFilters.LastWrite ||| NotifyFilters.CreationTime)
                    enqueue

        let mutable statePath = ""
        let mutable stateWatcher: FileSystemWatcher option = None

        let refreshStateWatcher () =
            let nextPath =
                match tryGetStatePath () with
                | Some path -> path
                | None -> ""

            if not (String.Equals(statePath, nextPath, StringComparison.OrdinalIgnoreCase)) then
                disposeWatcher stateWatcher
                stateWatcher <- None
                statePath <- nextPath

                if not (String.IsNullOrWhiteSpace statePath) then
                    let directory = Path.GetDirectoryName statePath
                    let fileName = Path.GetFileName statePath
                    stateWatcher <-
                        tryCreateWatcher
                            directory
                            fileName
                            (NotifyFilters.LastWrite
                             ||| NotifyFilters.FileName
                             ||| NotifyFilters.CreationTime
                             ||| NotifyFilters.Size)
                            enqueue

        let dispose () =
            disposeWatcher stateWatcher
            disposeWatcher udcWatcher
            disposeWatcher devWatcher
            disposeWatcher eventWatcher
            disposeWatcher capsWatcher

        { Channel = channel
          RefreshStateWatcher = refreshStateWatcher
          Dispose = dispose }
