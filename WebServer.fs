module Smoall.WebServer

open System.Net
open System.Net.Sockets
open System.Threading
open System.Text

type Server =
  static let log = Logger.Log ("Server")
  static let maxSimultaneousConnections = 20

  static let sem =
    new Semaphore (maxSimultaneousConnections, maxSimultaneousConnections)

  static let listener =
    Server.GetLocalHostIPs () |> Server.InitializeHttpListener

  /// Specified number of simultaneously allowed connections.
  static member public MaxSimultaneousConnections = maxSimultaneousConnections

  /// Returns list of IP addresses assigned to localhost network devices, such as hardwired ethernet, wireless, etc.
  static member private GetLocalHostIPs () =
    Dns.GetHostName ()
    |> Dns.GetHostEntry
    |> _.AddressList
    |> Array.filter (fun ip -> ip.AddressFamily = AddressFamily.InterNetwork)
    |> Array.toList

  static member private InitializeHttpListener
    (localhostIPs : list<IPAddress>)
    =
    let listener = new HttpListener ()
    listener.Prefixes.Add ("http://localhost/")

    localhostIPs
    |> List.iter (fun (ip : IPAddress) ->
      let prefix = $"http://{ip.ToString ()}/"
      log.info ($"Listening on IP: {prefix}")
      listener.Prefixes.Add (prefix))

    listener

  /// Begin listening to connections on a separate worker thread.
  static member public Start () =
    try
      listener.Start ()

      Tasks.Task.Run (fun () -> Server.RunServer (listener) : unit)
      |> Async.AwaitTask
      |> Async.RunSynchronously
    with e ->
      log.error (e.ToString ())

  /// Start awaiting for connections, up to the "maxSimultaneousConnections" value.
  /// This code runs in a separate thread. *)
  static member private RunServer (listener : HttpListener) =
    while true do
      sem.WaitOne () |> ignore
      Server.StartConnectionListener (listener)

  /// Await connections.
  static member private StartConnectionListener (listener : HttpListener) =
    task {
      // Wait for a connection. Return to caller while we wait.
      let! context = listener.GetContextAsync ()
      // Release the semaphore so that another listener can be immediately started up.
      sem.Release () |> ignore

      "Hello! I'm Smoall!!!"
      |> Encoding.UTF8.GetBytes
      |> fun encoded ->
          context.Response.ContentLength64 <- encoded.Length
          context.Response.OutputStream.Write (encoded, 0, encoded.Length)
          context.Response.OutputStream.Close ()
    }
    |> Async.AwaitTask
    |> Async.RunSynchronously
