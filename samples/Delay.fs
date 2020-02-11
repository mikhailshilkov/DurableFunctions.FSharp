module samples.Delay

open System
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.DurableTask
open DurableFunctions.FSharp

let sendNewsletter =     
    let impl (email: string) =
        Console.WriteLine (sprintf "Fake newsletter sent to %s" email)
        true
    Activity.define "SendNewsletter" impl

let newsletter = orchestrator {

    let pauseDuration = TimeSpan.FromHours 1.0

    let sendAndPause email = orchestrator {
        let! response = Activity.call sendNewsletter email
        do! Orchestrator.delay pauseDuration
        return response
    }

    let! responses =
        ["joe@foo.com"; "alex@bar.com"; "john@buzz.com"]
        |> List.map sendAndPause
        |> Activity.seq

    return responses |> List.forall id
}

[<FunctionName("SendNewsletter")>]
let SendNewsletter([<ActivityTrigger>] url) = Activity.run sendNewsletter url

[<FunctionName("NewsletterWorkflow")>]
let NewsletterWorkflow ([<OrchestrationTrigger>] context: IDurableOrchestrationContext) =
    Orchestrator.run (newsletter, context)