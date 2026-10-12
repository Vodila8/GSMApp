namespace gsm.Data;

public class ServiceOrderLine
{
    public int Id { get; set; }

    public string CompanyId { get; set; } = string.Empty;

    public int ServiceOrderId { get; set; }

    public ServiceOrder ServiceOrder { get; set; } = null!;

    public int? WarehouseItemId { get; set; }

    public WarehouseItem? WarehouseItem { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public bool IsWarehousePart { get; set; }
}
