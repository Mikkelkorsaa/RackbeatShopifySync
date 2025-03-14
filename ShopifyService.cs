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
                throw new HttpRequestException($"Failed to get products from Shopify: {response.StatusCode} - {responseContent}");
            }
            
            var productsResponse = JsonSerializer.Deserialize<ShopifyProductsListResponse>(responseContent, _serializerOptions);
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
    /// <returns>Response data for the created product</returns>
    public async Task<ShopifyProductResponse> CreateProductAsync(string productNumber)
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
                    Status = "draft", // Not visible in store
                    Published = false,
                    BodyHtml = $"Rackbeat product reference: {productNumber}",
                    Vendor = "Rackbeat",
                    Tags = productNumber // Add the product number as a tag for easier searching
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
            
            var productsResponse = JsonSerializer.Deserialize<ShopifyProductsListResponse>(responseContent, _serializerOptions);
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
    [JsonPropertyName("product")]
    public ShopifyProductData? Product { get; set; }
}

public class ShopifyProductResponse
{
    [JsonPropertyName("product")]
    public ShopifyProductData? Product { get; set; }
}

public class ShopifyProductData
{
    [JsonPropertyName("id")]
    public long? Id { get; set; }
    
    [JsonPropertyName("title")]
    public string? Title { get; set; }
    
    [JsonPropertyName("body_html")]
    public string? BodyHtml { get; set; }
    
    [JsonPropertyName("vendor")]
    public string? Vendor { get; set; }
    
    [JsonPropertyName("product_type")]
    public string? ProductType { get; set; }
    
    [JsonPropertyName("status")]
    public string? Status { get; set; }
    
    [JsonPropertyName("published")]
    public bool Published { get; set; }
    
    [JsonPropertyName("tags")]
    public string? Tags { get; set; }
}

public class ShopifyProductsListResponse
{
    [JsonPropertyName("products")]
    public List<ShopifyProductData> Products { get; set; } = new List<ShopifyProductData>();
}