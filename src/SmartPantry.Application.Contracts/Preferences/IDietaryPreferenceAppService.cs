using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace SmartPantry.Preferences;

public interface IDietaryPreferenceAppService : IApplicationService
{
    Task<DietaryPreferenceDto> GetMyPreferencesAsync();

    Task<DietaryPreferenceDto> UpdateMyPreferencesAsync(UpdateDietaryPreferenceDto input);

    Task<DietaryEvaluationDto> EvaluateProductAsync(EvaluateProductDietaryDto input);
}
