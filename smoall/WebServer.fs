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

open System
open System.Net
open System.Net.Sockets
open System.Threading

open Log
open Router
open WebSite

type WebServer () =

    /// Set up a semaphore that waits for a specified number of simultaneously allowed connections:
    static let MAX_SIMULTANEOUS_CONNECTION : int = 20

    static let router : Router = Router()

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
        Tasks.Task.Run(fun () -> WebServer.RunServer listener) |> ignore

    /// Start awaiting for connections, up to the "maxSimultaneousConnections" value
    /// This code runs in a separate thread
    static member private RunServer (listener : HttpListener) : Unit =
        while true do
            semaphore.WaitOne() |> ignore
            WebServer.StartConnectionListener(listener) |> Async.RunSynchronously

    /// Await connections.
    static member private StartConnectionListener (listener : HttpListener) : Async<unit> =
        async {
            try
                // Wait for a connection. Return to caller while we wait.
                let! context = listener.GetContextAsync() |> Async.AwaitTask

                // Release the semaphore so that another listener can be immediately started up.
                semaphore.Release() |> ignore

                // Router
                WebServer.Respond context.Response (router.Route(context.Request))

                WebServer.LogRequests context.Request
            with e ->
                Log.Error(e.ToString())
        }

    static member private Respond (response : HttpListenerResponse) (responsePaket : ResponsePaket) : Unit =
        response.ContentType <- responsePaket.ContentType
        response.ContentLength64 <- responsePaket.Data.Length
        response.ContentEncoding <- responsePaket.Encoding
        response.StatusCode <- int HttpStatusCode.OK

        response.OutputStream.Write(responsePaket.Data, 0, responsePaket.Data.Length)
        response.OutputStream.Close()


    static member private LogRequests (request : HttpListenerRequest) : unit =
        let remoteEndPoint = request.RemoteEndPoint
        let httpMethod = request.HttpMethod

        let path =
            request.Url.AbsoluteUri.IndexOf $"{request.Url.Host}" + request.Url.Host.Length
            |> request.Url.AbsoluteUri.Substring

        Log.Info $"{remoteEndPoint} {httpMethod} {path}"

    static member public Start (websitePath : String) : Unit =
        let localHostIPs : array<IPAddress> = WebServer.GetLocalHostIPs()
        let listener : HttpListener = WebServer.InitializeListener localHostIPs
        router.websitePath <- websitePath
        WebServer.Start listener

let Start () =
    Log.Info $"Start WebSite on {WebSite.DefaultPath}"
    WebServer.Start WebSite.DefaultPath
