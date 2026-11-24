using System.Drawing;
using PharmacyWarehouse.Core.Models;
using PharmacyWarehouse.Core.Services;

namespace PharmacyWarehouse.UI;

/// <summary>Форма входа. После 5 неудачных попыток закрывается.</summary>
public class LoginForm : Form
{
    private readonly AuthService _auth;
    private readonly TextBox _login = new() { Dock = DockStyle.Fill };
    private readonly TextBox _password = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
    private readonly Label _error = new() { AutoSize = true, ForeColor = Color.Firebrick };
    private int _attempts;

    public User? LoggedIn { get; private set; }

    public LoginForm(AuthService auth)
    {
        _auth = auth;
        Text = "Вход в систему — Аптека, склад";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(380, 210);

        var table = new TableLayoutPanel { Dock = DockStyle.Top, Height = 110, ColumnCount = 2, RowCount = 2, Padding = new Padding(16, 16, 16, 0) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.Controls.Add(Ui.Lbl("Логин:"), 0, 0); table.Controls.Add(_login, 1, 0);
        table.Controls.Add(Ui.Lbl("Пароль:"), 0, 1); table.Controls.Add(_password, 1, 1);

        _error.Location = new Point(16, 118);

        var ok = new Button { Text = "Войти", Width = 110, Height = 32, Left = 16, Top = 156 };
        var cancel = new Button { Text = "Выход", Width = 110, Height = 32, Left = 254, Top = 156, DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) => TryLogin();
        Controls.Add(table); Controls.Add(_error); Controls.Add(ok); Controls.Add(cancel);
        AcceptButton = ok; CancelButton = cancel;
    }

    private void TryLogin()
    {
        var user = _auth.Login(_login.Text, _password.Text);
        if (user != null)
        {
            LoggedIn = user;
            DialogResult = DialogResult.OK;
            Close();
            return;
        }
        _attempts++;
        _password.Clear();
        _password.Focus();
        if (_attempts >= 5)
        {
            Ui.Error(this, "Слишком много неудачных попыток входа.");
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }
        // Сообщение одинаково для «нет такого логина», «неверный пароль» и «учётная запись заблокирована».
        _error.Text = $"Неверный логин или пароль, либо учётная запись заблокирована (попытка {_attempts} из 5).";
    }
}
