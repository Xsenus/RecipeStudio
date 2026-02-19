using System;
using System.Collections.ObjectModel;

using RecipeStudio.Desktop.Services;

namespace RecipeStudio.Desktop.ViewModels;

public sealed class DashboardViewModel : ViewModelBase
{
    private readonly RecipeRepository _repo;
    private readonly Action<long> _openRecipe;

    public ObservableCollection<RecipeCardViewModel> Recipes { get; } = new();
    public ObservableCollection<object> DashboardTiles { get; } = new();

    private readonly RecipeImportService _importService;

    public RelayCommand RefreshCommand { get; }
    public RelayCommand NewRecipeCommand { get; }
    public RelayCommand ImportRecipeCommand { get; }

    public event Action? RequestCreateRecipe;
    public event Action? RequestImportRecipe;

    public DashboardViewModel(RecipeRepository repo, RecipeImportService importService, Action<long> openRecipe)
    {
        _repo = repo;
        _importService = importService;
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

    public RecipeImportPreview PreviewImport(string path)
    {
        return _importService.Preview(path);
    }

    public bool SaveImportedRecipe(RecipeImportPreview preview, string recipeName)
    {
        if (!preview.IsSuccess || preview.HasBlockingIssues || preview.Document is null)
        {
            return false;
        }

        var finalName = recipeName.Trim();
        if (string.IsNullOrWhiteSpace(finalName))
        {
            return false;
        }

        preview.Document.RecipeCode = finalName;

        var id = _repo.Create(preview.Document);
        Refresh();
        _openRecipe(id);
        return true;
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
