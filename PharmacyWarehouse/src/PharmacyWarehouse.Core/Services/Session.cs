using PharmacyWarehouse.Core.Models;

namespace PharmacyWarehouse.Core.Services;

/// <summary>Текущий вошедший пользователь (хранится на время работы приложения).</summary>
public static class Session
{
    public static User? Current { get; private set; }
    public static bool IsAdmin => Current?.IsAdmin == true;
    public static int UserId => Current?.UserId ?? 0;
    public static string UserName => Current?.FullName ?? "";
    public static string RoleName => Current?.RoleName ?? "";

    public static void Start(User user) => Current = user;
    public static void End() => Current = null;
}
