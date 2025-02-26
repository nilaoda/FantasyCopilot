﻿// Copyright (c) Fantasy Copilot. All rights reserved.
// <auto-generated />

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.AI.TextCompletion;

namespace FantasyCopilot.Services;

internal sealed class TextCompletionStreamingResult : ITextCompletionStreamingResult
{
    private readonly string _result;

    public TextCompletionStreamingResult(string result)
    {
        this._result = result ?? string.Empty;
    }

    public Task<string> GetCompletionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(this._result);
    }

    public IAsyncEnumerable<string> GetCompletionStreamingAsync(CancellationToken cancellationToken = default)
    {
        return this.GetCompletionAsync(cancellationToken).ToAsyncEnumerable();
    }
}
