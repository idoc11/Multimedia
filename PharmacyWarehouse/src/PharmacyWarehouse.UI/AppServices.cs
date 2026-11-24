using PharmacyWarehouse.Core.Data;
using PharmacyWarehouse.Core.Services;

namespace PharmacyWarehouse.UI;

/// <summary>Контейнер сервисов: форм не создают БД сами, а получают готовые сервисы.</summary>
public sealed class AppServices
{
    public Database Db { get; }
    public AuthService Auth { get; }
    public ProductService Products { get; }
    public StockService Stock { get; }
    public ReportService Reports { get; }

    public AppServices(string dbPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        Db = new Database(dbPath);
        Db.Initialize();
        Auth = new AuthService(Db);
        Products = new ProductService(Db);
        Stock = new StockService(Db);
        Reports = new ReportService(Db);
    }
}
