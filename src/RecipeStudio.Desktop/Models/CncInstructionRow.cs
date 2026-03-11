using System;
using System.Collections.Generic;

namespace RecipeStudio.Desktop.Models;

public sealed class CncInstructionRow
{
    private readonly IReadOnlyDictionary<string, string> _values;

    public CncInstructionRow(IReadOnlyDictionary<string, string> values)
    {
        _values = values ?? throw new ArgumentNullException(nameof(values));
    }

    public string this[string key]
        => _values.TryGetValue(key, out var value) ? value : string.Empty;
}
