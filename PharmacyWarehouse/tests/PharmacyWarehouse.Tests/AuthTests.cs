using PharmacyWarehouse.Core.Data;
using PharmacyWarehouse.Core.Models;
using PharmacyWarehouse.Core.Services;

namespace PharmacyWarehouse.Tests;

public class AuthTests
{
    [Fact]
    public void PasswordHasher_SamePassword_DifferentSaltsAndVerifies()
    {
        var (h1, s1) = PasswordHasher.CreateHash("secret1");
        var (h2, s2) = PasswordHasher.CreateHash("secret1");
        Assert.NotEqual(s1, s2);
        Assert.NotEqual(h1, h2);
        Assert.True(PasswordHasher.Verify("secret1", h1, s1));
        Assert.False(PasswordHasher.Verify("secret2", h1, s1));
    }

    [Fact]
    public void Login_DefaultAdmin_Succeeds_WithAdminRole()
    {
        using var t = new TestDb();
        var u = t.Auth.Login(Database.DefaultAdminLogin, Database.DefaultAdminPassword);
        Assert.NotNull(u);
        Assert.True(u!.IsAdmin);
    }

    [Theory]
    [InlineData("admin", "wrong")]
    [InlineData("nobody", "Admin123!")]
    [InlineData("", "")]
    [InlineData("admin", "")]
    public void Login_BadCredentials_ReturnsNull(string login, string pwd)
    {
        using var t = new TestDb();
        Assert.Null(t.Auth.Login(login, pwd));
    }

    [Fact]
    public void Login_IsCaseInsensitiveByLogin()
    {
        using var t = new TestDb();
        Assert.NotNull(t.Auth.Login("ADMIN", Database.DefaultAdminPassword));
    }

    [Fact]
    public void Passwords_AreNotStoredInPlainText()
    {
        using var t = new TestDb();
        using var conn = t.Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT PasswordHash FROM Users WHERE Login='admin'";
        var hash = (string)cmd.ExecuteScalar()!;
        Assert.DoesNotContain(Database.DefaultAdminPassword, hash);
    }

    [Fact]
    public void Create_Pharmacist_CanLogin_ButIsNotAdmin()
    {
        using var t = new TestDb();
        var r = t.Auth.Create(t.Admin, "ivanov", "pass123", "Иванов И.И.", Roles.Pharmacist);
        Assert.True(r.Success, r.Message);
        var u = t.Auth.Login("ivanov", "pass123");
        Assert.NotNull(u);
        Assert.False(u!.IsAdmin);
    }

    [Fact]
    public void Create_ByPharmacist_ThrowsUnauthorized_RBAC()
    {
        using var t = new TestDb();
        t.Auth.Create(t.Admin, "ivanov", "pass123", "Иванов", Roles.Pharmacist);
        var pharmacist = t.Auth.Login("ivanov", "pass123")!;
        Assert.Throws<UnauthorizedAccessException>(() => t.Auth.Create(pharmacist, "x1x", "pass123", "X", Roles.Pharmacist));
        Assert.Throws<UnauthorizedAccessException>(() => t.Auth.GetAll(pharmacist));
        Assert.Throws<UnauthorizedAccessException>(() => t.Auth.SetActive(pharmacist, 1, false));
    }

    [Fact]
    public void Create_DuplicateLogin_ReturnsError()
    {
        using var t = new TestDb();
        var r = t.Auth.Create(t.Admin, "Admin", "pass123", "Дубль", Roles.Pharmacist);
        Assert.False(r.Success);
        Assert.Contains("уже существует", r.Message);
    }

    [Theory]
    [InlineData("ab", "pass123", "ФИО")]      // короткий логин
    [InlineData("user1", "123", "ФИО")]       // короткий пароль
    [InlineData("user1", "pass123", " ")]     // пустое ФИО
    public void Create_InvalidInput_ReturnsError(string login, string pwd, string name)
    {
        using var t = new TestDb();
        Assert.False(t.Auth.Create(t.Admin, login, pwd, name, Roles.Pharmacist).Success);
    }

    [Fact]
    public void BlockedUser_CannotLogin_AndCanBeUnblocked()
    {
        using var t = new TestDb();
        t.Auth.Create(t.Admin, "ivanov", "pass123", "Иванов", Roles.Pharmacist);
        int id = t.Auth.GetAll(t.Admin).Single(u => u.Login == "ivanov").UserId;
        Assert.True(t.Auth.SetActive(t.Admin, id, false).Success);
        Assert.Null(t.Auth.Login("ivanov", "pass123"));
        Assert.True(t.Auth.SetActive(t.Admin, id, true).Success);
        Assert.NotNull(t.Auth.Login("ivanov", "pass123"));
    }

    [Fact]
    public void Admin_CannotBlockSelf()
    {
        using var t = new TestDb();
        Assert.False(t.Auth.SetActive(t.Admin, t.Admin.UserId, false).Success);
    }

    [Fact]
    public void LastActiveAdmin_CannotBeBlocked()
    {
        using var t = new TestDb();
        t.Auth.Create(t.Admin, "admin2", "pass123", "Второй админ", Roles.Admin);
        var admin2 = t.Auth.Login("admin2", "pass123")!;
        // admin2 блокирует admin — можно (остаётся сам admin2)
        Assert.True(t.Auth.SetActive(admin2, t.Admin.UserId, false).Success);
        // теперь admin2 — единственный активный администратор: себя заблокировать нельзя
        Assert.False(t.Auth.SetActive(admin2, admin2.UserId, false).Success);
    }

    [Fact]
    public void ResetPassword_ChangesCredentials()
    {
        using var t = new TestDb();
        Assert.True(t.Auth.ResetPassword(t.Admin, t.Admin.UserId, "NewPass1").Success);
        Assert.Null(t.Auth.Login("admin", Database.DefaultAdminPassword));
        Assert.NotNull(t.Auth.Login("admin", "NewPass1"));
    }
}
