using Microsoft.Data.Sqlite;
using PharmacyWarehouse.Core.Models;
using PharmacyWarehouse.Core.Services;

namespace PharmacyWarehouse.Core.Data;

/// <summary>Доступ к SQLite: строка подключения, создание схемы, начальные данные.</summary>
public sealed class Database
{
    public const string DateFormat = "yyyy-MM-dd";
    public const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";
    public const string DefaultAdminLogin = "admin";
    public const string DefaultAdminPassword = "Admin123!";

    public string ConnectionString { get; }

    public Database(string filePath)
    {
        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = filePath,
            ForeignKeys = true,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        return conn;
    }

    private const string Schema = @"
CREATE TABLE IF NOT EXISTS Roles (
    RoleId   INTEGER PRIMARY KEY AUTOINCREMENT,
    RoleName TEXT NOT NULL UNIQUE
);
CREATE TABLE IF NOT EXISTS Users (
    UserId       INTEGER PRIMARY KEY AUTOINCREMENT,
    Login        TEXT NOT NULL UNIQUE COLLATE NOCASE,
    PasswordHash TEXT NOT NULL,
    Salt         TEXT NOT NULL,
    FullName     TEXT NOT NULL,
    RoleId       INTEGER NOT NULL,
    IsActive     INTEGER NOT NULL DEFAULT 1 CHECK (IsActive IN (0,1)),
    FOREIGN KEY (RoleId) REFERENCES Roles(RoleId)
);
CREATE TABLE IF NOT EXISTS Products (
    ProductId            INTEGER PRIMARY KEY AUTOINCREMENT,
    Name                 TEXT NOT NULL,
    ActiveIngredient     TEXT,
    Manufacturer         TEXT,
    DosageForm           TEXT,
    Price                REAL NOT NULL CHECK (Price >= 0),
    RequiresPrescription INTEGER NOT NULL DEFAULT 0 CHECK (RequiresPrescription IN (0,1))
);
CREATE TABLE IF NOT EXISTS Batches (
    BatchId     INTEGER PRIMARY KEY AUTOINCREMENT,
    ProductId   INTEGER NOT NULL,
    BatchNumber TEXT NOT NULL,
    Quantity    INTEGER NOT NULL DEFAULT 0 CHECK (Quantity >= 0),
    ExpiryDate  TEXT NOT NULL,
    FOREIGN KEY (ProductId) REFERENCES Products(ProductId),
    UNIQUE (ProductId, BatchNumber)
);
CREATE TABLE IF NOT EXISTS StockOperations (
    OperationId   INTEGER PRIMARY KEY AUTOINCREMENT,
    BatchId       INTEGER NOT NULL,
    OperationType TEXT NOT NULL CHECK (OperationType IN ('IN', 'OUT')),
    Quantity      INTEGER NOT NULL CHECK (Quantity > 0),
    OperationDate TEXT NOT NULL,
    UserId        INTEGER NOT NULL,
    Comment       TEXT,
    FOREIGN KEY (BatchId) REFERENCES Batches(BatchId),
    FOREIGN KEY (UserId)  REFERENCES Users(UserId)
);
CREATE INDEX IF NOT EXISTS IX_Batches_Product ON Batches(ProductId);
CREATE INDEX IF NOT EXISTS IX_Batches_Expiry  ON Batches(ExpiryDate);
CREATE INDEX IF NOT EXISTS IX_Ops_Batch       ON StockOperations(BatchId);
";

    /// <summary>Создаёт таблицы (если их нет), роли и учётную запись администратора по умолчанию.</summary>
    public void Initialize()
    {
        using var conn = Open();
        using (var cmd = conn.CreateCommand()) { cmd.CommandText = Schema; cmd.ExecuteNonQuery(); }

        foreach (var role in new[] { Roles.Admin, Roles.Pharmacist })
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO Roles (RoleName) VALUES ($r)";
            cmd.Parameters.AddWithValue("$r", role);
            cmd.ExecuteNonQuery();
        }

        using var count = conn.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM Users";
        if (Convert.ToInt32(count.ExecuteScalar()) == 0)
        {
            var (hash, salt) = PasswordHasher.CreateHash(DefaultAdminPassword);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO Users (Login, PasswordHash, Salt, FullName, RoleId)
                                VALUES ($l, $h, $s, $f, (SELECT RoleId FROM Roles WHERE RoleName = $r))";
            cmd.Parameters.AddWithValue("$l", DefaultAdminLogin);
            cmd.Parameters.AddWithValue("$h", hash);
            cmd.Parameters.AddWithValue("$s", salt);
            cmd.Parameters.AddWithValue("$f", "Администратор");
            cmd.Parameters.AddWithValue("$r", Roles.Admin);
            cmd.ExecuteNonQuery();
        }
    }

    public static string ToDb(DateTime d) => d.ToString(DateFormat, System.Globalization.CultureInfo.InvariantCulture);
    public static DateTime ParseDate(string s) =>
        DateTime.ParseExact(s, DateFormat, System.Globalization.CultureInfo.InvariantCulture);
    public static DateTime ParseDateTime(string s) =>
        DateTime.ParseExact(s, DateTimeFormat, System.Globalization.CultureInfo.InvariantCulture);
}
