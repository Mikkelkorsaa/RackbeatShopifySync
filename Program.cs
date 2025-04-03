using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using RackbeatShopifySync;

class Program
{
    private static readonly HttpClient httpClient = new HttpClient();

    // Configuration variables
    private static string shopifyAccessToken = "";
    private static string shopifyShopName = "arch-plus-test";


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
                int skipped = 0;


                // Now you can iterate through products and process them
                foreach (var product in products)
                {
                    Console.WriteLine($"Processing: {product.Name}, Number: {product.Number}");

                    try
                    {
                        // First check if the product exists by searching for it directly
                        var searchResults = await shopifyService.SearchProductsAsync(product.Number);
                        bool exists = searchResults.Any(p =>
                            p.Title?.Equals(product.Number, StringComparison.OrdinalIgnoreCase) == true ||
                            (p.Tags != null && p.Tags.Contains(product.Number)));

                        if (exists)
                        {
                            Console.WriteLine($"Product {product.Number} already exists in Shopify. Skipping...");
                            skipped++;
                            continue;
                        }

                        Console.WriteLine($"Syncing product {product.Number} to Shopify...");

                        try
                        {
                            var shopifyProduct = await shopifyService.CreateProductAsync(product.Number, product.SalesPrice ?? 0);
                            Console.WriteLine($"Created Shopify product with ID: {shopifyProduct.Product?.Id}, Title: {shopifyProduct.Product?.Title}");
                            created++;

                            // Add a small delay to avoid rate limiting
                            await Task.Delay(500);
                        }
                        catch (HttpRequestException ex) when (ex.Message.Contains("422"))
                        {
                            // 422 often means this product already exists but wasn't caught by our search
                            Console.WriteLine($"Product {product.Number} appears to already exist (422 error). Skipping...");
                            skipped++;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error syncing product {product.Number} to Shopify: {ex.Message}");
                            if (ex.InnerException != null)
                            {
                                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error searching for product {product.Number}: {ex.Message}");
                    }
                }

                Console.WriteLine($"Sync summary: {created} products created, {skipped} products skipped (already exist)");
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

