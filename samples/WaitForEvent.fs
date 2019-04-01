module samples.WaitForEvent

open Microsoft.Azure.WebJobs
open DurableFunctions.FSharp
open System

type Event = Ack | Nack

let workflow = orchestrator {
    let maxWaitDuration = TimeSpan.FromHours 1.
    let! result = Orchestrator.waitForEvent maxWaitDuration "Ack"
    return 
        match result with
        | Ok Ack -> true
        | _ -> false
}

[<FunctionName("WaitingWorkflow")>]
let Run ([<OrchestrationTrigger>] context: DurableOrchestrationContext) =
    Orchestrator.run (workflow, context)