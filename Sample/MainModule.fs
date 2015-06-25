namespace Sample

open Owin
open Nancy
open Nancy.Owin

type Startup() =
    member x.Configure (app :IAppBuilder) :unit =
        app.UseNancy()
        |> ignore

type MainModule() = 
    inherit NancyModule()

    do  let Get = base.Get
        Get.["/data/sample"] <- fun _ -> "Data from API!" :> obj