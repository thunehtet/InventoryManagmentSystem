using Microsoft.AspNetCore.Http;

namespace ClothInventoryApp.Services.Storefront
{
    public interface ICartService
    {
        Cart GetCart(ISession session, string tenantCode);
        void SaveCart(ISession session, string tenantCode, Cart cart);
        void ClearCart(ISession session, string tenantCode);
    }
}
