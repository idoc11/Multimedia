using PharmacyWarehouse.Core.Models;

namespace PharmacyWarehouse.Tests;

public class ProductTests
{
    [Theory]
    [InlineData("", 10)]
    [InlineData("   ", 10)]
    [InlineData("Аспирин", -1)]
    public void Add_InvalidProduct_Fails(string name, int price)
    {
        using var t = new TestDb();
        Assert.False(t.Products.Add(new Product { Name = name, Price = price }).Success);
    }

    [Fact]
    public void Crud_AddReadUpdateDelete_Works()
    {
        using var t = new TestDb();
        var add = t.Products.Add(new Product
        {
            Name = "Аспирин", ActiveIngredient = "Ацетилсалициловая кислота", Manufacturer = "Байер",
            DosageForm = "таблетки", Price = 150.5m, RequiresPrescription = false
        });
        Assert.True(add.Success);
        var p = t.Products.Get(add.Value)!;
        Assert.Equal("Аспирин", p.Name);
        Assert.Equal(150.5m, p.Price);

        p.Price = 200m; p.RequiresPrescription = true;
        Assert.True(t.Products.Update(p).Success);
        var again = t.Products.Get(p.ProductId)!;
        Assert.Equal(200m, again.Price);
        Assert.True(again.RequiresPrescription);

        Assert.True(t.Products.Delete(p.ProductId).Success);
        Assert.Null(t.Products.Get(p.ProductId));
    }

    [Fact]
    public void Delete_ProductWithBatches_IsRejected()
    {
        using var t = new TestDb();
        int pid = t.AddProduct();
        t.AddBatch(pid, 5);
        var r = t.Products.Delete(pid);
        Assert.False(r.Success);
        Assert.NotNull(t.Products.Get(pid));
    }

    [Fact]
    public void Search_FindsByNameAndIngredient()
    {
        using var t = new TestDb();
        t.Products.Add(new Product { Name = "Нурофен", ActiveIngredient = "Ибупрофен", Price = 1 });
        t.Products.Add(new Product { Name = "Аспирин", ActiveIngredient = "Ацетилсалициловая кислота", Price = 1 });
        Assert.Single(t.Products.GetAll("нуро"));
        Assert.Single(t.Products.GetAll("ибупрофен"));
        Assert.Equal(2, t.Products.GetAll().Count);
    }

    [Fact]
    public void Update_NonExistent_Fails()
    {
        using var t = new TestDb();
        Assert.False(t.Products.Update(new Product { ProductId = 999, Name = "X", Price = 1 }).Success);
    }
}
