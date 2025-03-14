using System.Text.Json.Serialization;

namespace RackbeatShopifySync;

public class ProductsResponse
{
    [JsonPropertyName("products")]
    public List<Product>? Products { get; set; }
}

public class Product
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }
    [JsonPropertyName("number")]
    public string? Number { get; set; }
    [JsonPropertyName("urlfriendly_number")]
    public string? UrlfriendlyNumber { get; set; }
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    [JsonPropertyName("barcode")]
    public string? Barcode { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    [JsonPropertyName("sales_price")]
    public decimal? SalesPrice { get; set; }
    [JsonPropertyName("recommended_sales_price")]
    public decimal? RecommendedSalesPrice { get; set; }
    [JsonPropertyName("sales_profit")]
    public decimal? SalesProfit { get; set; }
    [JsonPropertyName("cost_price")]
    public decimal? CostPrice { get; set; }
    [JsonPropertyName("recommended_cost_price")]
    public decimal? RecommendedCostPrice { get; set; }
    [JsonPropertyName("cost_addition_percentage")]
    public decimal? CostAdditionPercentage { get; set; }
    [JsonPropertyName("min_order")]
    public int? MinOrder { get; set; }
    [JsonPropertyName("min_sales")]
    public int? MinSales { get; set; }
    [JsonPropertyName("min_stock")]
    public int? MinStock { get; set; }
    [JsonPropertyName("stock_quantity")]
    public int? StockQuantity { get; set; }
    [JsonPropertyName("in_order_quantity")]
    public int? InOrderQuantity { get; set; }
    [JsonPropertyName("available_quantity")]
    public int? AvailableQuantity { get; set; }
    [JsonPropertyName("purchased_quantity")]
    public int? PurchasedQuantity { get; set; }
    [JsonPropertyName("used_in_production_quantity")]
    public int? UsedInProductionQuantity { get; set; }
    [JsonPropertyName("default_supplier_id")]
    public int? DefaultSupplierId { get; set; }
    [JsonPropertyName("default_location")]
    public Location? DefaultLocation { get; set; }
    [JsonPropertyName("picture_url")]
    public string? PictureUrl { get; set; }
    [JsonPropertyName("pictures")]
    public Pictures? Pictures { get; set; }
    [JsonPropertyName("is_barred")]
    public bool? IsBarred { get; set; }
    [JsonPropertyName("is_convertable")]
    public bool? IsConvertable { get; set; }
    [JsonPropertyName("inventory_enabled")]
    public bool? InventoryEnabled { get; set; }
    [JsonPropertyName("should_assure_quality")]
    public bool? ShouldAssureQuality { get; set; }
    [JsonPropertyName("group_id")]
    public int? GroupId { get; set; }
    [JsonPropertyName("group")]
    public Group? Group { get; set; }
    [JsonPropertyName("physical")]
    public Physical? Physical { get; set; }
    [JsonPropertyName("serial_numbers")]
    public SerialNumbers? SerialNumbers { get; set; }
    [JsonPropertyName("batch_control")]
    public BatchControl? BatchControl { get; set; }
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }
    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }
    [JsonPropertyName("deleted_at")]
    public DateTime? DeletedAt { get; set; }
    [JsonPropertyName("pdf_url")]
    public string? PdfUrl { get; set; }
    [JsonPropertyName("department_id")]
    public int? DepartmentId { get; set; }
    [JsonPropertyName("goods_code")]
    public string? GoodsCode { get; set; }
    [JsonPropertyName("country_code")]
    public string? CountryCode { get; set; }
    [JsonPropertyName("self")]
    public string? Self { get; set; }
}

public class Location
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("number")]
    public int? Number { get; set; }
    [JsonPropertyName("barcode")]
    public string? Barcode { get; set; }
    [JsonPropertyName("is_default")]
    public bool? IsDefault { get; set; }
    [JsonPropertyName("parent_id")]
    public int? ParentId { get; set; }
    [JsonPropertyName("children_count")]
    public int? ChildrenCount { get; set; }
    [JsonPropertyName("toplevel_parent_id")]
    public int? TopLevelParentId { get; set; }
    [JsonPropertyName("nesting_level")]
    public int? NestingLevel { get; set; }
    [JsonPropertyName("nest_list")]
    public List<NestList>? NestList { get; set; }
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }
    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }
    [JsonPropertyName("self")]
    public string? Self { get; set; }
}

public class Group
{
    [JsonPropertyName("number")]
    public int? Number { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("vat_abroad")]
    public decimal? VatAbroad { get; set; }
    [JsonPropertyName("vat_eu")]
    public decimal? VatEu { get; set; }
    [JsonPropertyName("vat_domestic")]
    public decimal? VatDomestic { get; set; }
    [JsonPropertyName("vat_domestic_exempt")]
    public decimal? VatDomesticExempt { get; set; }
    [JsonPropertyName("self")]
    public string? Self { get; set; }
}

public class Pictures
{
    [JsonPropertyName("thumb")]
    public string? Thumb { get; set; }
    [JsonPropertyName("display")]
    public string? Display { get; set; }
    [JsonPropertyName("large")]
    public string? Large { get; set; }
    [JsonPropertyName("original")]
    public string? Original { get; set; }
}

public class Physical
{
    [JsonPropertyName("weight")]
    public decimal? Weight { get; set; }
    [JsonPropertyName("weight_unit")]
    public string? WeightUnit { get; set; }
    [JsonPropertyName("height")]
    public decimal? Height { get; set; }
    [JsonPropertyName("width")]
    public decimal? Width { get; set; }
    [JsonPropertyName("depth")]
    public decimal? Depth { get; set; }
    [JsonPropertyName("size_unit")]
    public string? SizeUnit { get; set; }
}

public class SerialNumbers
{
    [JsonPropertyName("is_active")]
    public bool? IsActive { get; set; }
    [JsonPropertyName("index")]
    public string? Index { get; set; }
}

public class BatchControl
{
    [JsonPropertyName("is_active")]
    public bool? IsActive { get; set; }
}

public class NestList
{
    [JsonPropertyName("number")]
    public int? Number { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("children_count")]
    public int? ChildrenCount { get; set; }
}