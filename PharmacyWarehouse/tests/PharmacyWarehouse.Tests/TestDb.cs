using Microsoft.Data.Sqlite;
using PharmacyWarehouse.Core.Data;
using PharmacyWarehouse.Core.Models;
using PharmacyWarehouse.Core.Services;
using Xunit;

namespace PharmacyWarehouse.Tests;

/// <summary>Временная файловая БД на каждый тест (":memory:" не подходит: у каждого подключения своя БД).</summary>
public sealed class TestDb : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"pharm_{Guid.NewGuid():N}.db");
    public Database Db { get; }
    public DateTime Today { get; } = new DateTime(2026, 6, 1);
    public AuthService Auth { get; }
    public ProductService Products { get; }
    public StockService Stock { get; }
    public ReportService Reports { get; }
    public User Admin { get; }

    public TestDb()
    {
        Db = new Database(_path);
        Db.Initialize();
        Auth = new AuthService(Db);
        Products = new ProductService(Db);
        Stock = new StockService(Db, () => Today);
        Reports = new ReportService(Db, () => Today);
        Admin = Auth.Login(Database.DefaultAdminLogin, Database.DefaultAdminPassword)!;
    }

    public int AddProduct(string name = "Парацетамол", decimal price = 100m) =>
        Products.Add(new Product { Name = name, Price = price, DosageForm = "таблетки" }).Value;

    /// <summary>Создаёт партию. Для просроченной (daysToExpiry &lt; 0) срок «состаривается» прямым SQL —
    /// сервис справедливо не принимает уже просроченный товар.</summary>
    public int AddBatch(int productId, int qty, string number = "A1", int daysToExpiry = 365)
    {
        var r = Stock.AddBatch(productId, number, Today.AddDays(Math.Max(daysToExpiry, 1)), qty, Admin.UserId);
        Assert.True(r.Success, r.Message);
        if (daysToExpiry < 1)
        {
            using var conn = Db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Batches SET ExpiryDate=$d WHERE BatchId=$id";
            cmd.Parameters.AddWithValue("$d", Database.ToDb(Today.AddDays(daysToExpiry)));
            cmd.Parameters.AddWithValue("$id", r.Value);
            cmd.ExecuteNonQuery();
        }
        return r.Value;
    }

    public int QuantityOf(int batchId) => Stock.GetBatches().Single(b => b.BatchId == batchId).Quantity;

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var f in new[] { _path, _path + "-wal", _path + "-shm", _path + "-journal" })
            if (File.Exists(f)) File.Delete(f);
    }
}
