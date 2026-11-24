using ClosedXML.Excel;
using PharmacyWarehouse.Core.Services;

namespace PharmacyWarehouse.Tests;

public class ReportTests
{
    [Fact]
    public void Summary_ComputesStatusesAndSums()
    {
        using var t = new TestDb();
        int p = t.AddProduct("Парацетамол", 100m);
        t.AddBatch(p, 3, "OK", 300);
        t.AddBatch(p, 2, "SOON", 10);
        t.AddBatch(p, 1, "OLD", -1);
        t.AddBatch(p, 0, "ZERO", 100);
        var table = t.Reports.GetStockSummary(30);
        string Status(string batch) => table.Rows.Cast<System.Data.DataRow>().Single(r => (string)r["Партия"] == batch)["Статус"].ToString()!;
        Assert.Equal("В норме", Status("OK"));
        Assert.Equal("Истекает скоро", Status("SOON"));
        Assert.Equal("Просрочено", Status("OLD"));
        Assert.Equal("Нет в наличии", Status("ZERO"));
        var ok = table.Rows.Cast<System.Data.DataRow>().Single(r => (string)r["Партия"] == "OK");
        Assert.Equal(300m, (decimal)ok["Сумма, руб."]);
    }

    [Fact]
    public void ExportToExcel_WritesHeaderAndRows_WithNumericCells()
    {
        using var t = new TestDb();
        int p = t.AddProduct("Аспирин", 50m);
        t.AddBatch(p, 4, "X1", 100);
        var table = t.Reports.GetStockSummary();
        var path = Path.Combine(Path.GetTempPath(), $"report_{Guid.NewGuid():N}.xlsx");
        try
        {
            t.Reports.ExportToExcel(table, path);
            Assert.True(File.Exists(path));
            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet(1);
            Assert.Equal("Товар", ws.Cell(1, 1).GetString());
            Assert.True(ws.Cell(1, 1).Style.Font.Bold);
            Assert.Equal("Аспирин", ws.Cell(2, 1).GetString());
            Assert.Equal(4, ws.Cell(2, 4).GetDouble());      // остаток — число
            Assert.Equal(200, ws.Cell(2, 7).GetDouble());    // сумма 50*4
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void ExportToExcel_EmptyTable_StillCreatesFileWithHeader()
    {
        using var t = new TestDb();
        var path = Path.Combine(Path.GetTempPath(), $"empty_{Guid.NewGuid():N}.xlsx");
        try
        {
            t.Reports.ExportToExcel(t.Reports.GetStockSummary(), path);
            using var wb = new XLWorkbook(path);
            Assert.Equal("Статус", wb.Worksheet(1).Cell(1, 6).GetString());
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
