DurableFunctions.FSharp Library
-------------------------------

F#-friendly API layer around 
[Azure Functions Durable Extensions](https://github.com/Azure/azure-functions-durable-extension).

*Note: this is an early draft with the primary goal to gather feedback,
opinions, real-world scenarios and refine the API. Breaking changes
may be introduced at any time.*

Getting Started
---------------

Here is how you get started :

#### 1. Create a new .NET Core console application

With .NET Core 2.1 installed, run the following command to create a new console application:

```
dotnet new console -lang F#
```

#### 2. Modify `fsproj`

Edit the top section in `fsproj` to be:

``` xml
<PropertyGroup>
  <TargetFramework>netcoreapp2.1</TargetFramework>
  <AzureFunctionsVersion>v2</AzureFunctionsVersion>
</PropertyGroup>
```

See [the example](https://github.com/mikhailshilkov/DurableFunctions.FSharp/blob/master/samples/samples.fsproj#L3-L6).

#### 3. Install NuGet package

Install the `Microsoft.NET.Sdk.Functions` and `DurableFunctions.FSharp` NuGet packages:

```
dotnet add package Microsoft.NET.Sdk.Functions
dotnet add package DurableFunctions.FSharp
```

#### 4. Define an activity and an orchestrator

The following Hello World application can be used as a starting point:

``` fsharp
namespace MyDurableApp

open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.DurableTask
open DurableFunctions.FSharp

module TypedSequence =

  let sayHello = 
    Activity.define "SayHello" (sprintf "Hello %s!")

  let workflow = orchestrator {
    let! hello1 = Activity.call sayHello "Tokyo"
    let! hello2 = Activity.call sayHello "Seattle"
    let! hello3 = Activity.call sayHello "London"

    // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
    return [hello1; hello2; hello3]
  }

  [<FunctionName("SayHello")>]
  let SayHello([<ActivityTrigger>] name) = Activity.run sayHello

  [<FunctionName("TypedSequence")>]
  let Run ([<OrchestrationTrigger>] context: IDurableOrchestrationContext) =
    Orchestrator.run (workflow, context)
```

#### 5. Run

[Install Azure Functions Core Tools](https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local)
to run the app locally and deploy to the cloud, or use the tooling in Visual Studio or Visual Studio Code.

If you have any issue, you can also clone/fork
[the samples](https://github.com/mikhailshilkov/DurableFunctions.FSharp/tree/master/samples).

Basic Orchestrator
------------------

Durable Orchestrators are not allowed to use `async` computation expression due to the
requirement of being single-threaded. Orchestrators can be defined with `task` computation 
expression, as shown in the standard
[samples](https://github.com/Azure/azure-functions-durable-extension/blob/master/samples/fsharp/HelloSequence.fs#L12-#L19).

However, to enable a more F#-idiomatic style of orchestrator definitions, this library
defines a new computation expression called `orchestrator`.

Given a simple activity function:

``` fsharp
[<FunctionName("SayHello")>]
let SayHello([<ActivityTrigger>] name) = 
    sprintf "Hello %s!" name
```

An orchestrator can be defined as follows:

``` fsharp
let workflow = orchestrator {
    let! hello1 = Activity.callByName<string> "SayHello" "Tokyo"
    let! hello2 = Activity.callByName<string> "SayHello" "Seattle"
    let! hello3 = Activity.callByName<string> "SayHello" "London"

    // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
    return [hello1; hello2; hello3]
}
```

The result of the computation is a function of type `IDurableOrchestrationContext -> Task<'a>`
which can be invoked from the orchestrator Azure Function:

``` fsharp
[<FunctionName("HelloSequence")>]
let Run ([<OrchestrationTrigger>] context: IDurableOrchestrationContext) = 
    Orchestrator.run (workflow, context)
```

See [the full example](https://github.com/mikhailshilkov/DurableFunctions.FSharp/blob/master/samples/Hello.fs).

Typed Activity
--------------

In the example above, the orchestrator calls activities by name. It also specifies the
return type explicitly.

To gain more type safety, an explicit typed `Activity<'a, 'b>` may be defined:

``` fsharp
let sayHello = 
    Activity.define "SayTyped" (sprintf "Hello typed %s!")
```

`Activity.define` accepts the function name as the first parameter and the function `'a -> 'b`
as the second parameter. Azure Function still has to be defined separately:

``` fsharp
[<FunctionName("SayTyped")>]
let SayHello([<ActivityTrigger>] name) = Activity.run sayHello name
```

The orchestrator can now infer types from the activity type:

``` fsharp
let workflow = orchestrator {
    let! hello1 = Activity.call sayHello "Tokyo"
    let! hello2 = Activity.call sayHello "Seattle"
    let! hello3 = Activity.call sayHello "London"

    // returns ["Hello typed Tokyo!", "Hello typed Seattle!", "Hello typed London!"]
    return [hello1; hello2; hello3]
}
```

See [the full example](https://github.com/mikhailshilkov/DurableFunctions.FSharp/blob/master/samples/Typed.fs).

`Async` in activities
---------------------

While `Async<'a>` return type is not supported out of the box by Azure Functions, it can
be used internally. There is a helper `defineAsync` function to make such definition easier:

``` fsharp
let hardWork = 
    fun item -> async {
      do! Async.Sleep 1000
      return sprintf "Worked hard on %s!" item
    }
    |> Activity.defineAsync "HardWork"
```

See [the full example](https://github.com/mikhailshilkov/DurableFunctions.FSharp/blob/master/samples/FanOutFanIn.fs).

Orchestrator with an input parameter
------------------------------------

Orchestrators can accept an input parameter (1 at most). This can be defined as an argument of the workflow
definition function:

``` fsharp
let workflow input = orchestrator {
  // ...
}
```

An overload of `Orchestrator.run` will get the input from the context and pass it to the workflow.

Fan-out/fan-in
--------------

`Activity.all` helper function can be used to run multiple activities in parallel and
combine the results:

``` fsharp
let workflow = orchestrator {
    let! items =
      ["Tokyo"; "Seattle"; "London"]
      |> List.map (Activity.call hardWork)
      |> Activity.all

    return String.concat ", " items
}
```

See [the full example](https://github.com/mikhailshilkov/DurableFunctions.FSharp/blob/master/samples/FanOutFanIn.fs).

Delays
------

You can pause the orchestrator by calling `Orchestrator.delay` function:

``` fsharp
let sendAndPause email = orchestrator {
    do! Activity.call sendNewsletter email
    do! Orchestrator.delay (TimeSpan.FromHours 1.0)
}
```

Note that the durable timer is used to implement this delay, so the orchestrator function will actually stop the current
execution and will resume after the delay expires. See [Timers in Durable Functions](https://docs.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-timers).

Retry
-----

There is a built-in mechanism to retry failed activity calls. Invoke `Activity.callWithRetries` function with retry parameters:

``` fsharp
let workflow = orchestrator {
    let policy = ExponentialBackOff { MaxNumberOfAttempts = 5
                                      FirstRetryInterval = TimeSpan.FromSeconds 1.
                                      BackoffCoefficient = 2. }
    return! Activity.callWithRetries policy failUntil3 "Jam"
}
```

See [the full example](https://github.com/mikhailshilkov/DurableFunctions.FSharp/blob/master/samples/Retry.fs).

Waiting For External Events
---------------------------

Orchestrator functions have the ability to wait and listen for external events. This feature of Durable Functions is often useful for handling human interaction or other external triggers.

`Orchestrator.waitForEvent` accepts the time-out duration and returns a `Result<'a, string>`: an `Ok` result if an external event occured, and `Error` otherwise:

``` fsharp
let workflow = orchestrator {
    let maxWaitDuration = TimeSpan.FromHours 1.
    let! result = Orchestrator.waitForEvent maxWaitDuration "Ack"
    return 
        match result with
        | Ok Ack -> true
        | _ -> false
}
```

See [the full example](https://github.com/mikhailshilkov/DurableFunctions.FSharp/blob/master/samples/WaitForEvent.fs).

"Eternal" Orchestrators
-----------------------

Normal orchestrators persist full history of past events, which means there's a practical limit of how many event they can store without slowing down too much.

[Eternal orchestrators](https://docs.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-eternal-orchestrations) solve this problem: the orchestrator can restart itself from a clean sheet. Each iteration will start the history from scratch. However, a piece of data can be forwarded from the previous iteration to the next one.

`Orchestrator.runEternal` accepts a workflow which should return a value of a special type `EternalOrchestrationCommand`, which is a discriminated union. `ContinueAsNew of 'a` case can be used to restart the workflow and pass the value, while `Stop` case will end the orchestration (they don't have to be really eternal).

``` fsharp
let workflow = orchestrator {
    let! s = Activity.call printTime DateTime.Now
    do! Orchestrator.delay (TimeSpan.FromSeconds 5.0)
    return ContinueAsNew ()
}

[<FunctionName("Eternal")>]
let Run ([<OrchestrationTrigger>] context: IDurableOrchestrationContext) =
    Orchestrator.runEternal (workflow, context)
```

See [the full example](https://github.com/mikhailshilkov/DurableFunctions.FSharp/blob/master/samples/Eternal.fs).

Contributions
-------------

Everybody is welcome to contribute! Please try the library and create an issue with
your ideas, problems, suggestions and target scenarios.