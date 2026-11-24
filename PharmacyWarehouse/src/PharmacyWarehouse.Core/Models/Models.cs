namespace PharmacyWarehouse.Core.Models;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Pharmacist = "Pharmacist";
}

public class User
{
    public int UserId { get; set; }
    public string Login { get; set; } = "";
    public string FullName { get; set; } = "";
    public string RoleName { get; set; } = Roles.Pharmacist;
    public bool IsActive { get; set; } = true;
    public bool IsAdmin => RoleName == Roles.Admin;
}

public class Product
{
    public int ProductId { get; set; }
    public string Name { get; set; } = "";
    public string? ActiveIngredient { get; set; }
    public string? Manufacturer { get; set; }
    public string? DosageForm { get; set; }
    public decimal Price { get; set; }
    public bool RequiresPrescription { get; set; }
}

public class Batch
{
    public int BatchId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string BatchNumber { get; set; } = "";
    public int Quantity { get; set; }
    public DateTime ExpiryDate { get; set; }
    public override string ToString() => $"{ProductName}, партия {BatchNumber}, остаток {Quantity}, до {ExpiryDate:dd.MM.yyyy}";
}

public class StockOperation
{
    public int OperationId { get; set; }
    public int BatchId { get; set; }
    public string ProductName { get; set; } = "";
    public string BatchNumber { get; set; } = "";
    public string OperationType { get; set; } = "";   // IN / OUT
    public int Quantity { get; set; }
    public DateTime OperationDate { get; set; }
    public string UserName { get; set; } = "";
    public string Comment { get; set; } = "";
}

public class ExpiringBatch
{
    public string ProductName { get; set; } = "";
    public string BatchNumber { get; set; } = "";
    public int Quantity { get; set; }
    public DateTime ExpiryDate { get; set; }
    public bool IsExpired { get; set; }
    public override string ToString() =>
        $"{ProductName} (партия {BatchNumber}, {Quantity} шт.) — {(IsExpired ? "ПРОСРОЧЕНО" : "до")} {ExpiryDate:dd.MM.yyyy}";
}

/// <summary>Результат операции бизнес-логики: успех/ошибка и сообщение для пользователя.</summary>
public record OperationResult(bool Success, string Message)
{
    public static OperationResult Ok(string message = "Операция успешно выполнена.") => new(true, message);
    public static OperationResult Fail(string message) => new(false, message);
}

public record OperationResult<T>(bool Success, string Message, T? Value = default)
{
    public static OperationResult<T> Ok(T value, string message = "Операция успешно выполнена.") => new(true, message, value);
    public static OperationResult<T> Fail(string message) => new(false, message);
}
