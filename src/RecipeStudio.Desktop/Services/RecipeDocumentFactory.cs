using RecipeStudio.Desktop.Models;

namespace RecipeStudio.Desktop.Services;

public static class RecipeDocumentFactory
{
    public static RecipeDocument CreateStarter(string recipeCode)
    {
        var doc = new RecipeDocument
        {
            RecipeCode = recipeCode,
            DClampForm = 800,
            DClampCont = 1600,
            ContainerPresent = true,
        };

        // A tiny starter path (bottom + top) so that the plot/table are not empty.
        doc.Points.Add(new RecipePoint
        {
            RecipeCode = recipeCode,
            NPoint = 1,
            Act = true,
            Safe = false,
            RCrd = 364,
            ZCrd = 354,
            Place = 0,
            Hidden = false,
            ANozzle = 20,
            Alfa = -30,
            Betta = 12,
            SpeedTable = 3,
            IceRate = 100,
            IceGrind = 0,
            AirPressure = 5,
            AirTemp = 0,
            Container = true,
            DClampForm = 800,
            DClampCont = 1600,
            Description = "0",
        });

        doc.Points.Add(new RecipePoint
        {
            RecipeCode = recipeCode,
            NPoint = 2,
            Act = true,
            Safe = false,
            RCrd = 377,
            ZCrd = 337,
            Place = 0,
            Hidden = false,
            ANozzle = 20,
            Alfa = -15,
            Betta = 13,
            SpeedTable = 3,
            IceRate = 103.6,
            IceGrind = 0,
            AirPressure = 5,
            AirTemp = 0,
            Container = true,
            DClampForm = 800,
            DClampCont = 1600,
            Description = "0",
        });

        doc.Points.Add(new RecipePoint
        {
            RecipeCode = recipeCode,
            NPoint = 3,
            Act = true,
            Safe = false,
            RCrd = 334,
            ZCrd = 99,
            Place = 1,
            Hidden = false,
            ANozzle = 20,
            Alfa = 104,
            Betta = 90,
            SpeedTable = 3,
            IceRate = 91.8,
            IceGrind = 0,
            AirPressure = 5,
            AirTemp = 0,
            Container = true,
            DClampForm = 800,
            DClampCont = 1600,
            Description = "0",
        });

        doc.Points.Add(new RecipePoint
        {
            RecipeCode = recipeCode,
            NPoint = 4,
            Act = true,
            Safe = true,
            RCrd = 311,
            ZCrd = 99,
            Place = 1,
            Hidden = false,
            ANozzle = 20,
            Alfa = 90,
            Betta = 90,
            SpeedTable = 3,
            IceRate = 85.4,
            IceGrind = 0,
            AirPressure = 5,
            AirTemp = 0,
            Container = true,
            DClampForm = 800,
            DClampCont = 1600,
            Description = "0",
        });

        return doc;
    }
}
