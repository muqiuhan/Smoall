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

module smoall.Log

#nowarn "3535"

open System

module Logger =

    [<Interface>]
    type T =
        abstract member Info : String -> Unit
        abstract member Warn : String -> Unit
        abstract member Error : String -> Unit
        abstract member Debug : String -> Unit

    type Console private () =

        static let self : Console = Console()

        /// Output logs with additional colors, the output will be locked
        static member private log (color : ConsoleColor) (s : String) : Unit =
            let consoleLogOutputLock = obj ()

            lock consoleLogOutputLock (fun _ ->
                Console.ForegroundColor <- color
                printf $"""{(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))} > """
                Console.ForegroundColor <- ConsoleColor.White
                printfn $"{s}"
                Console.ResetColor())

        interface T with
            member this.Info s = Console.log ConsoleColor.Green s
            member this.Debug s = Console.log ConsoleColor.Cyan s
            member this.Warn s = Console.log ConsoleColor.Yellow s
            member this.Error s = Console.log ConsoleColor.Red s

        static member Info = (self :> T).Info
        static member Debug = (self :> T).Debug
        static member Warn = (self :> T).Warn
        static member Error = (self :> T).Error


type LogTarget =
    | File
    | Console

type Log private () =

    static let mutable TARGET : LogTarget = Console

    static member Info (message : String) : Unit =
        match TARGET with
        | File -> failwith "Unsupported log target"
        | Console -> Logger.Console.Info message

    static member Debug (message : String) : Unit =
        match TARGET with
        | File -> failwith "Unsupported log target"
        | Console -> Logger.Console.Debug message

    static member Warn (message : String) : Unit =
        match TARGET with
        | File -> failwith "Unsupported log target"
        | Console -> Logger.Console.Warn message

    static member Error (message : String) : Unit =
        match TARGET with
        | File -> failwith "Unsupported log target"
        | Console -> Logger.Console.Error message
