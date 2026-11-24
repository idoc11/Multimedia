using System.Drawing;
using PharmacyWarehouse.Core.Models;
using PharmacyWarehouse.Core.Services;

namespace PharmacyWarehouse.UI;

/// <summary>Управление пользователями. Доступно только администратору.</summary>
public class UserForm : Form
{
    private readonly AppServices _s;
    private readonly DataGridView _grid = Ui.Grid();

    public UserForm(AppServices services)
    {
        _s = services;
        Text = "Пользователи";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(760, 420);

        _grid.Columns.Add(Ui.Col("Логин", nameof(User.Login), 80));
        _grid.Columns.Add(Ui.Col("ФИО", nameof(User.FullName), 140));
        _grid.Columns.Add(Ui.Col("Роль", nameof(User.RoleName), 80));
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Активен", DataPropertyName = nameof(User.IsActive), FillWeight = 50 });

        var bar = Ui.ButtonBar();
        bar.Controls.Add(Ui.Btn("Добавить", (_, _) => Add()));
        bar.Controls.Add(Ui.Btn("Блокировать / разблок.", (_, _) => Toggle(), 170));
        bar.Controls.Add(Ui.Btn("Сменить пароль", (_, _) => Reset(), 140));
        bar.Controls.Add(Ui.Btn("Закрыть", (_, _) => Close(), 100));

        Controls.Add(_grid); Controls.Add(bar);
        Load += (_, _) =>
        {
            if (!Session.IsAdmin) { Ui.Error(this, "Раздел доступен только администратору."); Close(); return; }
            Reload();
        };
    }

    private User Actor => Session.Current!;
    private User? Selected => _grid.CurrentRow?.DataBoundItem as User;
    private void Reload() => _grid.DataSource = _s.Auth.GetAll(Actor);

    private void Add()
    {
        using var dlg = new UserEditDialog();
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var r = _s.Auth.Create(Actor, dlg.Login, dlg.Password, dlg.FullName, dlg.RoleName);
        if (r.Success) { Ui.Info(this, r.Message); Reload(); } else Ui.Error(this, r.Message);
    }

    private void Toggle()
    {
        var u = Selected;
        if (u == null) { Ui.Info(this, "Выберите пользователя."); return; }
        var r = _s.Auth.SetActive(Actor, u.UserId, !u.IsActive);
        if (r.Success) Reload(); else Ui.Error(this, r.Message);
    }

    private void Reset()
    {
        var u = Selected;
        if (u == null) { Ui.Info(this, "Выберите пользователя."); return; }
        var pwd = Ui.Prompt(this, "Смена пароля", $"Новый пароль для «{u.Login}» (не менее 6 символов):", password: true);
        if (pwd == null) return;
        var r = _s.Auth.ResetPassword(Actor, u.UserId, pwd);
        if (r.Success) Ui.Info(this, r.Message); else Ui.Error(this, r.Message);
    }
}

public class UserEditDialog : Form
{
    private readonly TextBox _login = new() { Dock = DockStyle.Fill };
    private readonly TextBox _password = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
    private readonly TextBox _full = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _role = new() { Dock = DockStyle.Left, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };

    public string Login => _login.Text.Trim();
    public string Password => _password.Text;
    public string FullName => _full.Text.Trim();
    public string RoleName => _role.SelectedIndex == 0 ? Roles.Pharmacist : Roles.Admin;

    public UserEditDialog()
    {
        Text = "Новый пользователь";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(430, 250);
        _role.Items.AddRange(new object[] { "Фармацевт / кладовщик", "Администратор" });
        _role.SelectedIndex = 0;

        var t = new TableLayoutPanel { Dock = DockStyle.Top, Height = 170, ColumnCount = 2, RowCount = 4, Padding = new Padding(12) };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        t.Controls.Add(Ui.Lbl("Логин*:"), 0, 0); t.Controls.Add(_login, 1, 0);
        t.Controls.Add(Ui.Lbl("Пароль*:"), 0, 1); t.Controls.Add(_password, 1, 1);
        t.Controls.Add(Ui.Lbl("ФИО*:"), 0, 2); t.Controls.Add(_full, 1, 2);
        t.Controls.Add(Ui.Lbl("Роль:"), 0, 3); t.Controls.Add(_role, 1, 3);

        var ok = new Button { Text = "Создать", Left = 220, Top = 200, Width = 95, Height = 30, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Отмена", Left = 325, Top = 200, Width = 95, Height = 30, DialogResult = DialogResult.Cancel };
        Controls.Add(t); Controls.Add(ok); Controls.Add(cancel);
        AcceptButton = ok; CancelButton = cancel;
    }
}
