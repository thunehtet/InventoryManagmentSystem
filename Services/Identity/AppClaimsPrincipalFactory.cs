using System.Security.Claims;
using ClothInventoryApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ClothInventoryApp.Services.Identity
{
    public class AppClaimsPrincipalFactory
        : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
    {
        public AppClaimsPrincipalFactory(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IOptions<IdentityOptions> options)
            : base(userManager, roleManager, options)
        {
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
        {
            var identity = await base.GenerateClaimsAsync(user);

            
            identity.AddClaim(new Claim("TenantId", user.TenantId.ToString()));

            return identity;
        }
    }
}