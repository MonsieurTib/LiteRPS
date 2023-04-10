// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// copied from https://github.com/dotnet/aspnetcore/blob/a450cb69b5e4549f5515cdb057a68771f56cefd7/src/Shared/ServerInfrastructure/DuplexPipe.cs
// and add some minor tweaks

using System.IO.Pipelines;

namespace LiteRPS;

public class DuplexPipe : IDuplexPipe
{
    private DuplexPipe(PipeReader reader, PipeWriter writer)
    {
        Input = reader;
        Output = writer;
    }

    public PipeReader Input { get; }
    public PipeWriter Output { get; }

    public static (IDuplexPipe transport, IDuplexPipe application) CreateConnectionPair(PipeOptions inputOptions, PipeOptions outputOptions)
    {
        var input = new Pipe(inputOptions);
        var output = new Pipe(outputOptions);

        var transportToApplication = new DuplexPipe(output.Reader, input.Writer);
        var applicationToTransport = new DuplexPipe(input.Reader, output.Writer);

        return new ValueTuple<IDuplexPipe, IDuplexPipe>(applicationToTransport, transportToApplication);
    }
}