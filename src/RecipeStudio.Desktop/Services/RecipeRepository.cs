using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia.Platform;
using Microsoft.Data.Sqlite;
using RecipeStudio.Desktop.Models;

namespace RecipeStudio.Desktop.Services;

/// <summary>
/// SQLite-backed storage for recipes + points.
///
/// Notes:
/// - This prototype keeps data-access code simple and explicit (no ORM).
/// - Saving strategy: replace all points for a recipe in a transaction.
/// </summary>
public sealed class RecipeRepository
{
    private readonly SettingsService _settings;
    private readonly RecipeTsvSerializer _tsv = new();

    /// <summary>
    /// We keep the DB inside the user-configurable recipes folder (so it's portable).
    /// </summary>
    private string DbPath => Path.Combine(_settings.Settings.RecipesFolder, "recipes.sqlite");

    public RecipeRepository(SettingsService settings)
    {
        _settings = settings;
        Directory.CreateDirectory(_settings.Settings.RecipesFolder);
        if (_settings.Settings.AutoCreateSampleRecipeOnEmpty)
        {
            EnsureSeedData();
        }
    }

    private SqliteConnection OpenConnection()
    {
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = DbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };

        var conn = new SqliteConnection(cs.ToString());
        conn.Open();

        // Enable FK constraints.
        using var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();

        EnsureSchema(conn);

