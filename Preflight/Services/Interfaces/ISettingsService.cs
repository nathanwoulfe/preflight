using Preflight.Models;

namespace Preflight.Services.Interfaces
{
    public interface ISettingsService
    {
        PreflightSettings Get(string culture);

        bool Save(PreflightSettings settings);
    }
}
