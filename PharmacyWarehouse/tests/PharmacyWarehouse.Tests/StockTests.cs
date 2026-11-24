using PharmacyWarehouse.Core.Services;

namespace PharmacyWarehouse.Tests;

public class StockTests
{
    [Fact]
    public void RegisterOperation_ZeroQuantity_ReturnsError()
    {
        using var t = new TestDb();
        int b = t.AddBatch(t.AddProduct(), 10);
        var r = t.Stock.RegisterOperation(b, StockService.In, 0, t.Admin.UserId);
        Assert.False(r.Success);
        Assert.Contains("больше нуля", r.Message);
    }

    [Fact]
    public void RegisterOperation_NegativeQuantity_ReturnsError()
    {
        using var t = new TestDb();
        int b = t.AddBatch(t.AddProduct(), 10);
        Assert.False(t.Stock.RegisterOperation(b, StockService.Out, -5, t.Admin.UserId).Success);
        Assert.Equal(10, t.QuantityOf(b));
    }

    [Fact]
    public void RegisterOperation_OutMoreThanStock_ReturnsInsufficientError_AndKeepsStock()
    {
        using var t = new TestDb();
        int b = t.AddBatch(t.AddProduct(), 10);
        var r = t.Stock.RegisterOperation(b, StockService.Out, 20, t.Admin.UserId);
        Assert.False(r.Success);
        Assert.Contains("Недостаточно", r.Message);
        Assert.Equal(10, t.QuantityOf(b));
        Assert.Single(t.Stock.GetHistory(b));   // только начальный приход, неудачная операция не записана
    }

    [Fact]
    public void RegisterOperation_UnknownType_Fails()
    {
        using var t = new TestDb();
        int b = t.AddBatch(t.AddProduct(), 10);
        Assert.False(t.Stock.RegisterOperation(b, "MOVE", 1, t.Admin.UserId).Success);
    }

    [Fact]
    public void RegisterOperation_UnknownBatch_Fails()
    {
        using var t = new TestDb();
        Assert.False(t.Stock.RegisterOperation(12345, StockService.In, 1, t.Admin.UserId).Success);
    }

    [Fact]
    public void Balance_AfterSeveralOperations_IsCorrect()
    {
        using var t = new TestDb();
        int b = t.AddBatch(t.AddProduct(), 10);
        Assert.True(t.Stock.RegisterOperation(b, StockService.In, 5, t.Admin.UserId).Success);   // 15
        Assert.True(t.Stock.RegisterOperation(b, StockService.Out, 7, t.Admin.UserId).Success);  // 8
        Assert.True(t.Stock.RegisterOperation(b, StockService.Out, 8, t.Admin.UserId).Success);  // 0
        Assert.Equal(0, t.QuantityOf(b));
        Assert.False(t.Stock.RegisterOperation(b, StockService.Out, 1, t.Admin.UserId).Success);
        Assert.Equal(4, t.Stock.GetHistory(b).Count);   // начальный приход + 3 операции
    }

    [Fact]
    public void History_RecordsUserTypeAndComment()
    {
        using var t = new TestDb();
        int b = t.AddBatch(t.AddProduct(), 10);
        t.Stock.RegisterOperation(b, StockService.Out, 2, t.Admin.UserId, "Продажа по чеку 17");
        var last = t.Stock.GetHistory(b).First();
        Assert.Equal("OUT", last.OperationType);
        Assert.Equal(2, last.Quantity);
        Assert.Equal("Продажа по чеку 17", last.Comment);
        Assert.Equal(t.Admin.FullName, last.UserName);
    }

    [Fact]
    public void ExpiredBatch_OutWithoutComment_IsRejected_WithCommentIsWriteOff()
    {
        using var t = new TestDb();
        int b = t.AddBatch(t.AddProduct(), 10, "OLD", daysToExpiry: -10);
        Assert.False(t.Stock.RegisterOperation(b, StockService.Out, 3, t.Admin.UserId).Success);
        Assert.True(t.Stock.RegisterOperation(b, StockService.Out, 3, t.Admin.UserId, "Списание: истёк срок").Success);
        Assert.Equal(7, t.QuantityOf(b));
    }

