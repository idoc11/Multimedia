using Microsoft.Data.Sqlite;
using PharmacyWarehouse.Core.Data;
using PharmacyWarehouse.Core.Models;

namespace PharmacyWarehouse.Core.Services;

/// <summary>Партии и складские операции (приход/расход). Все изменения — в одной транзакции.</summary>
public class StockService
{
    public const string In = "IN";
    public const string Out = "OUT";
    private readonly Database _db;
    private readonly Func<DateTime> _today;

    public StockService(Database db, Func<DateTime>? today = null)
    {
        _db = db;
        _today = today ?? (() => DateTime.Today);
    }

    public List<Batch> GetBatches(int? productId = null, bool onlyInStock = false)
    {
        var list = new List<Batch>();
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT b.BatchId, b.ProductId, p.Name, b.BatchNumber, b.Quantity, b.ExpiryDate
                            FROM Batches b JOIN Products p ON p.ProductId = b.ProductId
                            WHERE ($pid IS NULL OR b.ProductId = $pid) AND ($stock = 0 OR b.Quantity > 0)
                            ORDER BY p.Name, b.ExpiryDate";
        cmd.Parameters.AddWithValue("$pid", productId.HasValue ? productId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("$stock", onlyInStock ? 1 : 0);
        using var rd = cmd.ExecuteReader();
        while (rd.Read())
            list.Add(new Batch
            {
                BatchId = rd.GetInt32(0), ProductId = rd.GetInt32(1), ProductName = rd.GetString(2),
                BatchNumber = rd.GetString(3), Quantity = rd.GetInt32(4), ExpiryDate = Database.ParseDate(rd.GetString(5))
            });
        return list;
    }

    /// <summary>Создаёт партию; если указан начальный остаток — сразу записывает приход.</summary>
    public OperationResult<int> AddBatch(int productId, string batchNumber, DateTime expiryDate, int initialQuantity, int userId)
    {
        batchNumber = (batchNumber ?? "").Trim();
        if (batchNumber.Length == 0) return OperationResult<int>.Fail("Укажите номер партии (серии).");
        if (initialQuantity < 0) return OperationResult<int>.Fail("Начальное количество не может быть отрицательным.");
        if (expiryDate.Date < _today().Date) return OperationResult<int>.Fail("Срок годности партии уже истёк — принять её на склад нельзя.");

        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            using (var chk = conn.CreateCommand())
            {
                chk.Transaction = tx;
                chk.CommandText = "SELECT COUNT(*) FROM Products WHERE ProductId = $id";
                chk.Parameters.AddWithValue("$id", productId);
                if (Convert.ToInt32(chk.ExecuteScalar()) == 0) return OperationResult<int>.Fail("Товар не найден.");
            }
            int batchId;
            using (var ins = conn.CreateCommand())
            {
                ins.Transaction = tx;
                ins.CommandText = @"INSERT INTO Batches (ProductId, BatchNumber, Quantity, ExpiryDate)
                                    VALUES ($p, $n, 0, $e); SELECT last_insert_rowid();";
                ins.Parameters.AddWithValue("$p", productId);
                ins.Parameters.AddWithValue("$n", batchNumber);
                ins.Parameters.AddWithValue("$e", Database.ToDb(expiryDate));
                batchId = Convert.ToInt32(ins.ExecuteScalar());
            }
            if (initialQuantity > 0)
            {
                var r = Apply(conn, tx, batchId, In, initialQuantity, userId, "Начальный остаток при создании партии");
                if (!r.Success) { tx.Rollback(); return OperationResult<int>.Fail(r.Message); }
            }
            tx.Commit();
            return OperationResult<int>.Ok(batchId, "Партия добавлена.");
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            tx.Rollback();
            return OperationResult<int>.Fail("Партия с таким номером у этого товара уже существует.");
        }
    }

