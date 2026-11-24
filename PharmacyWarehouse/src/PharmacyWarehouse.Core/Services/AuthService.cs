using Microsoft.Data.Sqlite;
using PharmacyWarehouse.Core.Data;
using PharmacyWarehouse.Core.Models;

namespace PharmacyWarehouse.Core.Services;

/// <summary>Аутентификация и управление пользователями. Управление — только для роли Admin.</summary>
public class AuthService
{
    private readonly Database _db;
    public AuthService(Database db) => _db = db;

    private const string SelectUser = @"SELECT u.UserId, u.Login, u.FullName, r.RoleName, u.IsActive, u.PasswordHash, u.Salt
                                        FROM Users u JOIN Roles r ON r.RoleId = u.RoleId";

    /// <summary>Возвращает пользователя при верных данных и активной учётной записи, иначе null.</summary>
    public User? Login(string login, string password)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(password)) return null;
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = SelectUser + " WHERE u.Login = $l";
        cmd.Parameters.AddWithValue("$l", login.Trim());
        using var rd = cmd.ExecuteReader();
        if (!rd.Read()) return null;
        var user = ReadUser(rd);
        if (!user.IsActive) return null;
        return PasswordHasher.Verify(password, rd.GetString(5), rd.GetString(6)) ? user : null;
    }

    public List<User> GetAll(User actor)
    {
        RequireAdmin(actor);
        var list = new List<User>();
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = SelectUser + " ORDER BY u.Login";
        using var rd = cmd.ExecuteReader();
        while (rd.Read()) list.Add(ReadUser(rd));
        return list;
    }

    public OperationResult Create(User actor, string login, string password, string fullName, string roleName)
    {
        RequireAdmin(actor);
        login = (login ?? "").Trim(); fullName = (fullName ?? "").Trim();
        if (login.Length < 3) return OperationResult.Fail("Логин должен содержать не менее 3 символов.");
        if (fullName.Length == 0) return OperationResult.Fail("Укажите ФИО сотрудника.");
        var pwdError = ValidatePassword(password);
        if (pwdError != null) return OperationResult.Fail(pwdError);
        if (roleName != Roles.Admin && roleName != Roles.Pharmacist) return OperationResult.Fail("Неизвестная роль.");

        var (hash, salt) = PasswordHasher.CreateHash(password);
        try
        {
            using var conn = _db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO Users (Login, PasswordHash, Salt, FullName, RoleId)
                                VALUES ($l, $h, $s, $f, (SELECT RoleId FROM Roles WHERE RoleName = $r))";
            cmd.Parameters.AddWithValue("$l", login);
            cmd.Parameters.AddWithValue("$h", hash);
            cmd.Parameters.AddWithValue("$s", salt);
            cmd.Parameters.AddWithValue("$f", fullName);
            cmd.Parameters.AddWithValue("$r", roleName);
            cmd.ExecuteNonQuery();
            return OperationResult.Ok("Пользователь создан.");
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            return OperationResult.Fail("Пользователь с таким логином уже существует.");
        }
    }

    /// <summary>Блокировка/разблокировка. Нельзя заблокировать самого себя и последнего активного администратора.</summary>
    public OperationResult SetActive(User actor, int userId, bool active)
    {
        RequireAdmin(actor);
        if (!active && userId == actor.UserId) return OperationResult.Fail("Нельзя заблокировать собственную учётную запись.");
        using var conn = _db.Open();
        if (!active)
        {
            using var chk = conn.CreateCommand();
            chk.CommandText = @"SELECT COUNT(*) FROM Users u JOIN Roles r ON r.RoleId = u.RoleId
                                WHERE r.RoleName = 'Admin' AND u.IsActive = 1 AND u.UserId <> $id";
            chk.Parameters.AddWithValue("$id", userId);
            using var isAdm = conn.CreateCommand();
            isAdm.CommandText = "SELECT COUNT(*) FROM Users u JOIN Roles r ON r.RoleId=u.RoleId WHERE u.UserId=$id AND r.RoleName='Admin'";
            isAdm.Parameters.AddWithValue("$id", userId);
            if (Convert.ToInt32(isAdm.ExecuteScalar()) == 1 && Convert.ToInt32(chk.ExecuteScalar()) == 0)
                return OperationResult.Fail("Нельзя заблокировать последнего активного администратора.");
        }
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Users SET IsActive = $a WHERE UserId = $id";
        cmd.Parameters.AddWithValue("$a", active ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", userId);
        return cmd.ExecuteNonQuery() == 1
            ? OperationResult.Ok(active ? "Пользователь разблокирован." : "Пользователь заблокирован.")
            : OperationResult.Fail("Пользователь не найден.");
    }

    public OperationResult ResetPassword(User actor, int userId, string newPassword)
    {
        RequireAdmin(actor);
        var err = ValidatePassword(newPassword);
        if (err != null) return OperationResult.Fail(err);
        var (hash, salt) = PasswordHasher.CreateHash(newPassword);
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Users SET PasswordHash=$h, Salt=$s WHERE UserId=$id";
        cmd.Parameters.AddWithValue("$h", hash);
        cmd.Parameters.AddWithValue("$s", salt);
        cmd.Parameters.AddWithValue("$id", userId);
        return cmd.ExecuteNonQuery() == 1 ? OperationResult.Ok("Пароль изменён.") : OperationResult.Fail("Пользователь не найден.");
    }

    public static string? ValidatePassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 6) return "Пароль должен содержать не менее 6 символов.";
        return null;
    }

    private static void RequireAdmin(User actor)
    {
        if (actor == null || !actor.IsAdmin)
            throw new UnauthorizedAccessException("Действие доступно только администратору.");
    }

    private static User ReadUser(SqliteDataReader rd) => new()
    {
        UserId = rd.GetInt32(0), Login = rd.GetString(1), FullName = rd.GetString(2),
        RoleName = rd.GetString(3), IsActive = rd.GetInt32(4) == 1
    };
}
