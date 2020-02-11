module samples.FanInFanOut

open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.DurableTask
open DurableFunctions.FSharp

let hardWork = 
    fun item -> async {
      do! Async.Sleep 1000
      return sprintf "Worked hard on %s!" item
    }
    |> Activity.defineAsync "HardWork"

let workflow = orchestrator {
    let! batch1 =
      ["Tokyo"; "Seattle"; "London"]
      |> List.map (Activity.call hardWork)
      |> Activity.all

    let! batch2 =
      ["Paris"; "New York"; "Boston"]
      |> List.map (Activity.call hardWork)
      |> Activity.all

    // returns "Worked hard on Tokyo!, Worked hard on Seattle!, ...
    return String.concat ", " (batch1 @ batch2)
  }

[<FunctionName("HardWork")>]
let HardWork([<ActivityTrigger>] name) = hardWork.run name

[<FunctionName("FanInFanOut")>]
let FanInFanOut ([<OrchestrationTrigger>] context: IDurableOrchestrationContext) =
    Orchestrator.run (workflow, context)