    /// <summary>Оформление прихода (IN) или расхода (OUT) по партии.</summary>
    public OperationResult RegisterOperation(int batchId, string type, int quantity, int userId, string? comment = null)
    {
        if (type != In && type != Out) return OperationResult.Fail("Неизвестный тип операции.");
        if (quantity <= 0) return OperationResult.Fail("Количество должно быть больше нуля.");

        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            var result = Apply(conn, tx, batchId, type, quantity, userId, comment);
            if (result.Success) tx.Commit(); else tx.Rollback();
            return result;
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return OperationResult.Fail($"Ошибка: {ex.Message}");
        }
    }

    private OperationResult Apply(SqliteConnection conn, SqliteTransaction tx, int batchId, string type, int quantity, int userId, string? comment)
    {
        DateTime expiry;
        using (var get = conn.CreateCommand())
        {
            get.Transaction = tx;
            get.CommandText = "SELECT ExpiryDate FROM Batches WHERE BatchId = $id";
            get.Parameters.AddWithValue("$id", batchId);
            var v = get.ExecuteScalar();
            if (v == null) return OperationResult.Fail("Партия не найдена.");
            expiry = Database.ParseDate((string)v);
        }

        bool expired = expiry.Date < _today().Date;
        if (type == In && expired) return OperationResult.Fail("Нельзя оформить приход на партию с истёкшим сроком годности.");
        if (type == Out && expired && string.IsNullOrWhiteSpace(comment))
            return OperationResult.Fail("Партия просрочена: расход возможен только как списание — укажите причину в комментарии.");

        using (var upd = conn.CreateCommand())
        {
            upd.Transaction = tx;
            // Атомарная проверка остатка: условие внутри UPDATE исключает гонку «проверил — списал».
            upd.CommandText = type == In
                ? "UPDATE Batches SET Quantity = Quantity + $q WHERE BatchId = $id"
                : "UPDATE Batches SET Quantity = Quantity - $q WHERE BatchId = $id AND Quantity >= $q";
            upd.Parameters.AddWithValue("$q", quantity);
            upd.Parameters.AddWithValue("$id", batchId);
            if (upd.ExecuteNonQuery() == 0) return OperationResult.Fail("Недостаточно товара в выбранной партии.");
        }

        using var log = conn.CreateCommand();
        log.Transaction = tx;
        log.CommandText = @"INSERT INTO StockOperations (BatchId, OperationType, Quantity, OperationDate, UserId, Comment)
                            VALUES ($b, $t, $q, $d, $u, $c)";
        log.Parameters.AddWithValue("$b", batchId);
        log.Parameters.AddWithValue("$t", type);
        log.Parameters.AddWithValue("$q", quantity);
        log.Parameters.AddWithValue("$d", DateTime.Now.ToString(Database.DateTimeFormat, System.Globalization.CultureInfo.InvariantCulture));
        log.Parameters.AddWithValue("$u", userId);
        log.Parameters.AddWithValue("$c", comment ?? "");
        log.ExecuteNonQuery();
        return OperationResult.Ok();
    }

    public List<StockOperation> GetHistory(int? batchId = null, int limit = 500)
    {
        var list = new List<StockOperation>();
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT o.OperationId, o.BatchId, p.Name, b.BatchNumber, o.OperationType, o.Quantity,
                                   o.OperationDate, u.FullName, IFNULL(o.Comment,'')
                            FROM StockOperations o
                            JOIN Batches b ON b.BatchId = o.BatchId
                            JOIN Products p ON p.ProductId = b.ProductId
                            JOIN Users u ON u.UserId = o.UserId
                            WHERE $b IS NULL OR o.BatchId = $b
                            ORDER BY o.OperationId DESC LIMIT $lim";
        cmd.Parameters.AddWithValue("$b", batchId.HasValue ? batchId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("$lim", limit);
        using var rd = cmd.ExecuteReader();
        while (rd.Read())
            list.Add(new StockOperation
            {
                OperationId = rd.GetInt32(0), BatchId = rd.GetInt32(1), ProductName = rd.GetString(2), BatchNumber = rd.GetString(3),
                OperationType = rd.GetString(4), Quantity = rd.GetInt32(5), OperationDate = Database.ParseDateTime(rd.GetString(6)),
                UserName = rd.GetString(7), Comment = rd.GetString(8)
            });
        return list;
    }

    /// <summary>Партии с остатком, срок которых истёк или истекает в течение daysThreshold дней.</summary>
    public List<ExpiringBatch> GetExpiringBatches(int daysThreshold = 30)
    {
        var today = _today().Date;
        var limit = Database.ToDb(today.AddDays(daysThreshold));
        var result = new List<ExpiringBatch>();
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT p.Name, b.BatchNumber, b.Quantity, b.ExpiryDate
                            FROM Batches b JOIN Products p ON p.ProductId = b.ProductId
                            WHERE b.Quantity > 0 AND b.ExpiryDate <= $limit
                            ORDER BY b.ExpiryDate";
        cmd.Parameters.AddWithValue("$limit", limit);
        using var rd = cmd.ExecuteReader();
        while (rd.Read())
        {
            var d = Database.ParseDate(rd.GetString(3));
            result.Add(new ExpiringBatch
            {
                ProductName = rd.GetString(0), BatchNumber = rd.GetString(1), Quantity = rd.GetInt32(2),
                ExpiryDate = d, IsExpired = d < today
            });
        }
        return result;
    }
}
