module samples.InputParameter

open Microsoft.Azure.WebJobs
open DurableFunctions.FSharp

open TypedSequence

let workflow input = orchestrator {
    let! hello1 = Activity.call sayHello (input + " Tokyo")
    let! hello2 = Activity.call sayHello (input + " Seattle")
    let! hello3 = Activity.call sayHello (input + " London")

    // given "Bla" returns ["Hello Bla Tokyo!", "Hello Bla Seattle!", "Hello Bla London!"]
    return [hello1; hello2; hello3]
}

[<FunctionName("WorkflowWithInputParameter")>]
let Run ([<OrchestrationTrigger>] context: DurableOrchestrationContext) = 
    Orchestrator.run (workflow, context)