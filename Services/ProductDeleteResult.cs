namespace Bebochka.Api.Services;

public enum ProductDeleteResult
{
    NotFound,
    Deleted,
    ReferencedInOrders,
}
