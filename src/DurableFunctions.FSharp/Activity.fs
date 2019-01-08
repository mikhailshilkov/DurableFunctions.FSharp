namespace DurableFunctions.FSharp

open Microsoft.Azure.WebJobs
open System.Threading.Tasks

/// Strongly-typed activity representation to enable type-checked orchestrator definitions.
type Activity<'a, 'b> = {
    /// Activity name, has to match the name in FunctionName attribute.
    name: string
    /// Run the activity (should only be called from the orchestrator body).
    run: 'a -> Task<'b>
}

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

    /// Call all specified tasks in parallel and combine the results together. To be used
    /// for fan-out / fan-in pattern of parallel execution.
    let all (tasks: OrchestratorBuilder.ContextTask<'a> seq) (c: DurableOrchestrationContext) = 
        let bla = tasks |> Seq.map (fun x -> x c)
        let whenAll = Task.WhenAll bla
        whenAll.ContinueWith(fun (xs: Task<'a []>) -> xs.Result |> List.ofArray)
    
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
        
    
