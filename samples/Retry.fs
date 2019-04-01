module samples.Retry

open Microsoft.Azure.WebJobs
open DurableFunctions.FSharp
open System

let failUntil3 = 
    let mutable counter = 0
    let work (s: string) =
        counter <- counter + 1
        if counter < 3 then failwith "Boom"
        else sprintf "Tried %s %i times" s counter
    Activity.define "FailUntil3" work

let workflow = orchestrator {
    let policy = ExponentialBackOff { MaxNumberOfAttempts = 5
                                      FirstRetryInterval = TimeSpan.FromSeconds 1.
                                      BackoffCoefficient = 2. }
    let! msg = Activity.callWithRetries policy failUntil3 "Jam"
    return msg
}

[<FunctionName("FailUntil3")>]
let FailUntil3([<ActivityTrigger>] name) = failUntil3.run name

[<FunctionName("RetryWorkflow")>]
let Run ([<OrchestrationTrigger>] context: DurableOrchestrationContext) =
    Orchestrator.run (workflow, context)