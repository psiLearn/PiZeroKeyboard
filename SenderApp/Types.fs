namespace SenderApp

type SendStatus =
    | Idle
    | Success of int
    | Failure of string

type UsbStatus =
    { Text: string
      CssClass: string }

type IndexViewModel =
    { Status: SendStatus
      Text: string
      UsbStatus: UsbStatus
      IsMobile: bool
      Layout: string }

[<CLIMutable>]
type SendRequest =
    { Text: string
      Layout: string }

type SenderSettings =
    { TargetIp: string
      TargetPort: int }
