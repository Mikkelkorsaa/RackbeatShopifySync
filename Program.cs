using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using RackbeatShopifySync;

class Program
{
    private static readonly HttpClient httpClient = new HttpClient();
    
    // Configuration variables
    private static string shopifyAccessToken = "";
    private static string shopifyShopName = "arch-plus";
    

    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting Rackbeat to Shopify product sync");
        
        try
        {
            var shopifyService = new ShopifyService(shopifyShopName, shopifyAccessToken);
            
            // Get products from Rackbeat
            Console.WriteLine("Fetching products from Rackbeat...");
            var response = await httpClient.GetAsync("http://api.archplus.dk/ArchPlus/products");
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to fetch products from Rackbeat. HTTP Status: {response.StatusCode}");
                return;
            }
            var responseContent = await response.Content.ReadAsStringAsync();
            
            // Use the correct property name for deserialization
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            var productsResponse = JsonSerializer.Deserialize<ProductsResponse>(responseContent, options);
            var products = productsResponse?.Products;
            
            // Verify products were deserialized correctly
            if (products != null)
            {
                Console.WriteLine($"Successfully fetched {products.Count} products from Rackbeat");
                
                int created = 0;
                int updated = 0;
                int errors = 0;
                
                // Now you can iterate through products and process them
                foreach (var product in products)
                {
                    Console.WriteLine($"Processing: {product.Name}, Number: {product.Number}");
                    
                    try {
                        // Get the price from Rackbeat product
                        decimal price = 0;
                        
                        // Use the SalesPrice if available, otherwise default to 0
                        if (product.SalesPrice.HasValue)
                        {
                            price = product.SalesPrice.Value;
                            Console.WriteLine($"Using Rackbeat price: {price} for product {product.Number}");
                        }
                        else
                        {
                            Console.WriteLine($"No price found for product {product.Number}. Using default price 0.");
                        }
                        
                        // Use the new CreateOrUpdateProductAsync method with the price
                        // This will overwrite the product if it exists instead of skipping it
                        var result = await shopifyService.CreateOrUpdateProductAsync(product);
                        await Task.Delay(500);
                        
                        // Determine if this was a creation or update operation
                        var searchResults = await shopifyService.SearchProductsAsync(product.Number);
                        Task.Delay(500);
                        bool existedBefore = searchResults.Count > 1 || 
                            (searchResults.Count == 1 && searchResults[0].Id != result.Product?.Id);
                        
                        if (existedBefore)
                        {
                            Console.WriteLine($"Updated existing Shopify product with ID: {result.Product?.Id}, Title: {result.Product?.Title}");
                            updated++;
                        }
                        else
                        {
                            Console.WriteLine($"Created new Shopify product with ID: {result.Product?.Id}, Title: {result.Product?.Title}");
                            created++;
                        }
                        
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing product {product.Number}: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                        }
                        errors++;
                    }
                }
                
                Console.WriteLine($"Sync summary: {created} products created, {updated} products updated, {errors} errors");
            }
            else
            {
                Console.WriteLine("No products found or deserialization failed");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during sync: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
        
        Console.WriteLine("Sync completed");
    }
}