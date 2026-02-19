using System;
using System.Collections.ObjectModel;
using System.IO;
using RecipeStudio.Desktop.Models;
using RecipeStudio.Desktop.Services;

namespace RecipeStudio.Desktop.ViewModels;

public sealed class DashboardViewModel : ViewModelBase
{
    private readonly RecipeRepository _repo;
    private readonly Action<long> _openRecipe;

    public ObservableCollection<RecipeCardViewModel> Recipes { get; } = new();
    public ObservableCollection<object> DashboardTiles { get; } = new();

    private readonly RecipeExcelService _excel;
    private readonly RecipeTsvSerializer _tsv;

    public RelayCommand RefreshCommand { get; }
    public RelayCommand NewRecipeCommand { get; }
    public RelayCommand ImportRecipeCommand { get; }

    public event Action? RequestCreateRecipe;
    public event Action? RequestImportRecipe;

    public DashboardViewModel(RecipeRepository repo, RecipeExcelService excel, RecipeTsvSerializer tsv, Action<long> openRecipe)
    {
        _repo = repo;
        _excel = excel;
        _tsv = tsv;
        _openRecipe = openRecipe;

        RefreshCommand = new RelayCommand(Refresh);
        NewRecipeCommand = new RelayCommand(() => RequestCreateRecipe?.Invoke());
        ImportRecipeCommand = new RelayCommand(() => RequestImportRecipe?.Invoke());

        Refresh();
    }

    public void Refresh()
    {
        Recipes.Clear();
        DashboardTiles.Clear();

        foreach (var r in _repo.GetRecipes())
        {
            var card = new RecipeCardViewModel(r, _openRecipe);
            Recipes.Add(card);
            DashboardTiles.Add(card);
        }

        DashboardTiles.Add(new CreateRecipeTileViewModel(NewRecipeCommand));
        RaisePropertyChanged(nameof(Recipes));
    }

    public void DeleteRecipe(long id)
    {
        _repo.Delete(id);
        Refresh();
    }

    public void ImportFromFile(string path)
    {
        var normalizedPath = path.Trim();
        if (string.IsNullOrWhiteSpace(normalizedPath) || !File.Exists(normalizedPath))
        {
            return;
        }

        var extension = Path.GetExtension(normalizedPath).ToLowerInvariant();
        RecipeDocument doc = extension switch
        {
            ".xlsx" => _excel.Import(normalizedPath),
            ".csv" or ".tsv" => _tsv.Load(normalizedPath),
            _ => throw new InvalidOperationException($"Неподдерживаемый формат: {extension}")
        };

        if (string.IsNullOrWhiteSpace(doc.RecipeCode))
        {
            doc.RecipeCode = Path.GetFileNameWithoutExtension(normalizedPath);
        }

        var id = _repo.Create(doc);
        Refresh();
        _openRecipe(id);
    }

    public void CreateNewRecipe(string recipeCode)
    {
        var code = recipeCode.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        var doc = RecipeDocumentFactory.CreateStarter(code);
        doc.RecipeCode = code;
        var id = _repo.Create(doc);

        Refresh();
        _openRecipe(id);
    }
}

public sealed class RecipeCardViewModel : ViewModelBase
{
    private readonly Action<long> _openRecipe;

    public long Id { get; }
    public string Name { get; }
    public DateTime LastModified { get; }
    public int PointCount { get; }

    public RelayCommand OpenCommand { get; }

    public RecipeCardViewModel(RecipeInfo info, Action<long> openRecipe)
    {
        Id = info.Id;
        Name = info.RecipeCode;
        LastModified = info.ModifiedUtc.ToLocalTime();
        PointCount = info.PointCount;
        _openRecipe = openRecipe;

        OpenCommand = new RelayCommand(() => _openRecipe(Id));
    }
}

public sealed class CreateRecipeTileViewModel : ViewModelBase
{
    public RelayCommand CreateCommand { get; }

    public CreateRecipeTileViewModel(RelayCommand createCommand)
    {
        CreateCommand = createCommand;
    }
}
