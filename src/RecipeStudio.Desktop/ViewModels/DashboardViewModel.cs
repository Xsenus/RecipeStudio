using System;
using System.Collections.ObjectModel;
using System.Linq;
using RecipeStudio.Desktop.Services;

namespace RecipeStudio.Desktop.ViewModels;

public sealed class DashboardViewModel : ViewModelBase
{
    private readonly RecipeRepository _repo;
    private readonly Action<long> _openRecipe;

    public ObservableCollection<RecipeCardViewModel> Recipes { get; } = new();

    public RelayCommand RefreshCommand { get; }

    public RelayCommand NewRecipeCommand { get; }

    public DashboardViewModel(RecipeRepository repo, Action<long> openRecipe)
    {
        _repo = repo;
        _openRecipe = openRecipe;

        RefreshCommand = new RelayCommand(Refresh);
        NewRecipeCommand = new RelayCommand(CreateNewRecipe);

        Refresh();
    }

    public void Refresh()
    {
        Recipes.Clear();

        foreach (var r in _repo.GetRecipes())
        {
            Recipes.Add(new RecipeCardViewModel(r, _openRecipe, _repo, Refresh));
        }
    }

    private void CreateNewRecipe()
    {
        var code = $"recipe_{DateTime.Now:yyyyMMdd_HHmmss}";

        // Create starter and store in DB
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
    private readonly RecipeRepository _repo;
    private readonly Action _refresh;

    public long Id { get; }
    public string Name { get; }
    public DateTime LastModified { get; }
    public int PointCount { get; }

    public RelayCommand OpenCommand { get; }
    public RelayCommand DeleteCommand { get; }

    public RecipeCardViewModel(RecipeInfo info, Action<long> openRecipe, RecipeRepository repo, Action refresh)
    {
        Id = info.Id;
        Name = info.RecipeCode;
        LastModified = info.ModifiedUtc.ToLocalTime();
        PointCount = info.PointCount;
        _openRecipe = openRecipe;
        _repo = repo;
        _refresh = refresh;

        OpenCommand = new RelayCommand(() => _openRecipe(Id));
        DeleteCommand = new RelayCommand(Delete);
    }

    private void Delete()
    {
        _repo.Delete(Id);
        _refresh();
    }
}
