using Microsoft.Data.Sqlite;
using PharmacyWarehouse.Core.Data;
using PharmacyWarehouse.Core.Models;

namespace PharmacyWarehouse.Core.Services;

/// <summary>CRUD товаров с проверкой обязательных полей.</summary>
public class ProductService
{
    private readonly Database _db;
    public ProductService(Database db) => _db = db;

    public static string? Validate(Product p)
    {
        if (string.IsNullOrWhiteSpace(p.Name)) return "Наименование товара обязательно.";
        if (p.Name.Trim().Length > 200) return "Наименование слишком длинное (максимум 200 символов).";
        if (p.Price < 0) return "Цена не может быть отрицательной.";
        return null;
    }

    /// <summary>Список товаров. Поиск выполняется в C#: LIKE в SQLite не учитывает регистр кириллицы.</summary>
    public List<Product> GetAll(string? search = null)
    {
        var list = new List<Product>();
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT ProductId, Name, ActiveIngredient, Manufacturer, DosageForm, Price, RequiresPrescription
                            FROM Products ORDER BY Name";
        using var rd = cmd.ExecuteReader();
        while (rd.Read()) list.Add(Read(rd));
        if (string.IsNullOrWhiteSpace(search)) return list;
        var s = search.Trim();
        const StringComparison cmp = StringComparison.InvariantCultureIgnoreCase;
        return list.Where(p => p.Name.Contains(s, cmp) || (p.ActiveIngredient?.Contains(s, cmp) ?? false)).ToList();
    }

    public Product? Get(int id)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT ProductId, Name, ActiveIngredient, Manufacturer, DosageForm, Price, RequiresPrescription
                            FROM Products WHERE ProductId = $id";
        cmd.Parameters.AddWithValue("$id", id);
        using var rd = cmd.ExecuteReader();
        return rd.Read() ? Read(rd) : null;
    }

    public OperationResult<int> Add(Product p)
    {
        var err = Validate(p);
        if (err != null) return OperationResult<int>.Fail(err);
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO Products (Name, ActiveIngredient, Manufacturer, DosageForm, Price, RequiresPrescription)
                            VALUES ($n,$a,$m,$d,$p,$r); SELECT last_insert_rowid();";
        Fill(cmd, p);
        int id = Convert.ToInt32(cmd.ExecuteScalar());
        return OperationResult<int>.Ok(id, "Товар добавлен.");
    }

    public OperationResult Update(Product p)
    {
        var err = Validate(p);
        if (err != null) return OperationResult.Fail(err);
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"UPDATE Products SET Name=$n, ActiveIngredient=$a, Manufacturer=$m, DosageForm=$d, Price=$p,
                            RequiresPrescription=$r WHERE ProductId=$id";
        Fill(cmd, p);
        cmd.Parameters.AddWithValue("$id", p.ProductId);
        return cmd.ExecuteNonQuery() == 1 ? OperationResult.Ok("Товар сохранён.") : OperationResult.Fail("Товар не найден.");
    }

    /// <summary>Удаление запрещено, если у товара есть партии (иначе потеряется история).</summary>
    public OperationResult Delete(int id)
    {
        using var conn = _db.Open();
        using var chk = conn.CreateCommand();
        chk.CommandText = "SELECT COUNT(*) FROM Batches WHERE ProductId = $id";
        chk.Parameters.AddWithValue("$id", id);
        if (Convert.ToInt32(chk.ExecuteScalar()) > 0)
            return OperationResult.Fail("Нельзя удалить товар, у которого есть партии и история движения.");
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Products WHERE ProductId = $id";
        cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteNonQuery() == 1 ? OperationResult.Ok("Товар удалён.") : OperationResult.Fail("Товар не найден.");
    }

    private static void Fill(SqliteCommand cmd, Product p)
    {
        cmd.Parameters.AddWithValue("$n", p.Name.Trim());
        cmd.Parameters.AddWithValue("$a", (object?)p.ActiveIngredient ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$m", (object?)p.Manufacturer ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$d", (object?)p.DosageForm ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$p", (double)p.Price);
        cmd.Parameters.AddWithValue("$r", p.RequiresPrescription ? 1 : 0);
    }

    private static Product Read(SqliteDataReader rd) => new()
    {
        ProductId = rd.GetInt32(0), Name = rd.GetString(1),
        ActiveIngredient = rd.IsDBNull(2) ? null : rd.GetString(2),
        Manufacturer = rd.IsDBNull(3) ? null : rd.GetString(3),
        DosageForm = rd.IsDBNull(4) ? null : rd.GetString(4),
        Price = (decimal)rd.GetDouble(5), RequiresPrescription = rd.GetInt32(6) == 1
    };
}
