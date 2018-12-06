namespace DurableFunctions.FSharp

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
end