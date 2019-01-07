module samples.FanInFanOut

open Microsoft.Azure.WebJobs
open DurableFunctions.FSharp

let hardWork = 
    fun item -> async {
      do! Async.Sleep 1000
      return sprintf "Worked hard on %s!" item
    }
    |> Activity.defineAsync "HardWork"

let workflow = orchestrator {
    let! items =
      ["Tokyo"; "Seattle"; "London"]
      |> List.map (Activity.call hardWork)
      |> Activity.all

    // returns "Worked hard on Tokyo!, Worked hard on Seattle!, Worked hard on London!"
    return String.concat ", " items
  }

[<FunctionName("HardWork")>]
let HardWork([<ActivityTrigger>] name) = hardWork.run name

[<FunctionName("FanInFanOut")>]
let FanInFanOut ([<OrchestrationTrigger>] context: DurableOrchestrationContext) =
    Orchestrator.run (workflow, context)