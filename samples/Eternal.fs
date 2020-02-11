module samples.Eternal

open System
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.DurableTask
open DurableFunctions.FSharp

let printTime = 
    fun (d: DateTime) -> sprintf "Printing at %s!" (d.ToShortTimeString())
    |> Activity.define "PrintTime"

let workflow = orchestrator {
    let! (s: string) = Activity.call printTime DateTime.Now
    do! Orchestrator.delay (TimeSpan.FromSeconds 5.0)
    return if s.Contains "00" then Stop else ContinueAsNew ()
}

let workflowWithParam delay = orchestrator {
    let! (s: string) = Activity.call printTime DateTime.Now
    do! Orchestrator.delay (TimeSpan.FromSeconds delay)
    return if s.Contains "00" then Stop else ContinueAsNew (delay + 1.)
}

[<FunctionName("PrintTime")>]
let PrintTime([<ActivityTrigger>] name) = printTime.run name

[<FunctionName("Eternal")>]
let Run ([<OrchestrationTrigger>] context: IDurableOrchestrationContext) =
    Orchestrator.runEternal (workflow, context)

[<FunctionName("EternalWithParam")>]
let RunWithParam ([<OrchestrationTrigger>] context: IDurableOrchestrationContext) =
    Orchestrator.runEternal (workflowWithParam, context)