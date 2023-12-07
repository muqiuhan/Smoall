module Smoall.Tests.Program

open System
open System.Threading.Tasks
open Smoall.WebServer


[<EntryPoint>]
let main _ =
    let server = new WebServer()
    task { return! server.Start() } |> Task.WaitAll
    Console.ReadLine() |> ignore
    0
