using System;
using System.Collections.ObjectModel;

namespace RecipeStudio.Desktop.Models;

public sealed class RecipeDocument
{
    public long RecipeId { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime ModifiedUtc { get; set; }

    // Legacy: used before SQLite. Kept for import/export convenience.
    public string FilePath { get; set; } = "";
    public string RecipeCode { get; set; } = "";

    public ObservableCollection<RecipePoint> Points { get; } = new();

    // Convenience (we keep it simple for the prototype)
    public double DClampForm { get; set; } = 800;
    public double DClampCont { get; set; } = 1600;
    public bool ContainerPresent { get; set; } = true;
}
