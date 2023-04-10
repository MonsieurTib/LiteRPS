// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// copied from https://github.com/dotnet/aspnetcore/blob/a450cb69b5e4549f5515cdb057a68771f56cefd7/src/Shared/ValueTaskExtensions/ValueTaskExtensions.cs

using System.IO.Pipelines;
using System.Runtime.CompilerServices;

namespace LiteRPS;

public static class ValueTaskExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task GetAsTask(this in ValueTask<FlushResult> valueTask)
    {
        // Try to avoid the allocation from AsTask
        if (!valueTask.IsCompletedSuccessfully)
        {
            return valueTask.AsTask();
        }

        // Signal consumption to the IValueTaskSource
        valueTask.GetAwaiter().GetResult();
        return Task.CompletedTask;

    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask GetAsValueTask(this in ValueTask<FlushResult> valueTask)
    {
        // Try to avoid the allocation from AsTask
        if (!valueTask.IsCompletedSuccessfully)
        {
            return new ValueTask(valueTask.AsTask());
        }

        // Signal consumption to the IValueTaskSource
        valueTask.GetAwaiter().GetResult();
        return default;

    }
}