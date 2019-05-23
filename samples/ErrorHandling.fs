module samples.ErrorHandling

open Microsoft.Azure.WebJobs
open DurableFunctions.FSharp
open System

let failIfJam = 
    let work name =
        if name = "Jam" then failwith "No Jam"
        else sprintf "Hi %s" name
    Activity.define "FailIfJam" work

let tryWithFlow = orchestrator {
    try
        return! Activity.call failIfJam "Jam"
    with _ ->
        try
            return! Activity.call failIfJam "John"
        with
        | exn -> return exn.ToString()
}

let tryFinallyFlow name = orchestrator {
    try
        return! Activity.call failIfJam name
    finally
        printfn "*** Buy %s! THIS WILL ALWAYS BE EXECUTED ***" name
}

[<FunctionName("FailIfJam")>]
let Fail([<ActivityTrigger>] name) = failIfJam.run name

[<FunctionName("TryWithWorkflow")>]
let RunWith ([<OrchestrationTrigger>] context: DurableOrchestrationContext) =
    Orchestrator.run (tryWithFlow, context)

[<FunctionName("TryFinallyWorkflow")>]
let RunFinally ([<OrchestrationTrigger>] context: DurableOrchestrationContext) =
    Orchestrator.run (tryFinallyFlow, context)
