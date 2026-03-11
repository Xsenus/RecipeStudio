using System;
using System.Collections.Generic;
using System.Linq;
using RecipeStudio.Desktop.Models;

namespace RecipeStudio.Desktop.Services;

public static class NozzleOrientationModes
{
    public const string PhysicalAngles = "physical_angles";
    public const string TargetTracking = "target_tracking";

    public static string Normalize(string? mode)
    {
        if (string.Equals(mode, TargetTracking, StringComparison.OrdinalIgnoreCase))
            return TargetTracking;

        return PhysicalAngles;
    }
}

public static class RecommendedAlfaModes
{
    public const string Plus90 = "plus_90";
    public const string Minus90 = "minus_90";

    public static string Normalize(string? mode)
    {
        if (string.Equals(mode, Minus90, StringComparison.OrdinalIgnoreCase))
            return Minus90;

        return Plus90;
    }
}

public readonly record struct NozzleAngleLimits(double AlfaMin, double AlfaMax, double BettaMin, double BettaMax)
{
    public static NozzleAngleLimits FromSettings(AppSettings? settings)
    {
        settings ??= new AppSettings();
        var alfaMin = Math.Min(settings.AlfaMinDeg, settings.AlfaMaxDeg);
        var alfaMax = Math.Max(settings.AlfaMinDeg, settings.AlfaMaxDeg);
        var bettaMin = Math.Min(settings.BettaMinDeg, settings.BettaMaxDeg);
        var bettaMax = Math.Max(settings.BettaMinDeg, settings.BettaMaxDeg);
        return new NozzleAngleLimits(alfaMin, alfaMax, bettaMin, bettaMax);
    }

    public (double Alfa, double Betta) Clamp(double alfa, double betta)
        => (Math.Clamp(alfa, AlfaMin, AlfaMax), Math.Clamp(betta, BettaMin, BettaMax));

    public bool IsOutOfRange(double alfa, double betta)
        => alfa < AlfaMin || alfa > AlfaMax || betta < BettaMin || betta > BettaMax;
}

public readonly record struct NozzleAngleDiagnostics(
    int TotalPoints,
    int OutOfRangeCount,
    int AlfaOutOfRangeCount,
    int BettaOutOfRangeCount,
    IReadOnlyList<int> SamplePointNumbers);

public static class NozzleOrientationPolicy
{
    public static bool UsePhysicalOrientation(string? mode)
        => NozzleOrientationModes.Normalize(mode) == NozzleOrientationModes.PhysicalAngles;

    public static NozzleAngleLimits GetLimits(AppSettings? settings)
        => NozzleAngleLimits.FromSettings(settings);

    public static (double Alfa, double Betta) ClampForPhysicalOrientation(AppSettings? settings, double alfa, double betta)
    {
        var limits = GetLimits(settings);
        return limits.Clamp(alfa, betta);
    }

    public static NozzleAngleDiagnostics AnalyzePoints(IEnumerable<RecipePoint> points, AppSettings? settings)
    {
        var source = points?.ToList() ?? new List<RecipePoint>();
        var limits = GetLimits(settings);
        var outCount = 0;
        var alfaOut = 0;
        var bettaOut = 0;
        var samples = new List<int>(4);

        foreach (var p in source)
        {
            var isAlfaOut = p.Alfa < limits.AlfaMin || p.Alfa > limits.AlfaMax;
            var isBettaOut = p.Betta < limits.BettaMin || p.Betta > limits.BettaMax;
            if (!isAlfaOut && !isBettaOut)
                continue;

            outCount++;
            if (isAlfaOut)
                alfaOut++;
            if (isBettaOut)
                bettaOut++;

            if (samples.Count < 4)
                samples.Add(p.NPoint);
        }

        return new NozzleAngleDiagnostics(source.Count, outCount, alfaOut, bettaOut, samples);
    }

    public static string BuildWarningText(NozzleAngleDiagnostics diagnostics, NozzleAngleLimits limits)
    {
        if (diagnostics.OutOfRangeCount <= 0)
            return string.Empty;

        var points = diagnostics.SamplePointNumbers.Count == 0
            ? string.Empty
            : $" (пример точек: {string.Join(", ", diagnostics.SamplePointNumbers)})";

        return
            $"Внимание: {diagnostics.OutOfRangeCount} точек вне диапазонов A/B. " +
            $"A: {limits.AlfaMin:0.#}..{limits.AlfaMax:0.#}°, B: {limits.BettaMin:0.#}..{limits.BettaMax:0.#}°." +
            points;
    }
}
