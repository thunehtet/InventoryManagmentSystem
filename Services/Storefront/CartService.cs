using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace ClothInventoryApp.Services.Storefront
{
    /// <summary>
    /// Session-backed shopping cart scoped per tenant code so carts do not leak across tenants.
    /// Carts persist for the session's idle timeout (see Program.cs Session config).
    /// </summary>
    public class CartService : ICartService
    {
        private const string SessionKeyPrefix = "storefront:cart:";

        public Cart GetCart(ISession session, string tenantCode)
        {
            var key = SessionKeyPrefix + tenantCode.ToUpperInvariant();
            var json = session.GetString(key);
            if (string.IsNullOrEmpty(json))
                return new Cart();

            try
            {
                return JsonSerializer.Deserialize<Cart>(json) ?? new Cart();
            }
            catch
            {
                return new Cart();
            }
        }

        public void SaveCart(ISession session, string tenantCode, Cart cart)
        {
            var key = SessionKeyPrefix + tenantCode.ToUpperInvariant();
            session.SetString(key, JsonSerializer.Serialize(cart));
        }

        public void ClearCart(ISession session, string tenantCode)
        {
            var key = SessionKeyPrefix + tenantCode.ToUpperInvariant();
            session.Remove(key);
        }
    }

    public class Cart
    {
        public List<CartLine> Lines { get; set; } = new();

        public int TotalQuantity => Lines.Sum(x => x.Quantity);
    }

    public class CartLine
    {
        public Guid ProductVariantId { get; set; }
        public int Quantity { get; set; }
    }
}
