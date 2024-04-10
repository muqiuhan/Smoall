module Smoall.Program

open WebServer

[<EntryPoint>]
let main argv =
  Server.Start()
  0