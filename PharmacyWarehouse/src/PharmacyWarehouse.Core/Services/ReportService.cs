using System.Data;
using ClosedXML.Excel;
using PharmacyWarehouse.Core.Data;

namespace PharmacyWarehouse.Core.Services;

/// <summary>Сводка остатков и экспорт в Excel (ClosedXML, установленный Office не нужен).</summary>
public class ReportService
{
    private readonly Database _db;
    private readonly Func<DateTime> _today;

    public ReportService(Database db, Func<DateTime>? today = null)
    {
        _db = db;
        _today = today ?? (() => DateTime.Today);
    }

    /// <summary>Остатки по партиям: товар, форма, партия, остаток, срок годности, статус, сумма.</summary>
    public DataTable GetStockSummary(int expiringDays = 30)
    {
        var table = new DataTable("Остатки");
        table.Columns.Add("Товар", typeof(string));
        table.Columns.Add("Форма выпуска", typeof(string));
        table.Columns.Add("Партия", typeof(string));
        table.Columns.Add("Остаток", typeof(int));
        table.Columns.Add("Срок годности", typeof(string));
        table.Columns.Add("Статус", typeof(string));
        table.Columns.Add("Сумма, руб.", typeof(decimal));

        var today = _today().Date;
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT p.Name, IFNULL(p.DosageForm,''), b.BatchNumber, b.Quantity, b.ExpiryDate, p.Price
                            FROM Batches b JOIN Products p ON p.ProductId = b.ProductId
                            ORDER BY p.Name, b.ExpiryDate";
        using var rd = cmd.ExecuteReader();
        while (rd.Read())
        {
            var expiry = Database.ParseDate(rd.GetString(4));
            int qty = rd.GetInt32(3);
            string status = qty == 0 ? "Нет в наличии"
                : expiry < today ? "Просрочено"
                : expiry <= today.AddDays(expiringDays) ? "Истекает скоро" : "В норме";
            table.Rows.Add(rd.GetString(0), rd.GetString(1), rd.GetString(2), qty,
                           expiry.ToString("dd.MM.yyyy"), status, Math.Round((decimal)rd.GetDouble(5) * qty, 2));
        }
        return table;
    }

    public void ExportToExcel(DataTable data, string filePath, string sheetName = "Остатки на складе")
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add(sheetName);
        for (int c = 0; c < data.Columns.Count; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = data.Columns[c].ColumnName;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
        }
        for (int r = 0; r < data.Rows.Count; r++)
            for (int c = 0; c < data.Columns.Count; c++)
            {
                var v = data.Rows[r][c];
                var cell = ws.Cell(r + 2, c + 1);
                // числа пишем числами, чтобы в Excel работали формулы и сортировка
                if (v is int i) cell.Value = i;
                else if (v is decimal d) cell.Value = d;
                else cell.Value = v?.ToString() ?? "";
            }
        ws.Columns().AdjustToContents();
        workbook.SaveAs(filePath);
    }
}
