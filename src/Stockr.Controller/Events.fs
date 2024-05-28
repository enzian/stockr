module events

open api
open System

module api =
    let Version = "v1"
    let Group = "events.stockr.io"
    let Kind = "event"

type ObjectReference =
    { kind: string
      name: string
      apiVersion: string
      group: string
      resourceVersion: string }

type EventSpec =
    { message: string
      eventTime: int64
      reason: string
      reportingComponent: string
      reportingInstance: string
      kind: string
      regarding: ObjectReference option
      related: ObjectReference option }

type EventSpecManifest =
    { spec: EventSpec
      metadata: Metadata }

    interface Manifest with
        member this.metadata = this.metadata

type EventFactory = unit -> EventSpecManifest
type EventTransformator = EventSpecManifest -> EventSpecManifest

let publishEvent (client: ManifestApi<EventSpecManifest>) event = client.Put event |> ignore

let emptyEvent =
    { spec =
        { message = ""
          eventTime = 0L
          reason = ""
          reportingComponent = ""
          reportingInstance = ""
          kind = ""
          regarding = None
          related = None }
      metadata =
        { name = Guid.NewGuid().ToString()
          labels = None
          ``namespace`` = None
          annotations = None
          revision = None } }

let withCurrentTime event =
    let now = DateTimeOffset.UtcNow
    { event with spec.eventTime = now.UtcTicks }

let withComponent ``component`` event =
    { event with spec.reportingComponent = ``component`` }

let withReportingInstance instance event =
    { event with spec.reportingInstance = instance }

let withMessage message event =
    { event with spec.message = message }

let withReason message event =
    { event with spec.reason = message }

let withKind kind event =
    { event with spec.kind = kind }

let regarding objRef event =
    { event with spec.regarding = Some objRef }

let relatedTo objRef event =
    { event with spec.related = Some objRef }

type IEventLogger =
    abstract member factory: EventFactory
    abstract member client: ManifestApi<EventSpecManifest>
    abstract member Error: string -> string -> unit
    abstract member Info: string -> string -> unit
    abstract member Debug: string -> string -> unit
    abstract member Warn: string -> string -> unit

let newEventLogger
    (client: ManifestApi<EventSpecManifest>)
    (eventFac: unit -> EventSpecManifest) =
    { 
        new IEventLogger with
            member _.factory = eventFac
            member _.client = client
            member _.Error reason message = 
                (eventFac ()) 
                |> withKind "Error"
                |> withReason reason
                |> withMessage message
                |> publishEvent client
            member _.Debug reason message = 
                (eventFac ()) 
                |> withKind "Debug"
                |> withReason reason
                |> withMessage message
                |> publishEvent client
            member _.Info reason message = 
                (eventFac ()) 
                |> withKind "Info"
                |> withReason reason
                |> withMessage message
                |> publishEvent client
            member _.Warn reason message = 
                (eventFac ()) 
                |> withKind "Warning"
                |> withReason reason
                |> withMessage message
                |> publishEvent client
    }

let customize (transformer: EventTransformator) (logger: IEventLogger)  = 
    let eventFact () =
        (logger.factory ())
        |> transformer
    newEventLogger logger.client eventFact
