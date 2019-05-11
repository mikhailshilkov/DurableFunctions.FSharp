(*  
Example demonstrating automated retry with exponential backoff. For the purpose of selecting appropriate 
parameters the list of delays as well as the maximum delay can be calculated with: 

open System

let MaxNumberOfAttempts = 5
let FirstRetryInterval = TimeSpan.FromSeconds 1.
let BackoffCoefficient = 2.

let nthDelay n = FirstRetryInterval.TotalMilliseconds * (Math.Pow(BackoffCoefficient, float n))
let allDelays = seq { for n in 0 .. (MaxNumberOfAttempts - 1) do yield nthDelay n } |> Seq.toList
let totalDelay = allDelays |> List.sum


See: 
https://github.com/Azure/durabletask/blob/61a2dc2f94cfa0aa2aae6ceb080717bad8a616b8/src/DurableTask.Core/RetryInterceptor.cs#L81
*)

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

    // In this case the exeption will propogate out of the workflow causing the 
    // orchestration to fail.
    //let! msg = Activity.callWithRetries policy failUntil3 "Jam"
    //return msg

     //Catch any exceptions in the activity function. Without this the exception
     //will propogate causing the orchestrator to fail.
    try 
        let! msg = Activity.callWithRetries policy failUntil3 "Jam"
        return msg
    with ex -> 
        return "error"
}

[<FunctionName("FailUntil3")>]
let FailUntil3([<ActivityTrigger>] name) = failUntil3.run name

[<FunctionName("RetryWorkflow")>]
let Run ([<OrchestrationTrigger>] context: DurableOrchestrationContext) =
    Orchestrator.run (workflow, context)
