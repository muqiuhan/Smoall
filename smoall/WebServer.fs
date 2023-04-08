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
open System.IO
open System.Text

open Log
open WebSite
open Exception


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

and ExtensionInfo (Loader : String -> String -> ExtensionInfo -> ResponsePaket, ContentType : String) =

    member public this.ContentType : String = ContentType

    member public this.Loader : String -> String -> ExtensionInfo -> ResponsePaket = Loader


and ResponsePaket (init_error : SmoallException) =

    [<DefaultValue>]
    val mutable public Redirect : String

    [<DefaultValue>]
    val mutable public Data : byte[]

    [<DefaultValue>]
    val mutable public ContentType : String

    [<DefaultValue>]
    val mutable public Encoding : Encoding

    let mutable error : SmoallException = init_error

    member this.Error
        with public get () = error
        and public set (err : SmoallException) = error <- err

    public new () = ResponsePaket(None)


and Router () =

    [<DefaultValue>]
    val mutable public websitePath : String

    member private this.extensionFolderMap : list<String * ExtensionInfo> =
        [ ("ico", ExtensionInfo(this.ImageLoader, "image/ico"))
          ("png", ExtensionInfo(this.ImageLoader, "image/png"))
          ("jpg", ExtensionInfo(this.ImageLoader, "image/jpg"))
          ("gif", ExtensionInfo(this.ImageLoader, "image/gif"))
          ("bmp", ExtensionInfo(this.ImageLoader, "image/bmp"))
          ("html", ExtensionInfo(this.PageLoader, "text/html"))
          (String.Empty, ExtensionInfo(this.PageLoader, "text/html"))
          ("css", ExtensionInfo(this.FileLoader, "text/css"))
          ("js", ExtensionInfo(this.FileLoader, "text/javascript")) ]

    /// Read in an image file and returns a ResponsePacket with the raw data.
    /// FIXME: Could not find file '/favicon.ico'
    member private this.ImageLoader
        (fullPath : String)
        (extension : String)
        (extensionInfo : ExtensionInfo)
        : ResponsePaket =

        Log.Info $"Load image: {fullPath}"
        let stream : FileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read)
        let binaryReader : BinaryReader = new BinaryReader(stream)
        let image : ResponsePaket = ResponsePaket()

        image.Data <- WebSite.LoadImage fullPath
        image.ContentType <- extensionInfo.ContentType

        stream.Close()
        binaryReader.Close()

        image

    /// Read in what is basically a text file and return a ResponsePacket with the text UTF8 encoded
    member private this.FileLoader
        (fullPath : String)
        (extension : String)
        (extensionInfo : ExtensionInfo)
        : ResponsePaket =

        let file : ResponsePaket = ResponsePaket()

        match WebSite.LoadFile fullPath with
        | Ok data ->
            file.Data <- data
            file.ContentType <- extensionInfo.ContentType
            file.Encoding <- Encoding.UTF8
            file

        | Error error ->
            file.Error <- Some(Error.ExternalError error)
            file


    /// Load an HTML file, taking into account missing extensions and a file-less IP/domain.
    /// which should default to index.html.
    member private this.PageLoader
        (fullPath : String)
        (extension : String)
        (extensionInfo : ExtensionInfo)
        : ResponsePaket =

        Log.Info $"Load page : {fullPath}"

        if fullPath = "/" then
            this._Route $"/index.html" "GET" String.Empty

        else
            let fullPath : String ref = ref fullPath

            if String.IsNullOrEmpty extension then
                fullPath.Value <- fullPath.Value + ".html"

            let fullPath : String = $"{Path.DirectorySeparatorChar}pages{fullPath.Value}"
            let page = this.FileLoader fullPath extension extensionInfo

            Option.iter
                (function
                | Error.ExternalError(FileNotFound path) -> page.Error <- Some(Error.ExternalError(PageNotFound path))
                | err -> page.Error <- Some err)
                page.Error

            page

    member public this.Route (request : HttpListenerRequest) : ResponsePaket =
        let path : String ref = ref request.RawUrl
        let parameters : String ref = ref String.Empty

        if request.RawUrl.IndexOf("?") <> -1 then
            path.Value <- request.RawUrl.Remove(request.RawUrl.IndexOf("?"))
            parameters.Value <- request.RawUrl.Substring(request.RawUrl.IndexOf("?") + 1)

        let httpMethod : String = request.HttpMethod
        this._Route path.Value httpMethod parameters.Value |> this.Check

    static member private OnError (error : ExternalError) : String =
        match error with
        | ExpiredSession -> "/ErrorPages/expiredSession.html"
        | NotAuthorized -> "/ErrorPages/notAuthorized.html"
        | FileNotFound _ -> "/ErrorPages/fileNotFound.html"
        | PageNotFound _ -> "/ErrorPages/pageNotFound.html"
        | ServerError _ -> "/ErrorPages/serverError.html"
        | UnsupportedFileType _ -> "/ErrorPages/unknownType.html"

    member private this.Check (responsePaket : ResponsePaket) : ResponsePaket =
        if Option.isSome responsePaket.Error then
            Option.map
                (fun error ->
                    match error with
                    | Error.ExternalError error -> this._Route (Router.OnError error) "GET" ""
                    | _ ->
                        let error = error.ToString
                        Log.Error error
                        failwith error)
                responsePaket.Error
            |> Option.get
        else
            responsePaket

    member private this._Route (path : String) (httpMethod : String) (parameters : String) : ResponsePaket =
        let extension : String ref = ref String.Empty

        if path.IndexOf "." <> -1 then
            extension.Value <- (Path.GetExtension path).Substring 1

        let extension : String = extension.Value

        try
            let (_, extensionInfo : ExtensionInfo) =
                List.find (fun (ext : String, _) -> ext = extension) this.extensionFolderMap

            extensionInfo.Loader path extension extensionInfo
        with :? Collections.Generic.KeyNotFoundException ->
            let error = Error.ExternalError(UnsupportedFileType extension)

            Log.Info error.ToString
            new ResponsePaket(Some error)

let Start () =
    Log.Info $"Start WebSite on {WebSite.DefaultPath}"
    WebServer.Start WebSite.DefaultPath
