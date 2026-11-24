using System.Drawing;

namespace PharmacyWarehouse.UI;

/// <summary>Мелкие помощники: единые сообщения и построение однотипных элементов.</summary>
internal static class Ui
{
    public static void Info(IWin32Window owner, string text) =>
        MessageBox.Show(owner, text, "Аптека — склад", MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static void Error(IWin32Window owner, string text) =>
        MessageBox.Show(owner, text, "Аптека — склад", MessageBoxButtons.OK, MessageBoxIcon.Error);

    public static void Warn(IWin32Window owner, string text) =>
        MessageBox.Show(owner, text, "Аптека — склад", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public static bool Confirm(IWin32Window owner, string text) =>
        MessageBox.Show(owner, text, "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    public static DataGridView Grid()
    {
        var g = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoGenerateColumns = false,
            RowHeadersVisible = false,
            BackgroundColor = SystemColors.Window,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        return g;
    }

    public static DataGridViewTextBoxColumn Col(string header, string property, float weight = 100, string? format = null)
    {
        var c = new DataGridViewTextBoxColumn { HeaderText = header, DataPropertyName = property, FillWeight = weight };
        if (format != null) c.DefaultCellStyle.Format = format;
        return c;
    }

    public static Button Btn(string text, EventHandler onClick, int width = 130)
    {
        var b = new Button { Text = text, Width = width, Height = 32, Margin = new Padding(4) };
        b.Click += onClick;
        return b;
    }

    public static Label Lbl(string text) =>
        new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 3, 3) };

    public static FlowLayoutPanel ButtonBar() =>
        new() { Dock = DockStyle.Bottom, Height = 46, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(6) };

    /// <summary>Однострочный запрос текста (используется для нового пароля).</summary>
    public static string? Prompt(IWin32Window owner, string title, string label, bool password = false)
    {
        using var f = new Form
        {
            Text = title, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false, ClientSize = new Size(360, 120)
        };
        var l = new Label { Text = label, Left = 12, Top = 12, Width = 336 };
        var tb = new TextBox { Left = 12, Top = 36, Width = 336, UseSystemPasswordChar = password };
        var ok = new Button { Text = "OK", Left = 168, Top = 76, Width = 85, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Отмена", Left = 263, Top = 76, Width = 85, DialogResult = DialogResult.Cancel };
        f.Controls.AddRange(new Control[] { l, tb, ok, cancel });
        f.AcceptButton = ok; f.CancelButton = cancel;
        return f.ShowDialog(owner) == DialogResult.OK ? tb.Text : null;
    }
}
