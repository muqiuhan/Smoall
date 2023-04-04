/// The MIT License (MIT)
///
/// Copyright (c) 2022 Muqiu Han
///
/// Permission is hereby granted, free of charge, to any person obtaining a copy
/// of this software and associated documentation files (the "Software"), to deal
/// in the Software without restriction, including without limitation the rights
/// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
/// copies of the Software, and to permit persons to whom the Software is
/// furnished to do so, subject to the following conditions:
///
/// The above copyright notice and this permission notice shall be included in all
/// copies or substantial portions of the Software.
///
/// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
/// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
/// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
/// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
/// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
/// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
/// SOFTWARE.

module smoall.WebServer

open System.Net
open System.Net.Sockets
open System.Text
open System.Threading
open Log

type Server () =

    /// Set up a semaphore that waits for a specified number of simultaneously allowed connections:
    static let MAX_SIMULTANEOUS_CONNECTION : int = 20

    static let semaphore : Semaphore =
        new Semaphore(MAX_SIMULTANEOUS_CONNECTION, MAX_SIMULTANEOUS_CONNECTION)


    /// Returns list of IP addresses assigned to localhost network devices,
    /// such as hardwired ethernet, wireless, etc:
    static member private GetLocalHostIPs () : array<IPAddress> =
        let host : IPHostEntry = Dns.GetHostEntry(Dns.GetHostName())

        Array.filter
            (fun (ip : IPAddress) ->
                match ip.AddressFamily with
                | AddressFamily.InterNetwork -> true
                | _ -> false)
            host.AddressList

    /// Instantiate the HttpListener and add the localhost prefixes
    static member private InitializeListener (localHostIPs : array<IPAddress>) : HttpListener =
        let listener : HttpListener = new HttpListener() in
        listener.Prefixes.Add("http://localhost/")

        // Listen to IP address as well:
        Array.iter
            (fun ip ->
                let ip = $"http://{ip.ToString()}/"
                Log.Info $"Listening on IP {ip}"
                listener.Prefixes.Add ip)
            localHostIPs

        listener

    /// Begin listening to connections on a separate worker thread.
    static member private Start (listener : HttpListener) : Unit =
        listener.Start()
        Tasks.Task.Run(fun () -> Server.RunServer listener) |> ignore

    /// Start awaiting for connections, up to the "maxSimultaneousConnections" value
    /// This code runs in a separate thread
    static member private RunServer (listener : HttpListener) : Unit =
        while true do
            semaphore.WaitOne() |> ignore
            Server.StartConnectionListener(listener) |> Async.RunSynchronously

    /// Await connections.
    static member private StartConnectionListener (listener : HttpListener) : Async<unit> =
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

    static member public Start () =
        let localHostIPs : array<IPAddress> = Server.GetLocalHostIPs()
        let listener : HttpListener = Server.InitializeListener localHostIPs
        Server.Start listener

let Start = Server.Start
