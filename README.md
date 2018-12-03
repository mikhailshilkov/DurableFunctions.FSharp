DurableFunctions.FSharp Library
-------------------------------

F#-friendly API layer around 
[Azure Functions Durable Extensions](https://github.com/Azure/azure-functions-durable-extension).

** Note: this is an early draft with the main goal to gather feedback,
opinions, real-world scenarios and refine the API. Breaking changes
can be introduced at any time.**

Usage
-----

No NuGet exists yet. It will be created as soon as we have the basic API
version. To give it a try now, please clone/fork and start with Samples project.

Basic Orchestrator
------------------

Durable Orchestrators can not be created with `async` computation expression due to the
requirement of being single-threaded.

Orchestrators can be defined with `task` computation expression, as shown in the standard
[samples](https://github.com/Azure/azure-functions-durable-extension/blob/master/samples/fsharp/HelloSequence.fs#L12-#L19).

However, to enable more F#-idiomatic style of orchestrator definitions, this library
defines a new computation expression called `orchestrator`.

Given a simple activity function:

``` fsharp
[<FunctionName("SayHello")>]
let SayHello([<ActivityTrigger>] name) = 
    sprintf "Hello %s!" name
```

An orchestrator can be defined as following:

``` fsharp
let workflow = orchestrator {
    let! hello1 = Activity.callByName<string> "SayHello" "Tokyo"
    let! hello2 = Activity.callByName<string> "SayHello" "Seattle"
    let! hello3 = Activity.callByName<string> "SayHello" "London"

    // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
    return [hello1; hello2; hello3]
}
```

The result of the computation is a function of type `DurableOrchestrationContext -> Task<'a>`
which can be invoked from the orchestrator Azure Function:

``` fsharp
[<FunctionName("HelloSequence")>]
let Run ([<OrchestrationTrigger>] context: DurableOrchestrationContext) = 
    workflow context 
```

See [the full example](https://github.com/mikhailshilkov/DurableFunctions.FSharpblob/master/samples/Hello.fs).

Typed Activity
--------------

In the example above, the orchestrator calls activities by name. It also specifies the
return type explicitly.

To gain more type safety, an explicit typed `Activity` may be defined:

``` fsharp
let sayHello = 
    Activity.define "SayTyped" (sprintf "Hello typed %s!")
```

`Activity.define` accepts the function name as the first parameter and the function `'a -> 'b`
as the second parameter. Azure Function still has to be defined separately:

``` fsharp
[<FunctionName("SayTyped")>]
let SayHello([<ActivityTrigger>] name) = sayHello.run name
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

See [the full example](https://github.com/mikhailshilkov/DurableFunctions.FSharpblob/master/samples/Typed.fs).

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

See [the full example](https://github.com/mikhailshilkov/DurableFunctions.FSharpblob/master/samples/FanOutFanIn.fs).