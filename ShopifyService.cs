using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Linq;

namespace RackbeatShopifySync;

public class StringDecimalConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            string stringValue = reader.GetString();
            if (decimal.TryParse(stringValue, out decimal value))
            {
                return value;
            }
            return 0;
        }
        else if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetDecimal();
        }
        
        return 0;
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
    }
}

public class NullableStringDecimalConverter : JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }
        else if (reader.TokenType == JsonTokenType.String)
        {
            string stringValue = reader.GetString();
            if (string.IsNullOrEmpty(stringValue))
            {
                return null;
            }
            if (decimal.TryParse(stringValue, out decimal value))
            {
                return value;
            }
            return null;
        }
        else if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetDecimal();
        }
        
        return null;
    }

    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteStringValue(value.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}

public class ShopifyService
{
    private readonly HttpClient _httpClient;
    private readonly string _accessToken;
    private readonly string _shopName;
    private readonly string _baseUrl;
    private readonly JsonSerializerOptions _serializerOptions;

    public ShopifyService(string shopName, string accessToken)
    {
        _shopName = shopName;
        _accessToken = accessToken;
        _baseUrl = $"https://{_shopName}.myshopify.com/admin/api/2024-01/"; // Using a specific version

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Token auth for Shopify as shown in the curl example
        _httpClient.DefaultRequestHeaders.Add("X-Shopify-Access-Token", _accessToken);

        // Serializer options to handle property name casing in a consistent manner
        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
        
        // Add our custom converters to handle string price values
        _serializerOptions.Converters.Add(new StringDecimalConverter());
        _serializerOptions.Converters.Add(new NullableStringDecimalConverter());
    }

