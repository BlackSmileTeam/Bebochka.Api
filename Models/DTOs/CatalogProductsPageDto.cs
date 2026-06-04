namespace Bebochka.Api.Models.DTOs;

public class CatalogFacetDto
{
    public List<string> Brands { get; set; } = new();
    public List<string> Sizes { get; set; } = new();
    public List<string> Colors { get; set; } = new();
    public List<string> Genders { get; set; } = new();
    public List<string> Conditions { get; set; } = new();
}

public class CatalogProductsPageDto
{
    public List<ProductDto> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasMore { get; set; }
    public CatalogFacetDto? Facets { get; set; }
}
