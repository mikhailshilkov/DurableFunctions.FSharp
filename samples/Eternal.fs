module samples.Eternal

open System
open Microsoft.Azure.WebJobs
open DurableFunctions.FSharp

let printTime = 
    fun (d: DateTime) -> sprintf "Printing at %s!" (d.ToShortTimeString())
    |> Activity.define "PrintTime"

let workflow = orchestrator {
    let! s = Activity.call printTime DateTime.Now
    do! Orchestrator.delay (TimeSpan.FromSeconds 5.0)
    return if s.Contains "00" then Stop else ContinueAsNew ()
}

[<FunctionName("PrintTime")>]
let PrintTime([<ActivityTrigger>] name) = printTime.run name

[<FunctionName("Eternal")>]
let Run ([<OrchestrationTrigger>] context: DurableOrchestrationContext) =
    Orchestrator.runEternal (workflow, context)