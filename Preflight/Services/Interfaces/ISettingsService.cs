using Preflight.Models;
using System.Collections.Generic;

namespace Preflight.Services.Interfaces
{
    public interface ISettingsService
    {
        PreflightSettings Get(string culture, bool fallbackToDefault = false);

        bool Save(PreflightSettings settings);

        List<string> GetPropertiesForCurrent(string culture, string alias);
    }
}
