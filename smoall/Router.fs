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

module smoall.Router

open System.IO
open System.Text
open System.Net
open Exception
open System
open WebSite
open Log

type Router () =

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
