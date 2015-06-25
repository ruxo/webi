
open System
open System.Reflection
open Owin
open Microsoft.Owin.StaticFiles
open Microsoft.Owin
open Microsoft.Owin.FileSystems
open Microsoft.Owin.Hosting

let port = 9000

type FilePath = string

type Startup() =
    member x.Configuration (app :IAppBuilder) =
        let root_dir = IO.Directory.GetCurrentDirectory()

        FileServerOptions( EnableDirectoryBrowsing = true,
                           FileSystem = PhysicalFileSystem(root_dir)
                         )
        |> app.UseFileServer
        |> ignore

let discoverDllFiles() :FilePath seq = IO.Directory.EnumerateFiles("*.dll")

let loadDlls (files :FilePath seq) :Assembly list =
    files |> Seq.map Assembly.LoadFile 
          |> Seq.toList

[<EntryPoint>]
let main argv = 
    discoverDllFiles()
    |> loadDlls
    |> ignore

    use server = WebApp.Start<Startup>(sprintf "http://+:%d" port)
    printfn "ENTER to exit..."
    Console.ReadLine() |> ignore
    0
