using System.Data;
using System.Drawing;

namespace PharmacyWarehouse.UI;

/// <summary>Сводка остатков с цветовой индикацией статуса и экспортом в Excel.</summary>
public class ReportForm : Form
{
    private readonly AppServices _s;
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
    };
    private readonly NumericUpDown _days = new() { Minimum = 1, Maximum = 365, Value = 30, Width = 70 };
    private readonly Label _total = new() { AutoSize = true, Margin = new Padding(12, 9, 3, 3) };
    private DataTable? _table;

    public ReportForm(AppServices services)
    {
        _s = services;
        Text = "Отчёт: остатки на складе";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(960, 520);

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(6) };
        top.Controls.Add(Ui.Lbl("«Истекает скоро» — за, дней:"));
        top.Controls.Add(_days);
        top.Controls.Add(Ui.Btn("Построить", (_, _) => Build(), 110));
        top.Controls.Add(Ui.Btn("Экспорт в Excel…", (_, _) => Export(), 150));
        top.Controls.Add(_total);

        var bar = Ui.ButtonBar();
        bar.Controls.Add(Ui.Btn("Закрыть", (_, _) => Close(), 110));

        _grid.CellFormatting += Grid_CellFormatting;
        Controls.Add(_grid); Controls.Add(top); Controls.Add(bar);
        Build();
    }

    private void Build()
    {
        _table = _s.Reports.GetStockSummary((int)_days.Value);
        _grid.DataSource = _table;
        decimal sum = 0; int qty = 0;
        foreach (DataRow r in _table.Rows) { sum += (decimal)r["Сумма, руб."]; qty += (int)r["Остаток"]; }
        _total.Text = $"Позиций (партий): {_table.Rows.Count}   Единиц на складе: {qty}   На сумму: {sum:N2} руб.";
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || _table == null || e.CellStyle == null) return;
        var status = _grid.Rows[e.RowIndex].Cells["Статус"].Value?.ToString();
        e.CellStyle.BackColor = status switch
        {
            "Просрочено" => Color.FromArgb(255, 205, 210),
            "Истекает скоро" => Color.FromArgb(255, 240, 190),
            "Нет в наличии" => Color.FromArgb(230, 230, 230),
            _ => Color.White
        };
        e.CellStyle.ForeColor = Color.Black;
    }

    private void Export()
    {
        if (_table == null || _table.Rows.Count == 0) { Ui.Info(this, "Нет данных для экспорта."); return; }
        using var dlg = new SaveFileDialog
        {
            Filter = "Книга Excel (*.xlsx)|*.xlsx", FileName = $"Остатки_{DateTime.Now:yyyy-MM-dd}.xlsx", OverwritePrompt = true
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            _s.Reports.ExportToExcel(_table, dlg.FileName);
            Ui.Info(this, "Отчёт сохранён:\n" + dlg.FileName);
        }
        catch (IOException ex)
        {
            Ui.Error(this, "Не удалось сохранить файл (возможно, он открыт в Excel):\n" + ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            Ui.Error(this, "Нет доступа к выбранной папке:\n" + ex.Message);
        }
    }
}
