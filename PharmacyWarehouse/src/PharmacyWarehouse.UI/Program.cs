namespace PharmacyWarehouse.UI;

using PharmacyWarehouse.Core.Services;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        AppServices services;
        try
        {
            string dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PharmacyWarehouse", "pharmacy.db");
            services = new AppServices(dbPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось открыть базу данных:\n" + ex.Message, "Аптека — склад",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // Цикл «вход → работа → выход»: после выхода из учётной записи снова показывается форма входа.
        while (true)
        {
            using var login = new LoginForm(services.Auth);
            if (login.ShowDialog() != DialogResult.OK || login.LoggedIn == null) break;

            Session.Start(login.LoggedIn);
            var main = new MainForm(services);
            Application.Run(main);
            Session.End();
            if (!main.LogoutRequested) break;
        }
    }
}
