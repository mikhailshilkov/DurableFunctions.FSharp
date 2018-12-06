namespace samples

open System.Net.Http
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.Http
open Microsoft.Azure.WebJobs.Host
open Microsoft.Extensions.Logging
open FSharp.Control.Tasks

module HttpStart = 

  [<FunctionName("HttpStart")>]
  let RunSync
     ([<HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orchestrators/{functionName}")>] req: HttpRequestMessage,
      [<OrchestrationClient>] starter: DurableOrchestrationClient,
      functionName: string,
      log: ILogger) =
    task {
      let param = req.RequestUri.ParseQueryString().["input"]
      let! instanceId = starter.StartNewAsync (functionName, param)

      log.LogInformation(sprintf "Started orchestration with ID = '{%s}'." instanceId)

      return starter.CreateCheckStatusResponse(req, instanceId)
    }

  [<FunctionName("HttpSyncStart")>]
  let RunAsync
     ([<HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orchestrators/{functionName}/wait")>] req: HttpRequestMessage,
      [<OrchestrationClient>] starter: DurableOrchestrationClient,
      functionName: string,
      log: ILogger) =
    task {
      let param = req.RequestUri.ParseQueryString().["input"]
      let! instanceId = starter.StartNewAsync (functionName, param)

      log.LogInformation(sprintf "Started orchestration with ID = '{%s}'." instanceId)

      return! starter.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId)
    }