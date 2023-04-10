// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// copied from https://github.com/dotnet/aspnetcore/blob/3ea008c80d5cc63de7f90ddfd6823b7b006251ff/src/Shared/ServerInfrastructure/SslDuplexPipe.cs

using System.IO.Pipelines;
using System.Net.Security;

namespace LiteRPS;

internal class SslDuplexPipe : DuplexPipeStream, IDuplexPipe
{
    public SslDuplexPipe(IDuplexPipe duplexPipe, StreamPipeReaderOptions readerOptions, StreamPipeWriterOptions writerOptions,
        Func<Stream, SslStream> createStream) : base(duplexPipe.Input, duplexPipe.Output)
    {
        var stream = createStream(this);
        Stream = stream;

        Input = PipeReader.Create(Stream, readerOptions);
        Output = PipeWriter.Create(Stream, writerOptions);
    }

    public SslStream Stream { get; }

    public PipeReader Input { get; }
    public PipeWriter Output { get; }
}