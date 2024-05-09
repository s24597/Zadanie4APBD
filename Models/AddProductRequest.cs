namespace Zadanie4APBD.Models;

public class AddProductRequest
{
    public int IdProduct { get; set; }
    public int IdWarehouse { get; set; }
    public int Amount { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal Price { get; set; }
    public int IdOrder { get; set; }
    
}