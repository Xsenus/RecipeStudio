using System;

namespace RecipeStudio.Desktop.Services;

public static class CalculationOriginModes
{
    public const string ExcelFirstRow = "excel_first_row";
    public const string CurrentFirstWorking = "current_first_working";

    public static string Normalize(string? mode)
        => string.Equals(mode, CurrentFirstWorking, StringComparison.OrdinalIgnoreCase)
            ? CurrentFirstWorking
            : ExcelFirstRow;
}

public static class ANozzleKinematicsModes
{
    public const string ExcelFirstRow = "excel_first_row";
    public const string CurrentPerPoint = "current_per_point";

    public static string Normalize(string? mode)
        => string.Equals(mode, CurrentPerPoint, StringComparison.OrdinalIgnoreCase)
            ? CurrentPerPoint
            : ExcelFirstRow;
}

public static class VelocityCalculationModes
{
    public const string ExcelExact = "excel_exact";
    public const string CurrentRounded = "current_rounded";

    public static string Normalize(string? mode)
        => string.Equals(mode, CurrentRounded, StringComparison.OrdinalIgnoreCase)
            ? CurrentRounded
            : ExcelExact;
}

public static class TopLowPulseModes
{
    public const string ExcelLinked = "excel_linked";
    public const string CurrentIndependent = "current_independent";

    public static string Normalize(string? mode)
        => string.Equals(mode, CurrentIndependent, StringComparison.OrdinalIgnoreCase)
            ? CurrentIndependent
            : ExcelLinked;
}

public static class ExcelExportModes
{
    public const string Workbook = "workbook";
    public const string FlatPoints = "flat_points";

    public static string Normalize(string? mode)
        => string.Equals(mode, FlatPoints, StringComparison.OrdinalIgnoreCase)
            ? FlatPoints
            : Workbook;
}

public static class RecommendedFlowBulkModes
{
    public const string Disabled = "disabled";
    public const string IceRateHeader = "ice_rate_header";

    public static string Normalize(string? mode)
        => string.Equals(mode, IceRateHeader, StringComparison.OrdinalIgnoreCase)
            ? IceRateHeader
            : Disabled;
}
