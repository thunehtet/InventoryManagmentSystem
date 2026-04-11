using System;

namespace ClothInventoryApp.Services.Tenant
{
    public interface ITenantProvider
    {
        Guid GetTenantId();
    }
}