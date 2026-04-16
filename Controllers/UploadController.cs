using ClothInventoryApp.Models;
using ClothInventoryApp.Services.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ClothInventoryApp.Controllers
{
    [Authorize]
    public class UploadController : Controller
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly UserManager<ApplicationUser> _userManager;

        public UploadController(IFileStorageService fileStorageService, UserManager<ApplicationUser> userManager)
        {
            _fileStorageService = fileStorageService;
            _userManager = userManager;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Image(IFormFile file, string category, CancellationToken cancellationToken)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            try
            {
                var uploadedFile = await _fileStorageService.SaveImageAsync(
                    file,
                    category,
                    user.Id,
                    user.IsSuperAdmin ? null : user.TenantId,
                    cancellationToken);

                return Json(new
                {
                    uploadedFile.Id,
                    Url = _fileStorageService.GetPublicUrl(uploadedFile),
                    uploadedFile.OriginalFileName,
                    uploadedFile.SizeBytes,
                    uploadedFile.Category
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
