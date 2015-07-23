
open System
open System.Reflection
open Owin
open Microsoft.Owin.StaticFiles
open Microsoft.Owin
open Microsoft.Owin.FileSystems
open Microsoft.Owin.Hosting

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

let discoverDllFiles(bin_directory) :FilePath seq =
    if IO.Directory.Exists bin_directory
        then IO.Directory.EnumerateFiles(bin_directory, "*.dll")
             |> Seq.map IO.Path.GetFullPath
        else Seq.empty

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

type CommandLineContext =
    { port :int
      bin_dir :string }

[<EntryPoint>]
let main argv = 
    let options = [
        ["p"; "port:"], fun ctx v -> { ctx with port=Int32.Parse v }
        ["bin:"], fun ctx v -> { ctx with bin_dir=v }
    ]
    let cmd_parser = RZ.OptionParser.parser options
    let (settings, _) = cmd_parser { port=9000; bin_dir=null } argv

    installAssemblyLoadHandler()

    let configs = if settings.bin_dir=null
                      then Seq.empty
                      else discoverDllFiles settings.bin_dir
                  |> loadDlls
                  |> filterStartup
                  |> Seq.map createConfigFunc
                  |> Seq.toList

    let startup = Startup configs

    use server = WebApp.Start(sprintf "http://+:%d" settings.port, fun builder -> startup.Configuration builder)
    printfn "ENTER to exit..."
    Console.ReadLine() |> ignore
    0
