using System;
using System.Linq;
using System.Text;
using RecipeStudio.Desktop.Models;

namespace RecipeStudio.Desktop.Services;

public sealed class RecipeImportService
{
    private readonly RecipeExcelService _excel;
    private readonly RecipeTsvSerializer _tsv;

    public RecipeImportService(RecipeExcelService excel, RecipeTsvSerializer tsv)
    {
        _excel = excel;
        _tsv = tsv;
    }

    public RecipeImportPreview Preview(string path)
    {
        var extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
        var fileName = System.IO.Path.GetFileName(path);

        try
        {
            RecipeDocument doc;
            string diagnostics;
            var hasBlockingIssues = false;

            if (extension == ".xlsx")
            {
                var result = _excel.ImportWithReport(path);
                doc = result.Document;
                diagnostics = BuildImportDiagnosticsSummary(result.Report);
                hasBlockingIssues = result.Report.MissingRequiredColumns.Count > 0 || result.Report.DuplicateHeaders.Count > 0;
            }
            else if (extension is ".csv" or ".tsv")
            {
                doc = _tsv.Load(path);
                diagnostics = "Импорт CSV/TSV: файл прочитан.";
            }
            else
            {
                return RecipeImportPreview.Error(fileName, extension, "Неподдерживаемый формат файла. Разрешены: .xlsx, .csv, .tsv");
            }

            if (doc.Points.Count == 0)
            {
                return RecipeImportPreview.Error(fileName, extension, "Файл прочитан, но точки не найдены.");
            }

            var suggestedName = string.IsNullOrWhiteSpace(doc.RecipeCode)
                ? System.IO.Path.GetFileNameWithoutExtension(path)
                : doc.RecipeCode;

            return new RecipeImportPreview(
                true,
                hasBlockingIssues ? "Импорт выполнен с ошибками" : "Импорт выполнен успешно",
                fileName,
                extension,
                doc.Points.Count,
                suggestedName,
                diagnostics,
                doc,
                hasBlockingIssues);
        }
        catch (Exception ex)
        {
            return RecipeImportPreview.Error(fileName, extension, $"Ошибка импорта: {ex.Message}");
        }
    }

    private static string BuildImportDiagnosticsSummary(RecipeImportReport report)
    {
        if (!report.HasIssues && report.AliasHits.Count == 0)
            return "Импорт Excel: все колонки распознаны без алиасов.";

        var parts = new System.Collections.Generic.List<string>();

        if (report.AliasHits.Count > 0)
        {
            var aliasDetails = string.Join(", ", report.AliasHits.Select(a => $"{a.Alias}→{a.Canonical}"));
            parts.Add($"алиасы: {report.AliasHits.Count} ({aliasDetails})");
        }

        if (report.UnknownHeaders.Count > 0)
            parts.Add($"неизвестные колонки: {string.Join(", ", report.UnknownHeaders)}");

        if (report.MissingRequiredColumns.Count > 0)
            parts.Add($"отсутствуют обязательные: {string.Join(", ", report.MissingRequiredColumns)}");

        if (report.DuplicateHeaders.Count > 0)
            parts.Add($"дубликаты хедеров: {string.Join(", ", report.DuplicateHeaders)}");

        var builder = new StringBuilder("Импорт Excel: ");
        builder.Append(string.Join("; ", parts));
        return builder.ToString();
    }
}

public sealed record RecipeImportPreview(
    bool IsSuccess,
    string Status,
    string FileName,
    string Extension,
    int PointCount,
    string SuggestedRecipeName,
    string Diagnostics,
    RecipeDocument? Document,
    bool HasBlockingIssues)
{
    public static RecipeImportPreview Error(string fileName, string extension, string diagnostics)
        => new(false, "Импорт не выполнен", fileName, extension, 0, "", diagnostics, null, true);
}
