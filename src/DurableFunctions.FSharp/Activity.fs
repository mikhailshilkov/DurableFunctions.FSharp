namespace DurableFunctions.FSharp

open Microsoft.Azure.WebJobs
open System
open System.Threading.Tasks

/// Strongly-typed activity representation to enable type-checked orchestrator definitions.
type Activity<'a, 'b> = {
    /// Activity name, has to match the name in FunctionName attribute.
    name: string
    /// Run the activity (should only be called from the orchestrator body).
    run: 'a -> Task<'b>
}

/// Properties of exponential retry.
type ExponentialRetryPolicy = {
    FirstRetryInterval: TimeSpan
    BackoffCoefficient: float
    MaxNumberOfAttempts: int
}

/// Retry policy for calling activities.
type RetryPolicy =
    | ExponentialBackOff of ExponentialRetryPolicy

module Activity =
    /// Constructor of activity given its name and a synchronous function.
    let define (name: string) run = {
        name = name
        run = fun x -> run x |> Task.FromResult
    }

    /// Constructor of activity given its name and a function returning Async<'a>.
    let defineAsync (name: string) (f: 'a -> Async<'b>) = {
        name = name
        run = fun x -> f x |> Async.StartAsTask
    }

    /// Constructor of activity given its name and a function returning Task<'a>.
    let defineTask (name: string) run = {
        name = name
        run = run
    }    

    /// Runs the activity
    let run activity = activity.run

    /// Call an activity by name, passing an object as its input argument
    /// and specifying the type to expect for the activity output.
    let callByName<'a> (name: string) arg (c: DurableOrchestrationContext) =
        c.CallActivityAsync<'a> (name, arg)

    /// Call the activity with given input parameter and return its result.
    let call (activity: Activity<'a, 'b>) (arg: 'a) (c: DurableOrchestrationContext) =
        c.CallActivityAsync<'b> (activity.name, arg)

    let optionsBuilder = function
    | ExponentialBackOff e -> 
                let r = RetryOptions(firstRetryInterval = e.FirstRetryInterval,
                                     maxNumberOfAttempts = e.MaxNumberOfAttempts)
                r.BackoffCoefficient <- e.BackoffCoefficient
                r

    /// Call the activity with given input parameter and return its result. Apply retry
    /// policy in case of call failure(s).
    let callWithRetries (policy: RetryPolicy) (activity: Activity<'a, 'b>) (arg: 'a) (c: DurableOrchestrationContext) =
        c.CallActivityWithRetryAsync<'b> (activity.name, (optionsBuilder policy), arg)

    /// Call the activity by name passing an object as its input argument
    /// and specifying the type to expect for the activity output. Apply retry
    /// policy in case of call failure(s).
    let callByNameWithRetries<'a> (policy: RetryPolicy) (name:string) arg (c: DurableOrchestrationContext) =
        c.CallActivityWithRetryAsync<'a> (name, (optionsBuilder policy), arg)

    /// Call all specified tasks in parallel and combine the results together. To be used
    /// for fan-out / fan-in pattern of parallel execution.
    let all (tasks: OrchestratorBuilder.ContextTask<'a> seq) = orchestrator {
        let! result = fun c -> (tasks |> Seq.map (fun x -> x c) |> Task.WhenAll)
        return List.ofArray result
    }
    
    /// Call all specified tasks sequentially one after the other and combine the results together.
    let seq (tasks: OrchestratorBuilder.ContextTask<'a> list) = 
        let rec work acc (rem : OrchestratorBuilder.ContextTask<'a> list) =
            match rem with
            | [] -> fun _ -> Task.FromResult acc
            | d :: rest -> orchestrator {
                let! t = d
                return! work (acc @ [t]) rest
            }
        work [] tasks
        
    