    /// <summary>
    /// Retrieves a list of all products from Shopify
    /// </summary>
    /// <param name="limit">Maximum number of products to retrieve (default: 250)</param>
    /// <returns>A list of product data objects</returns>
    public async Task<List<ShopifyProductData>> GetProductsAsync(int limit = 250)
    {
        Console.WriteLine($"Fetching products from Shopify store '{_shopName}'...");

        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}products.json?limit={limit}");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Shopify API Error: {response.StatusCode}");
                Console.WriteLine($"Response: {responseContent}");
                throw new HttpRequestException(
                    $"Failed to get products from Shopify: {response.StatusCode} - {responseContent}");
            }

            var productsResponse =
                JsonSerializer.Deserialize<ShopifyProductsListResponse>(responseContent, _serializerOptions);
            return productsResponse?.Products ?? new List<ShopifyProductData>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception in GetProductsAsync: {ex.Message}");
            throw;
        }
    }

    
    /// <summary>
    /// Creates a new product in Shopify
    /// </summary>
    /// <param name="productNumber">The product number to create</param>
    /// <param name="price">The product price from Rackbeat (default: 0)</param>
    /// <returns>Response data for the created product</returns>
    public async Task<ShopifyProductResponse> CreateProductAsync(Product product)
    {
        Console.WriteLine($"Creating Shopify product with number: {product.Number}, price: {product.SalesPrice}");

        try
        {
            var shopifyProduct = new ShopifyProductRequest
            {
                Product = new ShopifyProductData
                {
                    Title = product.Number,
                    ProductType = "RackbeatProduct",
                    Status = "active",
                    Published = true,
                    BodyHtml = $"Rackbeat product reference: {product.Number}",
                    Vendor = "Rackbeat",
                    Tags = $"{product.Number},configurator-component,rackbeat-product",
                    Variants = new List<ShopifyVariantData>
                    {
                        new ShopifyVariantData
                        {
                            SKU = product.Number,
                            Barcode = product.Number,
                            InventoryManagement = null, // Don't track inventory
                            Price = product.SalesPrice,  // Use the provided price from Rackbeat
                            CompareAtPrice = null,
                            RequiresShipping = true,
                            Taxable = true,
                            Option1 = "Default",
                        }
                    },
                    Metafields = new List<ShopifyMetafieldData>
                    {
                        new ShopifyMetafieldData
                        {
                            Namespace = "rackbeat",
                            Key = "product_id",
                            Value = product.Number,
                            Type = "single_line_text_field"
                        },
                        new ShopifyMetafieldData
                        {
                            Namespace = "configurator",
                            Key = "component_type",
                            Value = "standard",
                            Type = "single_line_text_field"
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(shopifyProduct, _serializerOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}products.json", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Shopify API Error: {response.StatusCode}");
                Console.WriteLine($"Request URL: {_baseUrl}products.json");
                Console.WriteLine($"Request Body: {json}");
                Console.WriteLine($"Response: {responseContent}");
                throw new HttpRequestException($"Product creation failed: {response.StatusCode} - {responseContent}");
            }

            var result = JsonSerializer.Deserialize<ShopifyProductResponse>(responseContent, _serializerOptions);
            if (result?.Product == null)
            {
                throw new Exception("Failed to deserialize Shopify response or product data is missing");
            }

            Console.WriteLine($"Successfully created Shopify product with ID: {result.Product.Id}, Price: {product.SalesPrice}");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception in CreateProductAsync: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Searches for a product by title or tags
    /// </summary>
    /// <param name="query">The search query (product number)</param>
    /// <returns>List of matching products</returns>
    public async Task<List<ShopifyProductData>> SearchProductsAsync(string query)
    {
        Console.WriteLine($"Searching for Shopify product: {query}");

        try
        {
            // Shopify's search is an OR operation across several fields
            var response = await _httpClient.GetAsync($"{_baseUrl}products.json?title={Uri.EscapeDataString(query)}");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Shopify API Error: {response.StatusCode}");
                Console.WriteLine($"Response: {responseContent}");
                throw new HttpRequestException($"Failed to search products: {response.StatusCode} - {responseContent}");
            }

            var productsResponse =
                JsonSerializer.Deserialize<ShopifyProductsListResponse>(responseContent, _serializerOptions);
            return productsResponse?.Products ?? new List<ShopifyProductData>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception in SearchProductsAsync: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Updates an existing product in Shopify
    /// </summary>
    /// <param name="productId">The Shopify product ID to update</param>
    /// <param name="productNumber">The product number to update</param>
    /// <param name="price">The product price from Rackbeat (default: 0)</param>
    /// <returns>Response data for the updated product</returns>
    public async Task<ShopifyProductResponse> UpdateProductAsync(long productId, Product product)
    {
        Console.WriteLine($"Updating Shopify product with ID: {productId}, Number: {product.Number}, Price: {product.SalesPrice}");

        try
        {
            var shopifyProduct = new ShopifyProductRequest
            {
                Product = new ShopifyProductData
                {
                    Id = productId,
                    Title = product.Number,
                    ProductType = "RackbeatProduct",
                    Status = "active",
                    Published = true,
                    BodyHtml = $"Rackbeat product reference: {product.Number}",
                    Vendor = "Rackbeat",
                    Tags = $"{product.Number},configurator-component,rackbeat-product",
                    Variants = new List<ShopifyVariantData>
                    {
                        new ShopifyVariantData
                        {
                            SKU = product.Number,
                            Barcode = product.Number,
                            InventoryManagement = null, // Don't track inventory
                            Price = product.SalesPrice,  // Use the provided price from Rackbeat
                            CompareAtPrice = null,
                            RequiresShipping = true,
                            Taxable = true,
                            Option1 = "Default",
                        }
                    },
                    Metafields = new List<ShopifyMetafieldData>
                    {
                        new ShopifyMetafieldData
                        {
                            Namespace = "rackbeat",
                            Key = "product_id",
                            Value = product.Number,
                            Type = "single_line_text_field"
                        },
                        new ShopifyMetafieldData
                        {
                            Namespace = "configurator",
                            Key = "component_type",
                            Value = "standard",
                            Type = "single_line_text_field"
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(shopifyProduct, _serializerOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync($"{_baseUrl}products/{productId}.json", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Shopify API Error: {response.StatusCode}");
                Console.WriteLine($"Request URL: {_baseUrl}products/{productId}.json");
                Console.WriteLine($"Request Body: {json}");
                Console.WriteLine($"Response: {responseContent}");
                throw new HttpRequestException($"Product update failed: {response.StatusCode} - {responseContent}");
            }

            var result = JsonSerializer.Deserialize<ShopifyProductResponse>(responseContent, _serializerOptions);
            if (result?.Product == null)
            {
                throw new Exception("Failed to deserialize Shopify response or product data is missing");
            }

            Console.WriteLine($"Successfully updated Shopify product with ID: {result.Product.Id}, Price: {product.SalesPrice}");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception in UpdateProductAsync: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Creates or updates a product in Shopify (overwrite if exists)
    /// </summary>
    /// <param name="productNumber">The product number to create or update</param>
    /// <param name="price">The product price from Rackbeat (default: 0)</param>
    /// <returns>Response data for the created/updated product</returns>
    public async Task<ShopifyProductResponse> CreateOrUpdateProductAsync(Product product)
    {
        Console.WriteLine($"Attempting to create or update product: {product.Number}, Price: {product.SalesPrice}");
        
        try
        {
            // First, search for the product by its number
            var existingProducts = await SearchProductsAsync(product.Number);
            await Task.Delay(500);
            
            // Check if we found an exact match (by title)
            var exactMatch = existingProducts.FirstOrDefault(p => p.Title == product.Number);
            
            if (exactMatch != null && exactMatch.Id.HasValue)
            {
                Console.WriteLine($"Found existing product '{product.Number}' with ID: {exactMatch.Id}. Updating with price: {product.SalesPrice}...");
                return await UpdateProductAsync(exactMatch.Id.Value, product);
                
            }
            else
            {
                Console.WriteLine($"No existing product found for '{product.Number}'. Creating new product with price: {product.SalesPrice}...");
                return await CreateProductAsync(product);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception in CreateOrUpdateProductAsync: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Executes a GraphQL query against Shopify's GraphQL API
    /// </summary>
    /// <param name="query">The GraphQL query</param>
    /// <returns>The response as a string</returns>
    public async Task<string> ExecuteGraphQLQueryAsync(string query)
    {
        try
        {
            var graphQLRequest = new { query = query };
            var json = JsonSerializer.Serialize(graphQLRequest, _serializerOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var graphQLUrl = $"https://{_shopName}.myshopify.com/admin/api/2024-01/graphql.json";
            var response = await _httpClient.PostAsync(graphQLUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Shopify GraphQL Error: {response.StatusCode}");
                Console.WriteLine($"Response: {responseContent}");
                throw new HttpRequestException($"GraphQL request failed: {response.StatusCode} - {responseContent}");
            }

            return responseContent;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception in ExecuteGraphQLQueryAsync: {ex.Message}");
            throw;
        }
    }
}

public class ShopifyProductRequest
{
    [JsonPropertyName("product")] public ShopifyProductData? Product { get; set; }
}

public class ShopifyProductResponse
{
    [JsonPropertyName("product")] public ShopifyProductData? Product { get; set; }
}

public class ShopifyProductData
{
    [JsonPropertyName("id")] public long? Id { get; set; }

    [JsonPropertyName("title")] public string? Title { get; set; }

    [JsonPropertyName("body_html")] public string? BodyHtml { get; set; }

    [JsonPropertyName("vendor")] public string? Vendor { get; set; }

    [JsonPropertyName("product_type")] public string? ProductType { get; set; }

    [JsonPropertyName("status")] public string? Status { get; set; }

    [JsonPropertyName("published")] public bool Published { get; set; }

    [JsonPropertyName("tags")] public string? Tags { get; set; }
    
    [JsonPropertyName("variants")] public List<ShopifyVariantData>? Variants { get; set; }
    
    [JsonPropertyName("metafields")] public List<ShopifyMetafieldData>? Metafields { get; set; }
}

public class ShopifyProductsListResponse
{
    [JsonPropertyName("products")]
    public List<ShopifyProductData> Products { get; set; } = new List<ShopifyProductData>();
}

public class ShopifyVariantData
{
    [JsonPropertyName("id")] public long? Id { get; set; }
    
    [JsonPropertyName("product_id")] public long? ProductId { get; set; }
    
    [JsonPropertyName("title")] public string? Title { get; set; }
    
    [JsonPropertyName("sku")] public string? SKU { get; set; }
    
    [JsonPropertyName("barcode")] public string? Barcode { get; set; }
    
    [JsonPropertyName("inventory_management")] public string? InventoryManagement { get; set; }
    
    [JsonPropertyName("price")]
    [JsonConverter(typeof(StringDecimalConverter))]
    public decimal? Price { get; set; }
    
    [JsonPropertyName("compare_at_price")]
    [JsonConverter(typeof(NullableStringDecimalConverter))]
    public decimal? CompareAtPrice { get; set; }
    
    [JsonPropertyName("requires_shipping")] public bool RequiresShipping { get; set; }
    
    [JsonPropertyName("taxable")] public bool Taxable { get; set; }
    
    [JsonPropertyName("option1")] public string? Option1 { get; set; }
}

public class ShopifyMetafieldData
{
    [JsonPropertyName("id")] public long? Id { get; set; }
    
    [JsonPropertyName("namespace")] public string? Namespace { get; set; }
    
    [JsonPropertyName("key")] public string? Key { get; set; }
    
    [JsonPropertyName("value")] public string? Value { get; set; }
    
    [JsonPropertyName("type")] public string? Type { get; set; }
}