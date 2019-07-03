module samples.unittest

open Microsoft.Azure.WebJobs
open DurableFunctions.FSharp
open System

[<FunctionName("SayHelloTest")>]
let SayHello([<ActivityTrigger>] name) = 
    sprintf "Hello %s!" name

[<FunctionName("HelloSequenceTest")>]
let RunWorkflow ([<OrchestrationTrigger>] context: DurableOrchestrationContextBase) = 
    context |>    
    orchestrator {
      let! hello1 = Activity.callByName<string> "SayHello" "Tokyo"
      let! hello2 = Activity.callByName<string> "SayHello" "Seattle"
      let! hello3 = Activity.callByName<string> "SayHello" "London"

      // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
      return [hello1; hello2; hello3]
    }

module WorkflowTests =

  open Moq
  open Expecto
  
  
  [<Tests>]
  let tests = 
          testList "orchestration tests" [
              testAsync "Orchestration test no records" {
                   
                  let mockContext = Mock<DurableOrchestrationContextBase>()
                  mockContext.Setup(fun c -> c.CallActivityAsync<string>("SayHello","Tokyo")).ReturnsAsync("Hello Tokyo") |> ignore
                  mockContext.Setup(fun c -> c.CallActivityAsync<string>("SayHello","Seattle")).ReturnsAsync("Hello Seattle") |> ignore
                  mockContext.Setup(fun c -> c.CallActivityAsync<string>("SayHello","London")).ReturnsAsync("Hello London") |> ignore
                  let! results = RunWorkflow mockContext.Object |> Async.AwaitTask

                  Expect.hasLength results 3 "should be 3 results"
                  Expect.equal results.[0] "Hello Tokyo!" "Should be Hello Tokyo!"
                  Expect.equal results.[1] "Hello Seattle!" "Should be Hello Seattle!"
                  Expect.equal results.[2] "Hello London!" "Should be Hello London!"
              }
          ]