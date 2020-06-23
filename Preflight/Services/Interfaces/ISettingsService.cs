using Preflight.Models;

namespace Preflight.Services.Interfaces
{
    public interface ISettingsService
    {
        PreflightSettings Get(string culture, bool fallbackToDefault = false);

        bool Save(PreflightSettings settings);
    }
}
