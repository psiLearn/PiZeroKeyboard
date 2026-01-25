namespace SenderApp

open System

type SendingProgress =
    { BytesSent: int
      TotalBytes: int }

type SendStatus =
    | Idle
    | Sending of SendingProgress
    | Success of int
    | Failure of string

type ConnectedInfo =
    { LastActivity: DateTime
      LatencyMs: int option }

type DisconnectedInfo =
    { Reason: string
      LastAttempt: DateTime option
      RetryCount: int
      Suggestion: string }

type ConnectionStatus =
    | Connected of ConnectedInfo
    | NotConnected of DisconnectedInfo

type UsbStatus =
    { Text: string
      CssClass: string }

type CapsLockStatus =
    { Text: string
      CssClass: string }

type KeyboardVisibility =
    | Visible
    | Hidden

type RetryState =
    { IsRetrying: bool
      RetryCount: int
      LastAttemptTime: DateTime option
      NextRetryTime: DateTime option
      RetryIntervalSeconds: int }

type IndexViewModel =
    { Status: SendStatus
      Text: string
      UsbStatus: UsbStatus
      CapsLock: CapsLockStatus
      IsMobile: bool
      Layout: string
      ConnectionStatus: ConnectionStatus
      KeyboardVisibility: KeyboardVisibility
      SendingControls: SendingControlsService.SendingControls
      RetryState: RetryState
      AutoRetryEnabled: bool
      SendStartTime: DateTime option }

[<CLIMutable>]
type SendRequest =
    { Text: string
      Layout: string
      PrivateSend: bool }

type SenderSettings =
    { TargetIp: string
      TargetPort: int }
