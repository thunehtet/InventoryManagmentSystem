using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace ClothInventoryApp.Services.Tenant
{
    public class TenantProvider : ITenantProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TenantProvider(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Guid GetTenantId()
        {
            var tenantId = _httpContextAccessor.HttpContext?
                .User?
                .FindFirstValue("TenantId");

            if (string.IsNullOrWhiteSpace(tenantId))
                throw new UnauthorizedAccessException("TenantId not found in claims.");

            return Guid.Parse(tenantId);
        }
    }
}