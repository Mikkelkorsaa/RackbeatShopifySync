using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RackbeatShopifySync;

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
    /// Creates a new product in Shopify and publishes it to all available sales channels
    /// </summary>
    /// <param name="productNumber">The product number to create</param>
    /// <param name="price">The product price (default: 0)</param>
    /// <returns>Response data for the created product</returns>
    public async Task<ShopifyProductResponse> CreateProductAsync(string productNumber, decimal price = 0)
    {
        Console.WriteLine($"Creating Shopify product with number: {productNumber}");

        try
        {
            var shopifyProduct = new ShopifyProductRequest
            {
                Product = new ShopifyProductData
                {
                    Title = productNumber,
                    ProductType = "RackbeatProduct",
                    Status = "active",
                    Published = true, // Changed to true to ensure it's published
                    BodyHtml = $"Rackbeat product reference: {productNumber}",
                    Vendor = "Rackbeat",
                    Tags = $"{productNumber},configurator-component,rackbeat-product",
                    Variants = new List<ShopifyVariantData>
                    {
                        new ShopifyVariantData
                        {
                            SKU = productNumber,
                            Barcode = productNumber,
                            InventoryManagement = null, // Don't track inventory
                            Price = price,
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
                            Value = productNumber,
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

            // After creating the product, publish it to all available sales channels
            await PublishToAllSalesChannelsAsync(result.Product.Id.Value);

            Console.WriteLine($"Successfully created Shopify product with ID: {result.Product.Id}");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception in CreateProductAsync: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Publishes a product to all available sales channels
    /// </summary>
    /// <param name="productId">The ID of the product to publish</param>
    /// <returns>A task representing the asynchronous operation</returns>
    private async Task PublishToAllSalesChannelsAsync(long productId)
    {
        try
        {
            // First, get all available sales channels
            var channelsResponse = await _httpClient.GetAsync($"{_baseUrl}sales_channels.json");
            if (!channelsResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to get sales channels: {channelsResponse.StatusCode}");
                return;
            }
            
            var channelsContent = await channelsResponse.Content.ReadAsStringAsync();
            var salesChannels = JsonSerializer.Deserialize<SalesChannelsResponse>(channelsContent, _serializerOptions);
            
            if (salesChannels?.SalesChannels == null || salesChannels.SalesChannels.Count == 0)
            {
                Console.WriteLine("No sales channels found");
                return;
            }
            
            // For each sales channel, publish the product
            foreach (var channel in salesChannels.SalesChannels)
            {
                Console.WriteLine($"Publishing product {productId} to channel: {channel.Name} (ID: {channel.Id})");
                
                var publishData = new
                {
                    product_publication = new
                    {
                        product_id = productId,
                        channel_id = channel.Id,
                        published = true
                    }
                };
                
                var json = JsonSerializer.Serialize(publishData, _serializerOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var publishResponse = await _httpClient.PostAsync($"{_baseUrl}product_publications.json", content);
                
                if (!publishResponse.IsSuccessStatusCode)
                {
                    var errorContent = await publishResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"Failed to publish to channel {channel.Name}: {publishResponse.StatusCode}");
                    Console.WriteLine($"Error details: {errorContent}");
                }
                else
                {
                    Console.WriteLine($"Successfully published product to channel {channel.Name}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error publishing to sales channels: {ex.Message}");
            // Continue execution rather than letting the exception bubble up
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
    
    [JsonPropertyName("price")] public decimal Price { get; set; }
    
    [JsonPropertyName("compare_at_price")] public decimal? CompareAtPrice { get; set; }
    
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

// Add new classes for sales channels
public class SalesChannelsResponse
{
    [JsonPropertyName("sales_channels")]
    public List<SalesChannel> SalesChannels { get; set; } = new List<SalesChannel>();
}

public class SalesChannel
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}