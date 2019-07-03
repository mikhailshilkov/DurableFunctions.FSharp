module samples.Eternal

open System
open Microsoft.Azure.WebJobs
open DurableFunctions.FSharp

let printTime =     
    Activity.define "PrintTime" (fun (d: DateTime) -> sprintf "Printing at %s!" (d.ToShortTimeString()))

let workflow = orchestrator {
    let! (s:string) = Activity.call printTime DateTime.Now
    do! Orchestrator.delay (TimeSpan.FromSeconds 5.0)
    return if s.Contains "00" then Stop else ContinueAsNew ()
}

let workflowWithParam delay = orchestrator {
    let! (s:string) = Activity.call printTime DateTime.Now
    do! Orchestrator.delay (TimeSpan.FromSeconds delay)
    return if s.Contains "00" then Stop else ContinueAsNew (delay + 1.)
}

[<FunctionName("PrintTime")>]
let PrintTime([<ActivityTrigger>] name) = printTime.run name

[<FunctionName("Eternal")>]
let Run ([<OrchestrationTrigger>] context: DurableOrchestrationContext) =
    Orchestrator.runEternal (workflow, context)

[<FunctionName("EternalWithParam")>]
let RunWithParam ([<OrchestrationTrigger>] context: DurableOrchestrationContext) =
    Orchestrator.runEternal (workflowWithParam, context)