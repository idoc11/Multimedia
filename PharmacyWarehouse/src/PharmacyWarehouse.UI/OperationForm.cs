using System.Drawing;
using PharmacyWarehouse.Core.Models;
using PharmacyWarehouse.Core.Services;

namespace PharmacyWarehouse.UI;

/// <summary>Оформление прихода/расхода по партии, создание партий, журнал операций.</summary>
public class OperationForm : Form
{
    private readonly AppServices _s;
    private readonly ComboBox _product = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _batch = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly RadioButton _in = new() { Text = "Приход", Checked = true, AutoSize = true };
    private readonly RadioButton _out = new() { Text = "Расход / списание", AutoSize = true };
    private readonly NumericUpDown _qty = new() { Minimum = 1, Maximum = 1000000, Value = 1, Width = 100 };
    private readonly TextBox _comment = new() { Dock = DockStyle.Fill };
    private readonly DataGridView _history = Ui.Grid();

    public OperationForm(AppServices services)
    {
        _s = services;
        Text = "Приход и расход товара";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(940, 600);

        var t = new TableLayoutPanel { Dock = DockStyle.Top, Height = 190, ColumnCount = 2, RowCount = 5, Padding = new Padding(12) };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        t.Controls.Add(Ui.Lbl("Товар:"), 0, 0); t.Controls.Add(_product, 1, 0);
        t.Controls.Add(Ui.Lbl("Партия:"), 0, 1); t.Controls.Add(_batch, 1, 1);
        var type = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        type.Controls.AddRange(new Control[] { _in, _out });
        t.Controls.Add(Ui.Lbl("Операция:"), 0, 2); t.Controls.Add(type, 1, 2);
        t.Controls.Add(Ui.Lbl("Количество:"), 0, 3);
        var q = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true }; q.Controls.Add(_qty); t.Controls.Add(q, 1, 3);
        t.Controls.Add(Ui.Lbl("Комментарий:"), 0, 4); t.Controls.Add(_comment, 1, 4);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(8, 4, 0, 0) };
        actions.Controls.Add(Ui.Btn("Выполнить", (_, _) => Execute(), 140));
        actions.Controls.Add(Ui.Btn("Новая партия…", (_, _) => NewBatch(), 140));
        actions.Controls.Add(Ui.Btn("Закрыть", (_, _) => Close(), 100));

        _history.Columns.Add(Ui.Col("Дата и время", nameof(StockOperation.OperationDate), 90, "dd.MM.yyyy HH:mm"));
        _history.Columns.Add(Ui.Col("Товар", nameof(StockOperation.ProductName), 130));
        _history.Columns.Add(Ui.Col("Партия", nameof(StockOperation.BatchNumber), 60));
        _history.Columns.Add(Ui.Col("Тип", nameof(StockOperation.OperationType), 40));
        _history.Columns.Add(Ui.Col("Кол-во", nameof(StockOperation.Quantity), 50));
        _history.Columns.Add(Ui.Col("Сотрудник", nameof(StockOperation.UserName), 90));
        _history.Columns.Add(Ui.Col("Комментарий", nameof(StockOperation.Comment), 140));
        var histLabel = new Label { Text = "Последние операции:", Dock = DockStyle.Top, Height = 24, Padding = new Padding(8, 4, 0, 0) };

        Controls.Add(_history);
        Controls.Add(histLabel);
        Controls.Add(actions);
        Controls.Add(t);

        _product.SelectedIndexChanged += (_, _) => LoadBatches();
        LoadProducts();
    }

    private void LoadProducts()
    {
        var list = _s.Products.GetAll();
        _product.DisplayMember = nameof(Product.Name);
        _product.DataSource = list;
        if (list.Count == 0) _batch.DataSource = null;
        LoadHistory();
    }

    private void LoadBatches()
    {
        if (_product.SelectedItem is not Product p) { _batch.DataSource = null; return; }
        _batch.DataSource = _s.Stock.GetBatches(p.ProductId);
    }

    private void LoadHistory() => _history.DataSource = _s.Stock.GetHistory(null, 200);

    private void Execute()
    {
        if (_batch.SelectedItem is not Batch b) { Ui.Warn(this, "Сначала выберите товар и партию (или создайте новую партию)."); return; }
        string type = _in.Checked ? StockService.In : StockService.Out;
        var r = _s.Stock.RegisterOperation(b.BatchId, type, (int)_qty.Value, Session.UserId, _comment.Text);
        if (r.Success)
        {
            Ui.Info(this, r.Message);
            _comment.Clear();
            int keep = b.BatchId;
            LoadBatches();
            foreach (Batch item in _batch.Items) if (item.BatchId == keep) { _batch.SelectedItem = item; break; }
            LoadHistory();
        }
        else Ui.Error(this, r.Message);
    }

    private void NewBatch()
    {
        if (_product.SelectedItem is not Product p) { Ui.Warn(this, "Сначала выберите товар (добавьте его в разделе «Товары»)."); return; }
        using var dlg = new BatchDialog(p.Name);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var r = _s.Stock.AddBatch(p.ProductId, dlg.BatchNumber, dlg.Expiry, dlg.Quantity, Session.UserId);
        if (r.Success) { Ui.Info(this, r.Message); LoadBatches(); LoadHistory(); }
        else Ui.Error(this, r.Message);
    }
}

public class BatchDialog : Form
{
    private readonly TextBox _number = new() { Dock = DockStyle.Fill };
    private readonly DateTimePicker _expiry = new() { Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddYears(1), Width = 140 };
    private readonly NumericUpDown _qty = new() { Minimum = 0, Maximum = 1000000, Value = 0, Width = 100 };

    public string BatchNumber => _number.Text.Trim();
    public DateTime Expiry => _expiry.Value.Date;
    public int Quantity => (int)_qty.Value;

    public BatchDialog(string productName)
    {
        Text = $"Новая партия: {productName}";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(420, 190);

        var t = new TableLayoutPanel { Dock = DockStyle.Top, Height = 120, ColumnCount = 2, RowCount = 3, Padding = new Padding(12) };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        t.Controls.Add(Ui.Lbl("Номер партии (серии):"), 0, 0); t.Controls.Add(_number, 1, 0);
        t.Controls.Add(Ui.Lbl("Срок годности до:"), 0, 1); t.Controls.Add(_expiry, 1, 1);
        t.Controls.Add(Ui.Lbl("Начальный остаток:"), 0, 2); t.Controls.Add(_qty, 1, 2);

        var ok = new Button { Text = "Создать", Left = 210, Top = 140, Width = 95, Height = 30 };
        var cancel = new Button { Text = "Отмена", Left = 315, Top = 140, Width = 95, Height = 30, DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) =>
        {
            if (BatchNumber.Length == 0) { Ui.Warn(this, "Укажите номер партии."); return; }
            DialogResult = DialogResult.OK; Close();
        };
        Controls.Add(t); Controls.Add(ok); Controls.Add(cancel);
        AcceptButton = ok; CancelButton = cancel;
    }
}
