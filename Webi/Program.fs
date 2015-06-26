
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
    IO.Directory.EnumerateFiles(@"bin/", "*.dll")
    |> Seq.map IO.Path.GetFullPath

let loadDlls (files :FilePath seq) :Assembly list =
    files |> Seq.map Assembly.LoadFile 
          |> Seq.toList

let filterStartup (asm :Assembly list) :Type list =
    let getTypes (a :Assembly) =
        try
            printfn "Retriving types from %s" a.FullName
            a.GetExportedTypes()
        with
        | :? ReflectionTypeLoadException as e ->
            printfn "Loading types failed!"
            printfn "Loaded types:"
            e.Types |> Seq.filter (fun t -> t <> null) |> Seq.iter (fun t -> printfn "\t%s" t.FullName)
            printfn "Unfisnished loaded types:"
            e.LoaderExceptions |> Seq.iter (fun et -> printfn "\t%s: %s" (et.GetType().Name) et.Message)
            reraise()

    asm
    |> Seq.collect getTypes
    |> Seq.filter (fun t -> t.Name = "Startup")
    |> Seq.toList

let createConfigFunc (startup_type :Type) :StartupConfigure =
    let ctor = startup_type.GetConstructor([||])
    let instance = ctor.Invoke(null)
    let config_method = startup_type.GetMethod("Configure")
    fun (app :IAppBuilder) ->
        config_method.Invoke(instance, [|app|]) |> ignore

let installAssemblyLoadHandler() :unit =
    let loaded_asm = Collections.Generic.Dictionary<string,Assembly>()

    AppDomain.CurrentDomain.AssemblyLoad
    |> Observable.add (fun e -> loaded_asm.[e.LoadedAssembly.FullName] <- e.LoadedAssembly)

    AppDomain.CurrentDomain.add_AssemblyResolve(
        fun o e -> match loaded_asm.TryGetValue e.Name with
                   | false, _ -> null
                   | true, a -> a
    )

[<EntryPoint>]
let main argv = 
    installAssemblyLoadHandler()

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
