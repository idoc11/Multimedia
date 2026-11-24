using System.Drawing;
using PharmacyWarehouse.Core.Models;

namespace PharmacyWarehouse.UI;

/// <summary>Список товаров с поиском и кнопками добавления / редактирования / удаления.</summary>
public class ProductForm : Form
{
    private readonly AppServices _s;
    private readonly DataGridView _grid = Ui.Grid();
    private readonly TextBox _search = new() { Width = 260 };

    public ProductForm(AppServices services)
    {
        _s = services;
        Text = "Товары";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(900, 480);

        _grid.Columns.Add(Ui.Col("Наименование", nameof(Product.Name), 160));
        _grid.Columns.Add(Ui.Col("Действующее вещество", nameof(Product.ActiveIngredient), 130));
        _grid.Columns.Add(Ui.Col("Производитель", nameof(Product.Manufacturer), 110));
        _grid.Columns.Add(Ui.Col("Форма выпуска", nameof(Product.DosageForm), 90));
        _grid.Columns.Add(Ui.Col("Цена, руб.", nameof(Product.Price), 60, "N2"));
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "По рецепту", DataPropertyName = nameof(Product.RequiresPrescription), FillWeight = 60 });
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Edit(); };

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6) };
        top.Controls.Add(Ui.Lbl("Поиск:"));
        top.Controls.Add(_search);
        _search.TextChanged += (_, _) => Reload();

        var bar = Ui.ButtonBar();
        bar.Controls.Add(Ui.Btn("Добавить", (_, _) => Add()));
        bar.Controls.Add(Ui.Btn("Изменить", (_, _) => Edit()));
        bar.Controls.Add(Ui.Btn("Удалить", (_, _) => Delete()));
        bar.Controls.Add(Ui.Btn("Закрыть", (_, _) => Close()));

        Controls.Add(_grid);
        Controls.Add(top);
        Controls.Add(bar);
        Reload();
    }

    private Product? Selected => _grid.CurrentRow?.DataBoundItem as Product;

    private void Reload() => _grid.DataSource = _s.Products.GetAll(_search.Text);

    private void Add()
    {
        using var dlg = new ProductEditDialog(new Product());
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var r = _s.Products.Add(dlg.Result);
        if (r.Success) Reload(); else Ui.Error(this, r.Message);
    }

    private void Edit()
    {
        var p = Selected;
        if (p == null) { Ui.Info(this, "Выберите товар в списке."); return; }
        using var dlg = new ProductEditDialog(p);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var r = _s.Products.Update(dlg.Result);
        if (r.Success) Reload(); else Ui.Error(this, r.Message);
    }

    private void Delete()
    {
        var p = Selected;
        if (p == null) { Ui.Info(this, "Выберите товар в списке."); return; }
        if (!Ui.Confirm(this, $"Удалить товар «{p.Name}»?")) return;
        var r = _s.Products.Delete(p.ProductId);
        if (r.Success) Reload(); else Ui.Error(this, r.Message);
    }
}

/// <summary>Диалог ввода данных товара. Проверка — через ProductService.Validate.</summary>
public class ProductEditDialog : Form
{
    private readonly TextBox _name = new() { Dock = DockStyle.Fill };
    private readonly TextBox _ingredient = new() { Dock = DockStyle.Fill };
    private readonly TextBox _manufacturer = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _form = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
    private readonly NumericUpDown _price = new() { Dock = DockStyle.Left, Width = 120, DecimalPlaces = 2, Maximum = 1000000, ThousandsSeparator = true };
    private readonly CheckBox _rx = new() { Text = "Рецептурный отпуск", AutoSize = true };
    private readonly int _id;

    public Product Result { get; private set; } = new();

    public ProductEditDialog(Product p)
    {
        _id = p.ProductId;
        Text = p.ProductId == 0 ? "Новый товар" : "Редактирование товара";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(460, 300);

        _form.Items.AddRange(new object[] { "таблетки", "капсулы", "ампулы", "сироп", "мазь", "капли", "раствор", "порошок", "свечи" });
        _name.Text = p.Name; _ingredient.Text = p.ActiveIngredient ?? ""; _manufacturer.Text = p.Manufacturer ?? "";
        _form.Text = p.DosageForm ?? ""; _price.Value = Math.Min(_price.Maximum, Math.Max(0, p.Price)); _rx.Checked = p.RequiresPrescription;

        var t = new TableLayoutPanel { Dock = DockStyle.Top, Height = 220, ColumnCount = 2, RowCount = 6, Padding = new Padding(12) };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Row(int i, string label, Control c) { t.Controls.Add(Ui.Lbl(label), 0, i); t.Controls.Add(c, 1, i); }
        Row(0, "Наименование*:", _name); Row(1, "Действующее вещество:", _ingredient); Row(2, "Производитель:", _manufacturer);
        Row(3, "Форма выпуска:", _form); Row(4, "Цена, руб.:", _price); Row(5, "", _rx);

        var ok = new Button { Text = "Сохранить", Left = 250, Top = 252, Width = 95, Height = 30 };
        var cancel = new Button { Text = "Отмена", Left = 355, Top = 252, Width = 95, Height = 30, DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) => Save();
        Controls.Add(t); Controls.Add(ok); Controls.Add(cancel);
        AcceptButton = ok; CancelButton = cancel;
    }

    private void Save()
    {
        var p = new Product
        {
            ProductId = _id, Name = _name.Text.Trim(),
            ActiveIngredient = NullIfEmpty(_ingredient.Text), Manufacturer = NullIfEmpty(_manufacturer.Text),
            DosageForm = NullIfEmpty(_form.Text), Price = _price.Value, RequiresPrescription = _rx.Checked
        };
        var err = Core.Services.ProductService.Validate(p);
        if (err != null) { Ui.Warn(this, err); return; }
        Result = p;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
