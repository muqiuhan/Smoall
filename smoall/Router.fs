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

open System
open System.IO
open System.Net
open System.Text

open Exception
open Log
open WebSite

type ExtensionInfo (Loader : String -> String -> ExtensionInfo -> ResponsePaket, ContentType : String) =

    member public this.ContentType : String = ContentType

    member public this.Loader : String -> String -> ExtensionInfo -> ResponsePaket = Loader


and ResponsePaket () =

    [<DefaultValue>]
    val mutable public Redirect : String

    [<DefaultValue>]
    val mutable public Data : byte[]

    [<DefaultValue>]
    val mutable public ContentType : String

    [<DefaultValue>]
    val mutable public Encoding : Encoding

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

        file.Data <- WebSite.LoadFile fullPath
        file.ContentType <- extensionInfo.ContentType
        file.Encoding <- Encoding.UTF8

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

            this.FileLoader fullPath extension extensionInfo

    member public this.Route (request : HttpListenerRequest) : ResponsePaket =
        let path : String ref = ref request.RawUrl
        let parameters : String ref = ref String.Empty

        if request.RawUrl.IndexOf("?") <> -1 then
            path.Value <- request.RawUrl.Remove(request.RawUrl.IndexOf("?"))
            parameters.Value <- request.RawUrl.Substring(request.RawUrl.IndexOf("?") + 1)

        let httpMethod : String = request.HttpMethod
        this._Route path.Value httpMethod parameters.Value

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
            raise (SmoallException(UnsupportedFileType extension))
