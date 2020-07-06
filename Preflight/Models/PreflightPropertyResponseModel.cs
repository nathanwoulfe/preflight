using Newtonsoft.Json;
using Preflight.Plugins;
using System.Collections.Generic;

namespace Preflight.Models
{
    public class PreflightPropertyResponseModel
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("label")]
        internal string Label { get; set; }

        [JsonProperty("plugins")]
        public List<IPreflightPlugin> Plugins { get; set; }

        [JsonProperty("open")]
        public bool Open { get; set; }

        [JsonProperty("failed")]
        public bool Failed { get; set; }

        [JsonProperty("failedCount")]
        public int FailedCount { get; set; }

        [JsonProperty("totalTests")]
        public int TotalTests { get; set; }

        [JsonProperty("remove")]
        public bool Remove { get; set; }

        [JsonProperty("culture")]
        public string Culture { get; set; }

        public PreflightPropertyResponseModel()
        {
            Open = false;
            Failed = false;            
            FailedCount = -1;
            Remove = false;

            Plugins = new List<IPreflightPlugin>();
        }
    }
}