    [Fact]
    public void ExpiredBatch_In_IsRejected()
    {
        using var t = new TestDb();
        int b = t.AddBatch(t.AddProduct(), 0, "OLD", daysToExpiry: -1);
        Assert.False(t.Stock.RegisterOperation(b, StockService.In, 3, t.Admin.UserId).Success);
    }

    [Fact]
    public void AddBatch_DuplicateNumberForSameProduct_Fails_ButOtherProductOk()
    {
        using var t = new TestDb();
        int p1 = t.AddProduct("А"), p2 = t.AddProduct("Б");
        t.AddBatch(p1, 1, "S1");
        Assert.False(t.Stock.AddBatch(p1, "S1", t.Today.AddDays(100), 1, t.Admin.UserId).Success);
        Assert.True(t.Stock.AddBatch(p2, "S1", t.Today.AddDays(100), 1, t.Admin.UserId).Success);
    }

    [Fact]
    public void AddBatch_AlreadyExpired_IsRejected()
    {
        using var t = new TestDb();
        var r = t.Stock.AddBatch(t.AddProduct(), "OLD", t.Today.AddDays(-1), 5, t.Admin.UserId);
        Assert.False(r.Success);
        Assert.Empty(t.Stock.GetBatches());
    }

    [Fact]
    public void AddBatch_Validation()
    {
        using var t = new TestDb();
        int p = t.AddProduct();
        Assert.False(t.Stock.AddBatch(p, "  ", t.Today.AddDays(5), 1, t.Admin.UserId).Success);
        Assert.False(t.Stock.AddBatch(p, "X", t.Today.AddDays(5), -1, t.Admin.UserId).Success);
        Assert.False(t.Stock.AddBatch(777, "X", t.Today.AddDays(5), 1, t.Admin.UserId).Success);
    }

    [Fact]
    public void ExpiringBatches_ReturnsOnlyExpiredAndSoonWithStock()
    {
        using var t = new TestDb();
        int p = t.AddProduct();
        t.AddBatch(p, 5, "OLD", -5);      // просрочена
        t.AddBatch(p, 5, "SOON", 20);     // истекает в 30 дней
        t.AddBatch(p, 5, "FAR", 200);     // нет
        t.AddBatch(p, 0, "EMPTY", -5);    // остаток 0 — не показываем
        var list = t.Stock.GetExpiringBatches(30);
        Assert.Equal(new[] { "OLD", "SOON" }, list.Select(x => x.BatchNumber).ToArray());
        Assert.True(list[0].IsExpired);
        Assert.False(list[1].IsExpired);
    }

    [Fact]
    public void ExpiringBatches_BoundaryDay_IsIncluded()
    {
        using var t = new TestDb();
        t.AddBatch(t.AddProduct(), 5, "EDGE", 30);
        t.AddBatch(t.AddProduct("Другой"), 5, "OUT", 31);
        Assert.Single(t.Stock.GetExpiringBatches(30));
    }

    [Fact]
    public void FailedOperation_RollsBack_NoPartialWrite()
    {
        using var t = new TestDb();
        int b = t.AddBatch(t.AddProduct(), 10);
        // несуществующий пользователь нарушает внешний ключ при записи в историю -> откат изменения остатка
        var r = t.Stock.RegisterOperation(b, StockService.Out, 4, userId: 9999);
        Assert.False(r.Success);
        Assert.Equal(10, t.QuantityOf(b));
    }

    [Fact]
    public async Task ConcurrentWriteOffs_NeverMakeStockNegative()
    {
        using var t = new TestDb();
        int b = t.AddBatch(t.AddProduct(), 5);
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
            t.Stock.RegisterOperation(b, StockService.Out, 1, t.Admin.UserId))).ToArray();
        var results = await Task.WhenAll(tasks);
        Assert.True(results.Count(r => r.Success) <= 5);
        Assert.True(t.QuantityOf(b) >= 0);
        Assert.Equal(5 - results.Count(r => r.Success), t.QuantityOf(b));
    }
}
