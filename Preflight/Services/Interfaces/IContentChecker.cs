using Preflight.Models;
using Umbraco.Core.Models;

namespace Preflight.Services.Interfaces
{
    public interface IContentChecker
    {
        string CheckContent(int id, string culture, bool fromSave, out bool failed);
        string CheckContent(IContent content, string culture, bool fromSave, out bool failed);

        /// <summary>
        /// Checks set of property values and returns responses via signalr broadcast
        /// </summary>
        /// <param name="dirtyProperties"></param>
        bool CheckDirty(DirtyProperties dirtyProperties);
    }
}
