using System;
using System.Threading.Tasks;

namespace ClothInventoryApp.Services.Feature
{
    public interface IFeatureService
    {
        Task<bool> HasFeatureAsync(Guid tenantId, string featureCode);
    }
}