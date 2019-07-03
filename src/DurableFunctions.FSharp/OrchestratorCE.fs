namespace DurableFunctions.FSharp

open System
open System.Threading.Tasks
open System.Runtime.CompilerServices
open Microsoft.Azure.WebJobs

module OrchestratorBuilder =

    type ContextTask = DurableOrchestrationContextBase -> Task
    type ContextTask<'a> = DurableOrchestrationContextBase -> Task<'a>
  
    /// Represents the state of a computation:
    /// either awaiting something with a continuation,
    /// or completed with a return value.
    type Step<'a> =
        | Await of ICriticalNotifyCompletion * (unit -> Step<'a>)
        | Return of 'a
        /// We model tail calls explicitly, but still can't run them without O(n) memory usage.
        | ReturnFrom of ContextTask<'a>
    /// Implements the machinery of running a `Step<'m, 'm>` as a task returning a continuation task.
    and StepStateMachine<'a>(firstStep, c: DurableOrchestrationContextBase) as this =
        let methodBuilder = AsyncTaskMethodBuilder<'a Task>()
        /// The continuation we left off awaiting on our last MoveNext().
        let mutable continuation = fun () -> firstStep
        /// Returns next pending awaitable or null if exiting (including tail call).
        let nextAwaitable() =
            try
                match continuation() with
                | Return r ->
                    methodBuilder.SetResult(Task.FromResult(r))
                    null
                | ReturnFrom t ->
                    methodBuilder.SetResult(t c)
                    null
                | Await (await, next) ->
                    continuation <- next
                    await
            with
            | exn ->
                methodBuilder.SetException(exn)
                null
        let mutable self = this

        /// Start execution as a `Task<Task<'a>>`.
        member __.Run() =
            methodBuilder.Start(&self)
            methodBuilder.Task

        interface IAsyncStateMachine with
            /// Proceed to one of three states: result, failure, or awaiting.
            /// If awaiting, MoveNext() will be called again when the awaitable completes.
            member __.MoveNext() =
                let mutable await = nextAwaitable()
                if not (isNull await) then
                    // Tell the builder to call us again when this thing is done.
                    methodBuilder.AwaitUnsafeOnCompleted(&await, &self)
            member __.SetStateMachine(_) = () // Doesn't really apply since we're a reference type.

    let unwrapException (agg : AggregateException) =
        let inners = agg.InnerExceptions
        if inners.Count = 1 then inners.[0]
        else agg :> Exception

    /// Used to represent no-ops like the implicit empty "else" branch of an "if" expression.
    let zero = Return ()

    /// Used to return a value.
    let inline ret (x : 'a) = Return x

    type Binder<'out> =
        // We put the output generic parameter up here at the class level, so it doesn't get subject to
        // inline rules. If we put it all in the inline function, then the compiler gets confused at the
        // below and demands that the whole function either is limited to working with (x : obj), or must
        // be inline itself.
        //
        // let yieldThenReturn (x : 'a) =
        //     task {
        //         do! Task.Yield()
        //         return x
        //     }

        static member inline GenericAwait< ^abl, ^awt, ^inp
                                            when ^abl : (member GetAwaiter : unit -> ^awt)
                                            and ^awt :> ICriticalNotifyCompletion
                                            and ^awt : (member get_IsCompleted : unit -> bool)
                                            and ^awt : (member GetResult : unit -> ^inp) >
            (abl : ^abl, continuation : ^inp -> 'out Step) : 'out Step =
                let awt = (^abl : (member GetAwaiter : unit -> ^awt)(abl)) // get an awaiter from the awaitable
                if (^awt : (member get_IsCompleted : unit -> bool)(awt)) then // shortcut to continue immediately
                    continuation (^awt : (member GetResult : unit -> ^inp)(awt))
                else
                    Await (awt, fun () -> continuation (^awt : (member GetResult : unit -> ^inp)(awt)))

    /// Special case of the above for `Task<'a>`, for the context-insensitive builder.
    /// Have to write this out by hand to avoid confusing the compiler thinking our built-in bind method
    /// defined on the builder has fancy generic constraints on inp and out parameters.
    let inline bindTask (task : Task<'a>) (continuation : 'a -> Step<'b>) =
        let awt = task.GetAwaiter()
        if awt.IsCompleted then // Proceed to the next step based on the result we already have.
            continuation(awt.GetResult())
        else // Await and continue later when a result is available.
            Await (awt, (fun () -> continuation(awt.GetResult())))

    let inline bindTaskUnit (task : Task) (continuation : unit -> Step<'b>) =
        let awt = task.GetAwaiter()
        if awt.IsCompleted then // Proceed to the next step based on the result we already have.
            continuation(awt.GetResult())
        else // Await and continue later when a result is available.
            Await (awt, (fun () -> continuation(awt.GetResult())))

    /// Chains together a step with its following step.
    /// Note that this requires that the first step has no result.
    /// This prevents constructs like `task { return 1; return 2; }`.
    let rec combine (step : Step<unit>) (continuation : unit -> Step<'b>) (c: DurableOrchestrationContextBase) =
        match step with
        | Return _ -> continuation ()
        | ReturnFrom t ->
            Await ((t c).GetAwaiter(), continuation)
        | Await (awaitable, next) ->
            Await (awaitable, fun () -> combine (next()) continuation c)

    /// Runs a step as a task -- with a short-circuit for immediately completed steps.
    let run (firstStep : unit -> Step<'a>) (c: DurableOrchestrationContextBase) =
        try
            match firstStep() with
            | Return x -> Task.FromResult(x)
            | ReturnFrom t -> t c
            | Await _ as step -> StepStateMachine<'a>(step, c).Run().Unwrap() // sadly can't do tail recursion
        // Any exceptions should go on the task, rather than being thrown from this call.
        // This matches C# behavior where you won't see an exception until awaiting the task,
        // even if it failed before reaching the first "await".
        with
        | exn ->
            let src = new TaskCompletionSource<_>()
            src.SetException(exn)
            src.Task

    let inline bindContextTask (task : ContextTask<'a>) (continuation : 'a -> Step<'b>) =
        ReturnFrom(
            fun c -> 
                let a = bindTask (task c) continuation
                run (fun () -> a) c)

    let inline bindContextTaskUnit (task : ContextTask) (continuation : unit -> Step<'b>) =
        ReturnFrom(
            fun c -> 
                let a = bindTaskUnit (task c) continuation
                run (fun () -> a) c)

    /// Wraps a step in a try/with. This catches exceptions both in the evaluation of the function
    /// to retrieve the step, and in the continuation of the step (if any).
    let rec tryWith(step : unit -> Step<'a>) (catch : exn -> Step<'a>) =
        try
            match step() with
            | Return _ as i -> i
            | ReturnFrom task ->
                let wrapper ctx =
                    let awaitable = (task ctx).GetAwaiter()
                    let step = 
                        Await(awaitable, fun () ->
                            try
                                awaitable.GetResult() |> Return
                            with
                            | exn -> catch exn)
                    StepStateMachine<'a>(step, ctx).Run().Unwrap()
                ReturnFrom wrapper
            | Await (awaitable, next) -> Await (awaitable, fun () -> tryWith next catch)
        with
        | exn -> catch exn

    /// Wraps a step in a try/finally. This catches exceptions both in the evaluation of the function
    /// to retrieve the step, and in the continuation of the step (if any).
    let rec tryFinally (step : unit -> Step<'a>) fin =
        let step =
            try step()
            // Important point: we use a try/with, not a try/finally, to implement tryFinally.
            // The reason for this is that if we're just building a continuation, we definitely *shouldn't*
            // execute the `fin()` part yet -- the actual execution of the asynchronous code hasn't completed!
            with
            | _ ->
                fin()
                reraise()
        match step with
        | Return _ as i ->
            fin()
            i
        | ReturnFrom task ->
            let wrapper ctx =
                let awaitable = (task ctx).GetAwaiter()
                let step = 
                    Await(awaitable, fun () ->
                        let result =
                            try
                                awaitable.GetResult() |> Return
                            with
                            | _ ->
                                fin()
                                reraise()
                        fin() // if we got here we haven't run fin(), because we would've reraised after doing so
                        result)
                StepStateMachine<'a>(step, ctx).Run().Unwrap()
            ReturnFrom wrapper

        | Await (awaitable, next) ->
            Await (awaitable, fun () -> tryFinally next fin)


    /// Builds a `System.Threading.Tasks.Task<'a>` similarly to a C# async/await method, but with
    /// all awaited tasks automatically configured *not* to resume on the captured context.
    /// This is often preferable when writing library code that is not context-aware, but undesirable when writing
    /// e.g. code that must interact with user interface controls on the same thread as its caller.
    type ContextInsensitiveOrchestratorBuilder() =
        // These methods are consistent between the two builders.
        // Unfortunately, inline members do not work with inheritance.
        member inline __.Delay(f : unit -> Step<_>) = f
        member inline __.Run(f : unit -> Step<'m>) = run f
        member inline __.Zero() = zero
        member inline __.Return(x) = ret x
        member inline __.ReturnFrom(task : ContextTask<'a>) = ReturnFrom task
        member inline __.Combine(step : Step<unit>, continuation) = combine step continuation
        member inline __.TryWith(task : unit -> Step<'a>, handler : exn -> Step<'a>) = tryWith task handler
        member inline __.TryFinally(task : unit -> Step<'a>, fin : unit -> unit) = tryFinally task fin
        member inline __.Using(disp : #IDisposable, body : #IDisposable -> _ Step) = using disp body

        // We have to have a dedicated overload for Task<'a> so the compiler doesn't get confused.
        // Everything else can use bindGenericAwaitable via an extension member (defined later).
        member inline __.Bind(task : ContextTask<'a>, continuation : 'a -> 'b Step) : 'b Step =
            bindContextTask task continuation
        member inline __.Bind(task : ContextTask, continuation : unit -> 'b Step) : 'b Step =
            bindContextTaskUnit task continuation

[<AutoOpen>]
module Builders =
    /// Builds an orchestrator computation expression that can combine C#-style tasks, presumably
    /// by calling into activity functions.
    let orchestrator = OrchestratorBuilder.ContextInsensitiveOrchestratorBuilder()

    // These are fallbacks when the Bind and ReturnFrom on the builder object itself don't apply.
    // This is how we support binding arbitrary task-like types.
    type OrchestratorBuilder.ContextInsensitiveOrchestratorBuilder with
        member inline __.ReturnFrom(taskLike) =
            OrchestratorBuilder.Binder<_>.GenericAwait(taskLike, OrchestratorBuilder.ret)
        member inline __.Bind(taskLike, continuation : _ -> 'a OrchestratorBuilder.Step) : 'a OrchestratorBuilder.Step =
            OrchestratorBuilder.Binder<'a>.GenericAwait(taskLike, continuation)