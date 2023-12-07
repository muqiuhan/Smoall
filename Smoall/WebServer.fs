module Smoall.WebServer

open System.Collections.Generic
open System.Net
open System.Threading
open System.Threading.Tasks
open System.Text

type WebServer () as self =
    let listener : HttpListener = new HttpListener()
    let maxSimultaneousConnections : int = 20

    let sem : Semaphore =
        new Semaphore(maxSimultaneousConnections, maxSimultaneousConnections)

    do WebServer.GetLocalHostIP() |> self.InitializeListener

    member public this.Start () =
        listener.Start()
        Task.Run(fun () -> this.RunServer())

    member private this.RunServer () =
        task {
            while true do
                let _ = sem.WaitOne()
                this.StartConnectionListener()
        }

    member private this.StartConnectionListener () =
        task {
            let! context = listener.GetContextAsync()
            let _ = sem.Release()

            let response = "Hello Smoall!"
            let encoded = Encoding.UTF8.GetBytes(response)

            context.Response.ContentLength64 <- encoded.Length
            context.Response.OutputStream.Write(encoded, 0, encoded.Length)
            context.Response.OutputStream.Close()
        }
        |> Task.WaitAll

    member private this.InitializeListener (localhostIPList : array<IPAddress>) =
        let listen ip =
            printfn $"Listening on IP {ip}"
            listener.Prefixes.Add(ip)

        listener.Prefixes.Add("http://localhost/")

        Array.iter
            (fun ip -> ip.ToString() |> fun ip -> $"http://{ip}/" |> listen)
            localhostIPList


    static member private GetLocalHostIP () =
        Dns.GetHostName()
        |> Dns.GetHostEntry
        |> _.AddressList
        |> Array.filter (fun ip ->
            ip.AddressFamily = System.Net.Sockets.AddressFamily.InterNetwork)
