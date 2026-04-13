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

            // SuperAdmin overrides all tenant roles
            if (user.IsSuperAdmin)
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, "SuperAdmin"));
            }
            else
            {
                // Role claim: IsTenantAdmin → "Admin", otherwise "Staff"
                var role = user.IsTenantAdmin ? "Admin" : "Staff";
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
            }

            return identity;
        }
    }
}