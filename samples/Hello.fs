module samples.HelloSequence

open Microsoft.Azure.WebJobs
open DurableFunctions.FSharp

[<FunctionName("SayHello")>]
let SayHello([<ActivityTrigger>] name) = 
    sprintf "Hello %s!" name

[<FunctionName("HelloSequence")>]
let Run ([<OrchestrationTrigger>] context: DurableOrchestrationContext) = 
    context |>    
    orchestrator {
      let! hello1 = Activity.callByName<string> "SayHello" "Tokyo"
      let! hello2 = Activity.callByName<string> "SayHello" "Seattle"
      let! hello3 = Activity.callByName<string> "SayHello" "London"

      // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
      return [hello1; hello2; hello3]
    }