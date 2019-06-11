namespace DurableFunctions.FSharp

open System
open System.Threading
open System.Threading.Tasks
open Microsoft.Azure.WebJobs
open OrchestratorBuilder

type EternalOrchestrationCommand<'a> = Stop | ContinueAsNew of 'a

type Orchestrator = class

    /// Runs a workflow which expects an input parameter by reading this parameter from 
    /// the orchestration context.
    static member run (workflow : ContextTask<'b>, context : DurableOrchestrationContext) : Task<'b> = 
        workflow context

    /// Runs a workflow which expects an input parameter by reading this parameter from 
    /// the orchestration context.
    static member run (workflow : 'a -> ContextTask<'b>, context : DurableOrchestrationContext) : Task<'b> = 
        let input = context.GetInput<'a> ()
        workflow input context

    static member runEternal (workflow : ContextTask<EternalOrchestrationCommand<unit>>, context : DurableOrchestrationContext) : Task = 
        let task = workflow context
        task.ContinueWith (
            fun (t: Task<EternalOrchestrationCommand<unit>>) ->
                match t.Result with
                | ContinueAsNew () -> context.ContinueAsNew null
                | Stop -> ()
            )

    static member runEternal (workflow : 'a -> ContextTask<EternalOrchestrationCommand<'a>>, context : DurableOrchestrationContext) : Task = 
        let input = context.GetInput<'a> ()
        let task = workflow input context
        task.ContinueWith (
            fun (t: Task<EternalOrchestrationCommand<'a>>) ->
                match t.Result with
                | ContinueAsNew r -> context.ContinueAsNew r
                | Stop -> ()
            )
    
    /// Returns a fixed value as a orchestrator.
    static member ret value (_: DurableOrchestrationContext) =
        Task.FromResult value

    /// Delays orchestrator execution by the specified timespan.
    static member delay (timespan: TimeSpan) (context: DurableOrchestrationContext) =
        let deadline = context.CurrentUtcDateTime.Add timespan
        context.CreateTimer(deadline, CancellationToken.None)
    
    /// Wait for an external event. maxTimeToWait specifies the longest period to wait:
    /// the call will return an Error if timeout is reached.
    static member waitForEvent<'a> (maxTimeToWait: TimeSpan) (eventName: string) (context: DurableOrchestrationContext) =
        let deadline = context.CurrentUtcDateTime.Add maxTimeToWait
        let timer = context.CreateTimer(deadline, CancellationToken.None)
        let event = context.WaitForExternalEvent<'a> eventName
        Task.WhenAny(event, timer)
            .ContinueWith(
                fun (winner: Task) -> 
                    if winner = timer then Result.Error ""
                    else Result.Ok event.Result)
end