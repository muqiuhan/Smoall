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

module smoall.WebSite

open System
open System.IO
open System.Reflection
open System.Text

open Exception
open Log

type WebSite () =

    static let EXECUTION_PATH = Assembly.GetExecutingAssembly().Location

    static member public PATH : String = WebSite.DefaultPath

    static member DefaultPath : String =
        let path : Ref<String> = ref EXECUTION_PATH

        while (Path.GetFileName path.Value) <> "smoall" do
            if path.Value = "/" then
                (Error.InternalError(ExecutableFileLocation EXECUTION_PATH)).Raise()

            path.Value <- (Path.GetDirectoryName path.Value)

        Path.Combine [| path.Value; "WebSite" |]

    static member public LoadFile (path : String) : Result<byte[], ExternalError> =
        try
            let fullPath : String = WebSite.PATH + path

            Log.Debug $"Load file : {fullPath}"
            Ok(Encoding.UTF8.GetBytes(File.ReadAllText(fullPath)))
        with :? FileNotFoundException ->
            Error(FileNotFound path)

    static member public LoadImage (path : String) : byte[] =
        let fullPath : String = WebSite.PATH + path
        let stream : FileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read)
        let binaryReader : BinaryReader = new BinaryReader(stream)

        Log.Debug $"Load image: {fullPath}"
        let image = binaryReader.ReadBytes(int stream.Length)

        stream.Close()
        binaryReader.Close()

        image
