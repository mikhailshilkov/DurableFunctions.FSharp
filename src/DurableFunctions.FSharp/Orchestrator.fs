namespace DurableFunctions.FSharp

open System
open System.Threading
open System.Threading.Tasks
open Microsoft.Azure.WebJobs
open OrchestratorBuilder

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
    
    /// Returns a fixed value as a orchestrator.
    static member ret value (_: DurableOrchestrationContext) =
        Task.FromResult value

    /// Delays orchestrator execution by the specified timespan.
    static member delay (timespan: TimeSpan) (context: DurableOrchestrationContext) =
        let deadline = context.CurrentUtcDateTime.Add timespan
        context.CreateTimer(deadline, CancellationToken.None)
    
end