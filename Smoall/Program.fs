module Smoall.WebServer

open System
open System.Net
open System.Net.Sockets
open System.Text
open System.Threading
open Log

type Server () =

    /// Set up a semaphore that waits for a specified number of simultaneously allowed connections:
    static let MAX_SIMULTANEOUS_CONNECTION = 20

    static let semaphore =
        new Semaphore(MAX_SIMULTANEOUS_CONNECTION, MAX_SIMULTANEOUS_CONNECTION)

    static let listener = Server.InitializeListener()

    /// Instantiate the HttpListener and add the localhost prefixes
    static member private InitializeListener () =
        let listener : HttpListener = new HttpListener() in
        listener.Prefixes.Add("http://localhost/")

        // Returns list of IP addresses assigned to localhost network devices,
        // such as hardwired ethernet, wireless, etc:
        Dns.GetHostEntry(Dns.GetHostName()).AddressList
        |> Array.filter (fun (ip : IPAddress) ->
            match ip.AddressFamily with
            | AddressFamily.InterNetwork -> true
            | _ -> false)
        |>
        // Listen to IP address as well:
        Array.iter (fun ip ->
            let ip = $"http://{ip.ToString()}/"
            Log.Info $"Listening on IP {ip}"
            listener.Prefixes.Add ip)

        listener

    
    /// Start awaiting for connections, up to the "maxSimultaneousConnections" value
    /// This code runs in a separate thread
    static member private RunServer () =
        while true do
            semaphore.WaitOne() |> ignore
            Server.StartConnectionListener() |> Async.RunSynchronously
    
    /// Begin listening to connections on a separate worker thread.
    static member public Start () =
        listener.Start()
        Tasks.Task.Run(Server.RunServer) |> ignore

    /// Await connections.
    static member private StartConnectionListener () =
        async {
            // Wait for a connection. Return to caller while we wait.
            let! context = listener.GetContextAsync() |> Async.AwaitTask

            // Release the semaphore so that another listener can be immediately started up.
            semaphore.Release() |> ignore

            // We have a connection, do something...
            let response : array<byte> = "Hello Smoall!" |> Encoding.UTF8.GetBytes
            context.Response.ContentLength64 <- response.Length
            context.Response.OutputStream.Write(response, 0, response.Length)
            context.Response.OutputStream.Close()
        }

[<EntryPoint>]
let main args =
    Server.Start ()
    Console.ReadLine () |> ignore
    0
