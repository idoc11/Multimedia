using System.Drawing;
using PharmacyWarehouse.Core.Services;

namespace PharmacyWarehouse.UI;

/// <summary>Главная форма: навигация по разделам и уведомление о сроках годности.</summary>
public class MainForm : Form
{
    private readonly AppServices _s;
    private readonly ListBox _alerts = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly Label _alertTitle = new() { Dock = DockStyle.Top, Height = 32, Font = new Font("Segoe UI", 11, FontStyle.Bold), Padding = new Padding(4, 8, 0, 0) };
    private readonly Button _btnUsers;

    public bool LogoutRequested { get; private set; }

    public MainForm(AppServices services)
    {
        _s = services;
        Text = "Аптека — учёт товара на складе";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(860, 520);
        MinimumSize = new Size(700, 420);

        var nav = new FlowLayoutPanel
        {
            Dock = DockStyle.Left, Width = 200, FlowDirection = FlowDirection.TopDown, WrapContents = false,
            Padding = new Padding(10), BackColor = Color.FromArgb(235, 241, 245)
        };
        nav.Controls.Add(Ui.Btn("Товары", (_, _) => Open(new ProductForm(_s)), 176));
        nav.Controls.Add(Ui.Btn("Приход / расход", (_, _) => { Open(new OperationForm(_s)); RefreshAlerts(); }, 176));
        nav.Controls.Add(Ui.Btn("Отчёты", (_, _) => Open(new ReportForm(_s)), 176));
        _btnUsers = Ui.Btn("Пользователи", (_, _) => Open(new UserForm(_s)), 176);
        nav.Controls.Add(_btnUsers);
        nav.Controls.Add(Ui.Btn("Выйти из учётной записи", (_, _) => { LogoutRequested = true; Close(); }, 176));

        var status = new StatusStrip();
        status.Items.Add(new ToolStripStatusLabel($"Пользователь: {Session.UserName}   |   Роль: {RoleTitle(Session.RoleName)}"));

        var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
        right.Controls.Add(_alerts);
        right.Controls.Add(_alertTitle);

        Controls.Add(right);
        Controls.Add(nav);
        Controls.Add(status);

        Load += MainForm_Load;
    }

    private static string RoleTitle(string role) => role == "Admin" ? "Администратор" : "Фармацевт / кладовщик";

    private void MainForm_Load(object? sender, EventArgs e)
    {
        // RBAC: раздел «Пользователи» скрыт для всех, кроме администратора.
        // Сами сервисы дополнительно проверяют роль (AuthService бросает UnauthorizedAccessException).
        _btnUsers.Visible = Session.IsAdmin;
        RefreshAlerts();
        if (_alerts.Items.Count > 0 && _alerts.Items[0] is not string)
            Ui.Warn(this, $"Есть партии с истёкшим или истекающим сроком годности: {_alerts.Items.Count}.");
    }

    private void RefreshAlerts()
    {
        _alerts.Items.Clear();
        var list = _s.Stock.GetExpiringBatches(30);
        if (list.Count == 0)
        {
            _alertTitle.Text = "Сроки годности";
            _alerts.Items.Add("Партий с истекающим сроком (30 дней) нет.");
            return;
        }
        _alertTitle.Text = $"Внимание: сроки годности (30 дней) — {list.Count}";
        foreach (var b in list) _alerts.Items.Add(b);
    }

    private void Open(Form form)
    {
        using (form) form.ShowDialog(this);
    }
}
