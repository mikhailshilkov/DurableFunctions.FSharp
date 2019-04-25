(*  
Example demonstrating how to pass a strongly typed parameter to an activity funciton, in this case 
a string parsed from the query string by HttpStart. 

NOTE: Because durable functions maintain their state in Azure Storage the parameters must be 
serializable by vanilla Json.Net serialization. Basic types classses and F# records are supported.
Some types such as F# tuples are not. If you use an unsuppored type your code may compile but at 
runtime the DurableFunctions library will indicate the given actiivyt function can't be found.
*)

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
