namespace SenderApp

module ConnectionRetryService =
    open System
    
    let defaultRetryState : RetryState =
        { IsRetrying = false
          RetryCount = 0
          LastAttemptTime = None
          NextRetryTime = None
          RetryIntervalSeconds = 5 }
    
    /// Start auto-retry process
    let startRetry (state: RetryState) : RetryState =
        let now = DateTime.UtcNow
        { state with
            IsRetrying = true
            RetryCount = state.RetryCount + 1
            LastAttemptTime = Some now
            NextRetryTime = Some (now.AddSeconds(float state.RetryIntervalSeconds)) }
    
    /// Stop auto-retry
    let stopRetry (state: RetryState) : RetryState =
        { state with
            IsRetrying = false
            NextRetryTime = None }
    
    /// Check if it's time to retry
    let shouldRetryNow (state: RetryState) : bool =
        match state.NextRetryTime with
        | Some nextTime when state.IsRetrying ->
            DateTime.UtcNow >= nextTime
        | _ -> false
    
    /// Calculate seconds until next retry
    let secondsUntilNextRetry (state: RetryState) : int option =
        match state.NextRetryTime with
        | Some nextTime when state.IsRetrying ->
            let remaining = (nextTime - DateTime.UtcNow).TotalSeconds
            if remaining > 0.0 then Some (int (remaining + 0.5))
            else Some 0
        | _ -> None
    
    /// Format retry status for display
    let formatRetryStatus (state: RetryState) : string =
        if state.IsRetrying then
            match secondsUntilNextRetry state with
            | Some seconds when seconds > 0 ->
                sprintf "Retrying in %d seconds… (attempt %d)" seconds state.RetryCount
            | _ ->
                sprintf "Attempting to reconnect… (attempt %d)" state.RetryCount
        else
            ""
