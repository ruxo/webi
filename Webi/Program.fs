
open System
open System.Reflection
open Owin
open Microsoft.Owin.StaticFiles
open Microsoft.Owin
open Microsoft.Owin.FileSystems
open Microsoft.Owin.Hosting

let port = 9000

type FilePath = string
type StartupConfigure = IAppBuilder -> unit

type Startup(configs :StartupConfigure list) =
    member x.Configuration (app :IAppBuilder) =
        let root_dir = IO.Directory.GetCurrentDirectory()

        let builder = FileServerOptions( EnableDirectoryBrowsing = true,
                           FileSystem = PhysicalFileSystem(root_dir)
                         )
                      |> app.UseFileServer

        configs |> Seq.iter (fun f -> f builder)

let discoverDllFiles() :FilePath seq =
    IO.Directory.EnumerateFiles(".", "*.dll")
    |> Seq.map IO.Path.GetFullPath

let loadDlls (files :FilePath seq) :Assembly list =
    let loadAsm s =
        printfn "Loading %s" s
        Assembly.LoadFile s

    files |> Seq.map loadAsm 
          |> Seq.toList

let filterStartup (asm :Assembly list) :Type list =
    asm
    |> Seq.collect (fun a ->  printfn "Filtering... %s" a.FullName
                              a.GetTypes())
    |> Seq.filter (fun t -> t.Name = "Startup")
    |> Seq.toList

let createConfigFunc (startup_type :Type) :StartupConfigure =
    let ctor = startup_type.GetConstructor(null)
    let instance = ctor.Invoke(null)
    let config_method = startup_type.GetMethod("Configure")
    fun (app :IAppBuilder) ->
        config_method.Invoke(instance, [|app|]) |> ignore

[<EntryPoint>]
let main argv = 
    let configs = discoverDllFiles()
                    |> loadDlls
                    |> filterStartup
                    |> Seq.map createConfigFunc
                    |> Seq.toList

    let startup = Startup configs

    use server = WebApp.Start(sprintf "http://+:%d" port, fun builder -> startup.Configuration builder)
    printfn "ENTER to exit..."
    Console.ReadLine() |> ignore
    0