        return conn;
    }

    private void EnsureSchema(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS recipes (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  recipe_code TEXT NOT NULL,
  container_present INTEGER NOT NULL,
  d_clamp_form REAL NOT NULL,
  d_clamp_cont REAL NOT NULL,
  created_utc TEXT NOT NULL,
  modified_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS recipe_points (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  recipe_id INTEGER NOT NULL,
  n_point INTEGER NOT NULL,
  act INTEGER NOT NULL,
  safe INTEGER NOT NULL,

  r_crd REAL NOT NULL,
  z_crd REAL NOT NULL,
  place INTEGER NOT NULL,
  hidden INTEGER NOT NULL,

  a_nozzle REAL NOT NULL,
  recommended_alfa REAL NOT NULL,
  alfa REAL NOT NULL,
  betta REAL NOT NULL,

  speed_table REAL NOT NULL,
  time_sec REAL NOT NULL,
  nozzle_speed_mm_min REAL NOT NULL,

  recommended_ice_rate REAL NOT NULL,
  ice_rate REAL NOT NULL,
  ice_grind REAL NOT NULL,
  air_pressure REAL NOT NULL,
  air_temp REAL NOT NULL,

  container INTEGER NOT NULL,
  d_clamp_form REAL NOT NULL,
  d_clamp_cont REAL NOT NULL,
  description TEXT,

  xr0 REAL NOT NULL,
  yx0 REAL NOT NULL,
  zr0 REAL NOT NULL,
  dx REAL NOT NULL,
  dy REAL NOT NULL,
  dz REAL NOT NULL,
  da REAL NOT NULL,
  ab REAL NOT NULL,
  xpuls REAL NOT NULL,
  ypuls REAL NOT NULL,
  zpuls REAL NOT NULL,
  apuls REAL NOT NULL,
  bpuls REAL NOT NULL,
  top_puls REAL NOT NULL,
  top_hz REAL NOT NULL,
  low_puls REAL NOT NULL,
  low_hz REAL NOT NULL,
  clamp_puls REAL NOT NULL,

  FOREIGN KEY(recipe_id) REFERENCES recipes(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_recipe_points_recipe_id ON recipe_points(recipe_id, n_point);
";
        cmd.ExecuteNonQuery();
    }

    private void EnsureSeedData()
    {
        // Seed only if DB is empty.
        if (GetRecipeCount() > 0)
            return;

        // 1) If there are old .csv sample recipes in the configured folder, import them.
        try
        {
            var legacyFolder = _settings.Settings.RecipesFolder;
            if (Directory.Exists(legacyFolder))
            {
                var legacyFiles = Directory.EnumerateFiles(legacyFolder, "*.csv", SearchOption.TopDirectoryOnly)
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .Take(25)
                    .ToList();

                if (legacyFiles.Count > 0)
                {
                    foreach (var file in legacyFiles)
                    {
                        var doc = _tsv.Load(file);
                        if (doc.Points.Count == 0)
                            continue;
                        doc.RecipeCode = string.IsNullOrWhiteSpace(doc.RecipeCode)
                            ? Path.GetFileNameWithoutExtension(file)
                            : doc.RecipeCode;

                        Create(doc);
                    }
                    return;
                }
            }
        }
        catch
        {
            // ignore and fallback to bundled seed
        }

        // 2) Fallback: import the bundled sample from app resources.
        try
        {
            var doc = LoadBundledSample();
            Create(doc);
        }
        catch
        {
            // 3) Last resort: minimal starter
            var starter = RecipeDocumentFactory.CreateStarter("H340_KAMA_1");
            Create(starter);
        }
    }


    public bool CreateSampleRecipe()
    {
        try
        {
            var doc = LoadBundledSample();
            if (doc.Points.Count == 0)
            {
                return false;
            }

            var baseCode = string.IsNullOrWhiteSpace(doc.RecipeCode) ? "H340_KAMA_1" : doc.RecipeCode;
            doc.RecipeCode = BuildUniqueRecipeCode(baseCode);
            Create(doc);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private RecipeDocument LoadBundledSample()
    {
        var uri = new Uri("avares://RecipeStudio.Desktop/Assets/Samples/H340_KAMA_1.csv");
        using var src = AssetLoader.Open(uri);
        using var sr = new StreamReader(src);
        var tmp = Path.Combine(_settings.AppDataRoot, "_seed_sample.tsv");
        File.WriteAllText(tmp, sr.ReadToEnd());

        var doc = _tsv.Load(tmp);
        try { File.Delete(tmp); } catch { }

        if (doc.Points.Count == 0)
            throw new InvalidOperationException("Seed sample has no points");

        doc.RecipeCode = string.IsNullOrWhiteSpace(doc.RecipeCode) ? "H340_KAMA_1" : doc.RecipeCode;
        return doc;
    }

    private string BuildUniqueRecipeCode(string baseCode)
    {
        var existing = new HashSet<string>(GetRecipes().Select(r => r.RecipeCode), StringComparer.OrdinalIgnoreCase);
        if (!existing.Contains(baseCode))
        {
            return baseCode;
        }

        var idx = 2;
        while (existing.Contains($"{baseCode}_{idx}"))
        {
            idx++;
        }

        return $"{baseCode}_{idx}";
    }

    private int GetRecipeCount()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM recipes;";
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }

    public IReadOnlyList<RecipeInfo> GetRecipes()
    {
        var list = new List<RecipeInfo>();
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT r.id,
       r.recipe_code,
       r.modified_utc,
       (SELECT COUNT(1) FROM recipe_points p WHERE p.recipe_id = r.id) AS point_count
FROM recipes r
ORDER BY r.modified_utc DESC;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetInt64(0);
            var code = reader.GetString(1);
            var modifiedUtc = DateTime.Parse(reader.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            var count = reader.GetInt32(3);
            list.Add(new RecipeInfo(id, code, modifiedUtc, count));
        }

        return list;
    }

    public RecipeDocument Load(long recipeId)
    {
        using var conn = OpenConnection();

        // Load recipe header
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT id, recipe_code, container_present, d_clamp_form, d_clamp_cont, created_utc, modified_utc
FROM recipes
WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", recipeId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            throw new InvalidOperationException($"Recipe not found: {recipeId}");

        var doc = new RecipeDocument
        {
            RecipeId = reader.GetInt64(0),
            RecipeCode = reader.GetString(1),
            ContainerPresent = reader.GetInt32(2) != 0,
            DClampForm = reader.GetDouble(3),
            DClampCont = reader.GetDouble(4),
            CreatedUtc = DateTime.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
            ModifiedUtc = DateTime.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
        };

        // Load points
        using var cmd2 = conn.CreateCommand();
        cmd2.CommandText = @"
SELECT
  n_point, act, safe,
  r_crd, z_crd, place, hidden,
  a_nozzle, recommended_alfa, alfa, betta,
  speed_table, time_sec, nozzle_speed_mm_min,
  recommended_ice_rate, ice_rate, ice_grind, air_pressure, air_temp,
  container, d_clamp_form, d_clamp_cont, description,
  xr0, yx0, zr0, dx, dy, dz, da, ab,
  xpuls, ypuls, zpuls, apuls, bpuls,
  top_puls, top_hz, low_puls, low_hz, clamp_puls
FROM recipe_points
WHERE recipe_id = $id
ORDER BY n_point;";
        cmd2.Parameters.AddWithValue("$id", recipeId);

        using var r2 = cmd2.ExecuteReader();
        while (r2.Read())
        {
            var p = new RecipePoint
            {
                RecipeCode = doc.RecipeCode,
                NPoint = r2.GetInt32(0),
                Act = r2.GetInt32(1) != 0,
                Safe = r2.GetInt32(2) != 0,

                RCrd = r2.GetDouble(3),
                ZCrd = r2.GetDouble(4),
                Place = r2.GetInt32(5),
                Hidden = r2.GetInt32(6) != 0,

                ANozzle = r2.GetDouble(7),
                RecommendedAlfa = r2.GetDouble(8),
                Alfa = r2.GetDouble(9),
                Betta = r2.GetDouble(10),

                SpeedTable = r2.GetDouble(11),
                TimeSec = r2.GetDouble(12),
                NozzleSpeedMmMin = r2.GetDouble(13),

                RecommendedIceRate = r2.GetDouble(14),
                IceRate = r2.GetDouble(15),
                IceGrind = r2.GetDouble(16),
                AirPressure = r2.GetDouble(17),
                AirTemp = r2.GetDouble(18),

                Container = r2.GetInt32(19) != 0,
                DClampForm = r2.GetDouble(20),
                DClampCont = r2.GetDouble(21),
                Description = r2.IsDBNull(22) ? null : r2.GetString(22),

                Xr0 = r2.GetDouble(23),
                Yx0 = r2.GetDouble(24),
                Zr0 = r2.GetDouble(25),
                DX = r2.GetDouble(26),
                DY = r2.GetDouble(27),
                DZ = r2.GetDouble(28),
                DA = r2.GetDouble(29),
                AB = r2.GetDouble(30),

                XPuls = r2.GetDouble(31),
                YPuls = r2.GetDouble(32),
                ZPuls = r2.GetDouble(33),
                APuls = r2.GetDouble(34),
                BPuls = r2.GetDouble(35),

                TopPuls = r2.GetDouble(36),
                TopHz = r2.GetDouble(37),
                LowPuls = r2.GetDouble(38),
                LowHz = r2.GetDouble(39),
                ClampPuls = r2.GetDouble(40),
            };

            doc.Points.Add(p);
        }

        return doc;
    }

    public long Create(RecipeDocument doc)
    {
        // Insert header
        using var conn = OpenConnection();
        using var tx = conn.BeginTransaction();

        var now = DateTime.UtcNow;
        var created = doc.CreatedUtc == default ? now : doc.CreatedUtc;
        var modified = now;

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
INSERT INTO recipes(recipe_code, container_present, d_clamp_form, d_clamp_cont, created_utc, modified_utc)
VALUES ($code, $cont, $df, $dc, $created, $modified);
SELECT last_insert_rowid();";

            cmd.Parameters.AddWithValue("$code", doc.RecipeCode);
            cmd.Parameters.AddWithValue("$cont", doc.ContainerPresent ? 1 : 0);
            cmd.Parameters.AddWithValue("$df", doc.DClampForm);
            cmd.Parameters.AddWithValue("$dc", doc.DClampCont);
            cmd.Parameters.AddWithValue("$created", created.ToString("O", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$modified", modified.ToString("O", CultureInfo.InvariantCulture));
            var id = (long)(cmd.ExecuteScalar() ?? 0L);
            doc.RecipeId = id;
            doc.CreatedUtc = created;
            doc.ModifiedUtc = modified;
        }

        ReplacePoints(conn, tx, doc);

        tx.Commit();
        return doc.RecipeId;
    }

    public void Save(RecipeDocument doc)
    {
        if (doc.RecipeId <= 0)
        {
            Create(doc);
            return;
        }

        using var conn = OpenConnection();
        using var tx = conn.BeginTransaction();

        doc.ModifiedUtc = DateTime.UtcNow;

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
UPDATE recipes
SET recipe_code = $code,
    container_present = $cont,
    d_clamp_form = $df,
    d_clamp_cont = $dc,
    modified_utc = $modified
WHERE id = $id;";

            cmd.Parameters.AddWithValue("$id", doc.RecipeId);
            cmd.Parameters.AddWithValue("$code", doc.RecipeCode);
            cmd.Parameters.AddWithValue("$cont", doc.ContainerPresent ? 1 : 0);
            cmd.Parameters.AddWithValue("$df", doc.DClampForm);
            cmd.Parameters.AddWithValue("$dc", doc.DClampCont);
            cmd.Parameters.AddWithValue("$modified", doc.ModifiedUtc.ToString("O", CultureInfo.InvariantCulture));
            cmd.ExecuteNonQuery();
        }

        ReplacePoints(conn, tx, doc);

        tx.Commit();
    }

    public void Delete(long recipeId)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM recipes WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", recipeId);
        cmd.ExecuteNonQuery();
    }

    private static void ReplacePoints(SqliteConnection conn, SqliteTransaction tx, RecipeDocument doc)
    {
        // Delete old
        using (var del = conn.CreateCommand())
        {
            del.Transaction = tx;
            del.CommandText = "DELETE FROM recipe_points WHERE recipe_id = $id;";
            del.Parameters.AddWithValue("$id", doc.RecipeId);
            del.ExecuteNonQuery();
        }

        // Insert all (ordered)
        using var ins = conn.CreateCommand();
        ins.Transaction = tx;
        ins.CommandText = @"
INSERT INTO recipe_points(
  recipe_id, n_point, act, safe,
  r_crd, z_crd, place, hidden,
  a_nozzle, recommended_alfa, alfa, betta,
  speed_table, time_sec, nozzle_speed_mm_min,
  recommended_ice_rate, ice_rate, ice_grind, air_pressure, air_temp,
  container, d_clamp_form, d_clamp_cont, description,
  xr0, yx0, zr0, dx, dy, dz, da, ab,
  xpuls, ypuls, zpuls, apuls, bpuls,
  top_puls, top_hz, low_puls, low_hz, clamp_puls
)
VALUES(
  $recipe_id, $n_point, $act, $safe,
  $r_crd, $z_crd, $place, $hidden,
  $a_nozzle, $recommended_alfa, $alfa, $betta,
  $speed_table, $time_sec, $nozzle_speed,
  $recommended_ice_rate, $ice_rate, $ice_grind, $air_pressure, $air_temp,
  $container, $d_form, $d_cont, $description,
  $xr0, $yx0, $zr0, $dx, $dy, $dz, $da, $ab,
  $xpuls, $ypuls, $zpuls, $apuls, $bpuls,
  $top_puls, $top_hz, $low_puls, $low_hz, $clamp_puls
);";

        // Pre-create parameters once
        ins.Parameters.Add("$recipe_id", SqliteType.Integer);
        ins.Parameters.Add("$n_point", SqliteType.Integer);
        ins.Parameters.Add("$act", SqliteType.Integer);
        ins.Parameters.Add("$safe", SqliteType.Integer);
        ins.Parameters.Add("$r_crd", SqliteType.Real);
        ins.Parameters.Add("$z_crd", SqliteType.Real);
        ins.Parameters.Add("$place", SqliteType.Integer);
        ins.Parameters.Add("$hidden", SqliteType.Integer);
        ins.Parameters.Add("$a_nozzle", SqliteType.Real);
        ins.Parameters.Add("$recommended_alfa", SqliteType.Real);
        ins.Parameters.Add("$alfa", SqliteType.Real);
        ins.Parameters.Add("$betta", SqliteType.Real);
        ins.Parameters.Add("$speed_table", SqliteType.Real);
        ins.Parameters.Add("$time_sec", SqliteType.Real);
        ins.Parameters.Add("$nozzle_speed", SqliteType.Real);
        ins.Parameters.Add("$recommended_ice_rate", SqliteType.Real);
        ins.Parameters.Add("$ice_rate", SqliteType.Real);
        ins.Parameters.Add("$ice_grind", SqliteType.Real);
        ins.Parameters.Add("$air_pressure", SqliteType.Real);
        ins.Parameters.Add("$air_temp", SqliteType.Real);
        ins.Parameters.Add("$container", SqliteType.Integer);
        ins.Parameters.Add("$d_form", SqliteType.Real);
        ins.Parameters.Add("$d_cont", SqliteType.Real);
        ins.Parameters.Add("$description", SqliteType.Text);
        ins.Parameters.Add("$xr0", SqliteType.Real);
        ins.Parameters.Add("$yx0", SqliteType.Real);
        ins.Parameters.Add("$zr0", SqliteType.Real);
        ins.Parameters.Add("$dx", SqliteType.Real);
        ins.Parameters.Add("$dy", SqliteType.Real);
        ins.Parameters.Add("$dz", SqliteType.Real);
        ins.Parameters.Add("$da", SqliteType.Real);
        ins.Parameters.Add("$ab", SqliteType.Real);
        ins.Parameters.Add("$xpuls", SqliteType.Real);
        ins.Parameters.Add("$ypuls", SqliteType.Real);
        ins.Parameters.Add("$zpuls", SqliteType.Real);
        ins.Parameters.Add("$apuls", SqliteType.Real);
        ins.Parameters.Add("$bpuls", SqliteType.Real);
        ins.Parameters.Add("$top_puls", SqliteType.Real);
        ins.Parameters.Add("$top_hz", SqliteType.Real);
        ins.Parameters.Add("$low_puls", SqliteType.Real);
        ins.Parameters.Add("$low_hz", SqliteType.Real);
        ins.Parameters.Add("$clamp_puls", SqliteType.Real);

        foreach (var p in doc.Points.OrderBy(x => x.NPoint))
        {
            // Keep recipe-level fields in sync
            p.RecipeCode = doc.RecipeCode;
            p.DClampForm = doc.DClampForm;
            p.DClampCont = doc.DClampCont;
            p.Container = doc.ContainerPresent;

            ins.Parameters["$recipe_id"].Value = doc.RecipeId;
            ins.Parameters["$n_point"].Value = p.NPoint;
            ins.Parameters["$act"].Value = p.Act ? 1 : 0;
            ins.Parameters["$safe"].Value = p.Safe ? 1 : 0;
            ins.Parameters["$r_crd"].Value = p.RCrd;
            ins.Parameters["$z_crd"].Value = p.ZCrd;
            ins.Parameters["$place"].Value = p.Place;
            ins.Parameters["$hidden"].Value = p.Hidden ? 1 : 0;
            ins.Parameters["$a_nozzle"].Value = p.ANozzle;
            ins.Parameters["$recommended_alfa"].Value = p.RecommendedAlfa;
            ins.Parameters["$alfa"].Value = p.Alfa;
            ins.Parameters["$betta"].Value = p.Betta;
            ins.Parameters["$speed_table"].Value = p.SpeedTable;
            ins.Parameters["$time_sec"].Value = p.TimeSec;
            ins.Parameters["$nozzle_speed"].Value = p.NozzleSpeedMmMin;
            ins.Parameters["$recommended_ice_rate"].Value = p.RecommendedIceRate;
            ins.Parameters["$ice_rate"].Value = p.IceRate;
            ins.Parameters["$ice_grind"].Value = p.IceGrind;
            ins.Parameters["$air_pressure"].Value = p.AirPressure;
            ins.Parameters["$air_temp"].Value = p.AirTemp;
            ins.Parameters["$container"].Value = p.Container ? 1 : 0;
            ins.Parameters["$d_form"].Value = p.DClampForm;
            ins.Parameters["$d_cont"].Value = p.DClampCont;
            ins.Parameters["$description"].Value = p.Description ?? "";
            ins.Parameters["$xr0"].Value = p.Xr0;
            ins.Parameters["$yx0"].Value = p.Yx0;
            ins.Parameters["$zr0"].Value = p.Zr0;
            ins.Parameters["$dx"].Value = p.DX;
            ins.Parameters["$dy"].Value = p.DY;
            ins.Parameters["$dz"].Value = p.DZ;
            ins.Parameters["$da"].Value = p.DA;
            ins.Parameters["$ab"].Value = p.AB;
            ins.Parameters["$xpuls"].Value = p.XPuls;
            ins.Parameters["$ypuls"].Value = p.YPuls;
            ins.Parameters["$zpuls"].Value = p.ZPuls;
            ins.Parameters["$apuls"].Value = p.APuls;
            ins.Parameters["$bpuls"].Value = p.BPuls;
            ins.Parameters["$top_puls"].Value = p.TopPuls;
            ins.Parameters["$top_hz"].Value = p.TopHz;
            ins.Parameters["$low_puls"].Value = p.LowPuls;
            ins.Parameters["$low_hz"].Value = p.LowHz;
            ins.Parameters["$clamp_puls"].Value = p.ClampPuls;

            ins.ExecuteNonQuery();
        }
    }
}

public sealed record RecipeInfo(long Id, string RecipeCode, DateTime ModifiedUtc, int PointCount);
