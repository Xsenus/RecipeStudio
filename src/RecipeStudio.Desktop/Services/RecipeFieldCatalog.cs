using System;
using System.Collections.Generic;
using System.Linq;

namespace RecipeStudio.Desktop.Services;

/// <summary>
/// Canonical field dictionary used across model, TSV/Excel and import aliases.
/// Centralizing these names reduces drift between serializers and UI.
/// </summary>
public static class RecipeFieldCatalog
{
    public static readonly string[] BaseColumns =
    {
        "recipe_code",
        "n_point",
        "Act",
        "Safe",
        "r_crd",
        "z_crd",
        "place",
        "hidden",
        "a_nozzle",
        "alfa_crd",
        "betta_crd",
        "speed_table",
        "ice_rate",
        "ice_grind",
        "air_pressure",
        "air_temp",
        "container",
        "d_clamp_form",
        "d_clamp_cont",
        "description",
    };

    public static readonly string[] ExcelComputedColumns =
    {
        "recommended_alfa",
        "time_sec",
        "v_mm_min",
        "recommended_ice_rate",
    };

    public static readonly string[] CalcColumns =
    {
        "Xr0",
        "Yx0",
        "Zr0",
        "dX",
        "dY",
        "dZ",
        "dA",
        "aB",
        "Xpuls",
        "Ypuls",
        "Zpuls",
        "Apuls",
        "Bpuls",
        "Top_puls",
        "Top_Hz",
        "Low_puls",
        "Low_Hz",
        "Clamp_puls",
    };

    public static readonly string[] TsvColumns = BaseColumns
        .Concat(CalcColumns)
        .ToArray();

    public static readonly string[] ExcelColumns =
    {
        "recipe_code",
        "n_point",
        "Act",
        "Safe",
        "r_crd",
        "z_crd",
        "place",
        "hidden",
        "a_nozzle",
        "recommended_alfa",
        "alfa_crd",
        "betta_crd",
        "speed_table",
        "time_sec",
        "v_mm_min",
        "recommended_ice_rate",
        "ice_rate",
        "ice_grind",
        "air_pressure",
        "air_temp",
        "container",
        "d_clamp_form",
        "d_clamp_cont",
        "description",
    }
    .Concat(CalcColumns)
    .ToArray();

    public static readonly string[] RequiredImportColumns =
    {
        "r_crd",
        "z_crd",
        "a_nozzle",
        "alfa_crd",
        "betta_crd",
        "speed_table",
        "ice_rate",
    };

    public static readonly IReadOnlyDictionary<string, string> ExcelAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["α rec"] = "recommended_alfa",
            ["alfa_rec"] = "recommended_alfa",
            ["recom_alfa"] = "recommended_alfa",

            ["a_nozz"] = "a_nozzle",
            ["beta_crd"] = "betta_crd",
            ["betta_c"] = "betta_crd",

            ["speed_tab"] = "speed_table",
            ["time"] = "time_sec",
            ["t_sec"] = "time_sec",
            ["v"] = "v_mm_min",
            ["V_mm_min"] = "v_mm_min",

            ["ice_rate_rec"] = "recommended_ice_rate",
            ["ice_grin"] = "ice_grind",

            ["air_pre"] = "air_pressure",
            ["air_tem"] = "air_temp",

            ["contai"] = "container",
            ["d_clamp_f"] = "d_clamp_form",
            ["d_clamp"] = "d_clamp_cont",
        };
}
