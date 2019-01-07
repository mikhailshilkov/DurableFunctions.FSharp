module samples.TypedSequence

open Microsoft.Azure.WebJobs
open DurableFunctions.FSharp

let sayHello = 
    Activity.define "SayTyped" (sprintf "Hello typed %s!")

let workflow = orchestrator {
    let! hello1 = Activity.call sayHello "Tokyo"
    let! hello2 = Activity.call sayHello "Seattle"
    let! hello3 = Activity.call sayHello "London"

    // returns ["Hello typed Tokyo!", "Hello typed Seattle!", "Hello typed London!"]
    return [hello1; hello2; hello3]
}

[<FunctionName("SayTyped")>]
let SayHello([<ActivityTrigger>] name) = sayHello.run name

[<FunctionName("TypedSequence")>]
let Run ([<OrchestrationTrigger>] context: DurableOrchestrationContext) =
    Orchestrator.run (workflow, context)