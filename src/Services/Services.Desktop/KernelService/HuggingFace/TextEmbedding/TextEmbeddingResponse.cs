﻿// Copyright (c) Fantasy Copilot. All rights reserved.
// <auto-generated />

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FantasyCopilot.Services;

/// <summary>
/// HTTP Schema for embedding response.
/// </summary>
public sealed class TextEmbeddingResponse
{
    /// <summary>
    /// Model containing embedding.
    /// </summary>
    public sealed class EmbeddingVector
    {
        [JsonPropertyName("embedding")]
        public IList<float> Embedding { get; set; }
    }

    /// <summary>
    /// List of embeddings.
    /// </summary>
    [JsonPropertyName("data")]
    public IList<EmbeddingVector> Embeddings { get; set; }
}
