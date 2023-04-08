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

module smoall.Exception

open System

type ExternalError =
    | OK
    | ExpiredSession
    | NotAuthorized
    | FileNotFound of String
    | PageNotFound of String
    | ServerError of String
    | UnsupportedFileType of String

and InternalError = ExecutableFileLocation of String

module Error =
    type T =
        | ExternalError of ExternalError
        | InternalError of InternalError


        member public this.ToString : String =
            match this with
            | ExternalError server_error ->
                match server_error with
                | UnsupportedFileType extension -> $"Unsupported file type : {extension}"
                | e -> e.ToString()
            | InternalError internal_error ->
                match internal_error with
                | ExecutableFileLocation path -> $"smoall executable file location exception: {path}"

        member public this.Raise () = failwith this.ToString

type SmoallException = Error.T
