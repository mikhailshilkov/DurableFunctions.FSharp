module samples.ErrorHandling

open Microsoft.Azure.WebJobs
open DurableFunctions.FSharp
open System

let failIfJam = 
    let work name =
        if name = "Jam" then failwith "No Jam"
        else sprintf "Hi %s" name
    Activity.define "FailIfJam" work

let workflow = orchestrator {
    try
        return! Activity.call failIfJam "Jam"
    with _ ->
        try
            return! Activity.call failIfJam "John"
        with
        | exn -> return exn.ToString()
}

[<FunctionName("FailIfJam")>]
let Fail([<ActivityTrigger>] name) = failIfJam.run name

[<FunctionName("ErrorHandlingWorkflow")>]
let Run ([<OrchestrationTrigger>] context: DurableOrchestrationContext) =
    Orchestrator.run (workflow, context)
