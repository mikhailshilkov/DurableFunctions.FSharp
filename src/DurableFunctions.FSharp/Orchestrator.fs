namespace DurableFunctions.FSharp

open System
open System.Threading
open System.Threading.Tasks
open Microsoft.Azure.WebJobs
open OrchestratorBuilder

/// Eternal orchestrators should return the value of this type signaling
/// whether the orchestrator should continue ("as new") or quit (stop).
type EternalOrchestrationCommand<'a> = Stop | ContinueAsNew of 'a

type Orchestrator = class

    /// Runs a workflow which expects an input parameter by reading this parameter from 
    /// the orchestration context.
    static member run (workflow : ContextTask<'b>, context : DurableOrchestrationContextBase) : Task<'b> = 
        workflow context

    /// Runs a workflow which expects an input parameter by reading this parameter from 
    /// the orchestration context.
    static member run (workflow : 'a -> ContextTask<'b>, context : DurableOrchestrationContextBase) : Task<'b> = 
        let input = context.GetInput<'a> ()
        workflow input context

    /// Runs an "eternal" orchestrator: a series of workflow executions chained with
    /// [ContinueAsNew] calls. The orchestrator will keep running until Stop command is
    /// returned from one of the workflow iterations.
    /// This overload always passes [null] to [ContinueAsNew] calls.
    static member runEternal (workflow : ContextTask<EternalOrchestrationCommand<unit>>, context : DurableOrchestrationContextBase) : Task = 
        let task = workflow context
        task.ContinueWith (
            fun (t: Task<EternalOrchestrationCommand<unit>>) ->
                match t.Result with
                | ContinueAsNew () -> context.ContinueAsNew null
                | Stop -> ()
            )

    /// Runs an "eternal" orchestrator: a series of workflow executions chained with
    /// [ContinueAsNew] calls. The orchestrator will keep running until Stop command is
    /// returned from one of the workflow iterations.
    /// This overload always passes the returned value to [ContinueAsNew] calls.
    static member runEternal (workflow : 'a -> ContextTask<EternalOrchestrationCommand<'a>>, context : DurableOrchestrationContextBase) : Task = 
        let input = context.GetInput<'a> ()
        let task = workflow input context
        task.ContinueWith (
            fun (t: Task<EternalOrchestrationCommand<'a>>) ->
                match t.Result with
                | ContinueAsNew r -> context.ContinueAsNew r
                | Stop -> ()
            )
    
    /// Returns a fixed value as a orchestrator.
    static member ret value (_: DurableOrchestrationContextBase) =
        Task.FromResult value

    /// Delays orchestrator execution by the specified timespan.
    static member delay (timespan: TimeSpan) (context: DurableOrchestrationContextBase) =
        let deadline = context.CurrentUtcDateTime.Add timespan
        context.CreateTimer(deadline, CancellationToken.None)
    
    /// Wait for an external event. maxTimeToWait specifies the longest period to wait:
    /// the call will return an Error if timeout is reached.
    static member waitForEvent<'a> (maxTimeToWait: TimeSpan) (eventName: string) (context: DurableOrchestrationContextBase) =
        let deadline = context.CurrentUtcDateTime.Add maxTimeToWait
        let timer = context.CreateTimer(deadline, CancellationToken.None)
        let event = context.WaitForExternalEvent<'a> eventName
        Task.WhenAny(event, timer)
            .ContinueWith(
                fun (winner: Task) -> 
                    if winner = timer then Result.Error ""
                    else Result.Ok event.Result)
end