﻿// <auto-generated />


namespace FantasyCopilot.Libs.Everything.Interfaces
{
    using System.Collections.Generic;

    internal interface IQueryGenerator
    {
        RequestFlags Flags { get; }

        IEnumerable<string> GetQueryParts();
    }
}
