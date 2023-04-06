<div align="center">

<img src="./.github/logo.png" height="150" width="150">

# Smoall

> "Minimal, Fast, and Smooth F# Web Server"

</div>

## Introduction
Smoall is a Minimal, Fast, and Smooth F# Web Server, Mainly for the fun of it, I want to learn F# language and some .NET Core Network API (or some network-related knowledge) through this project.

[F#](https://dotnet.microsoft.com/zh-cn/languages/fsharp) is an open-source language that makes it easy to write succinct, robust, and performant code.

## Build and Run

- build: `dotnet build`
- test: `dotnet test`
- run: `dotnet run --project smoall`

> May require root privileges to run (should be administrator privileges on Windows)

If you need to package it into a single-file executable program:
```
dotnet publish -c Release \                                                                                   
               -r linux-x64 \
               --self-contained true \
               -p:PublishSingleFile=true \
               -p:IncludeNativeLibrariesForSelfExtract=true
```

This project has enabled .NET Native AOT optimization by default, which can be turned off in [smoall/smoall.fsproj](./smoall/smoall.fsproj):
```xml
<PropertyGroup>
    ...
    <PublishReadyToRun>true</PublishReadyToRun>
    <PublishTrimmed>true</PublishTrimmed>
    ...
</PropertyGroup>
```

## Documents
Everything is slowly being perfected...

## Acknowledgments

- [Writing a Web Server from Scratch](https://www.codeproject.com/articles/859108/writing-a-web-server-from-scratch) : This article was my main source of inspiration, the author wrote a simple web server in C#

## License
The MIT License (MIT)

Copyright (c) 2022 Muqiu Han

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.