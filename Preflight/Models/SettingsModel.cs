using Newtonsoft.Json;
using Preflight.Constants;
using Preflight.Extensions;
using System.Collections.Generic;
using System.Linq;

namespace Preflight.Models
{
    public class PreflightSettings
    {
        [JsonProperty("settings")]
        public List<SettingsModel> Settings { get; set; }

        [JsonProperty("tabs")]
        public List<SettingsTab> Tabs { get; set; }

        [JsonProperty("culture")]
        public string Culture { get; set; }
    }

    public class SettingsTab
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("summary")]
        public string Summary { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }
    }

    /// <summary>
    /// The model describing a generic setting for a test. A test can have none, one or many
    /// </summary>
    public class SettingsModel
    {
        [JsonProperty("core")]
        internal bool Core { get; set; }

        /// <summary>
        /// A UI-friendly label for the setting
        /// </summary>
        [JsonProperty("label")]
        public string Label { get; set; }

        /// <summary>
        /// Set the default value for the setting
        /// </summary>
        [JsonProperty("value")]
        public string Value { get; set; }

        /// <summary>
        /// Describe the setting
        /// </summary>
        [JsonProperty("description")]
        public string Description { get; set; }

        /// <summary>
        /// Path to an Umbraco property editor view eg "views/propertyeditors/boolean/boolean.html"
        /// </summary>
        [JsonProperty("view")]
        public string View { get; set; }

        /// <summary>
        /// Where should the setting sit on the tab
        /// </summary>
        [JsonProperty("order")]
        public int Order { get; set; }

        /// <summary>
        /// Where should the setting be displayed - either reference an existing tab from SettingsTabNames, or add your own
        /// Plugins default to the plugin name
        /// </summary>
        [JsonProperty("tab")]
        internal string Tab { get; set; }

        /// <summary>
        /// The generated property alias
        /// </summary>
        [JsonProperty("alias")]
        internal string Alias { get; set; }

        /// <summary>
        /// Prevalues for the setting
        /// </summary>
        [JsonProperty("prevalues")]
        public object Prevalues { get; set; }
    }


    /// <summary>
    /// Defines a generic Preflight setting
    /// </summary>
    public class GenericSettingModel : SettingsModel
    {
        public GenericSettingModel(string label)
        {
            Label = label;
            Alias = label.Camel();
        }
    }

    /// <summary>
    /// Defines the Preflight setting for the disable/enable setting 
    /// </summary>
    internal class DisabledSettingModel : SettingsModel
    {
        public DisabledSettingModel(string tab, bool val)
        {
            Value = val ? "1" : "0";
            Label = "Disabled";
            Alias = tab.DisabledAlias();
            Description = $"Disable the {tab} plugin";
            View = SettingType.Boolean;
            Order = -10;
            Core = true;
            Tab = tab;
        }
    }

    /// <summary>
    /// Defines the Preflight setting for the on-save-only setting 
    /// </summary>
    internal class OnSaveOnlySettingModel : SettingsModel
    {
        public OnSaveOnlySettingModel(string tab, bool val)
        {
            Value = val ? "1" : "0";
            Label = "Run on save only";
            Alias = tab.OnSaveOnlyAlias();
            Description = "Restrict this plugin to run only in a save event";
            View = SettingType.Boolean;
            Order = -5;
            Core = true;
            Tab = tab;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    internal class PropertiesToTestSettingModel : SettingsModel
    {
        public PropertiesToTestSettingModel(string tab, string propsToTest)
        {
            Value = propsToTest;
            Label = "Properties to test";
            Alias = tab.PropertiesToTestAlias();
            Description = "Restrict this plugin to run against a subset of testable properties";
            View = SettingType.CheckboxList;
            Prevalues = KnownPropertyAlias.All.Select(x => new { value = x, key = x });
            Order = -15;
            Core = true;
            Tab = tab;
        }
    }

    /// <summary>
    /// Quickly generate settings list with defaults included
    /// Set the default values for the plugin's disabled and run-on-save states
    /// </summary>
    public static class PluginSettingsList
    {
        public static IEnumerable<SettingsModel> Populate(string name, bool disabled, bool runOnSaveOnly, string propsToTest = "", params SettingsModel[] settings)
        {
            if (!settings.Any())
                return new List<SettingsModel>();

            if (!propsToTest.HasValue())
            {
                propsToTest = string.Join(",", KnownPropertyAlias.All);
            }

            List<SettingsModel> response = new List<SettingsModel>
            {
                new DisabledSettingModel(name, disabled),
                new OnSaveOnlySettingModel(name, runOnSaveOnly),
                new PropertiesToTestSettingModel(name, propsToTest)
            };

            foreach (SettingsModel s in settings)
            {
                s.Tab = name;
                response.Add(s);
            }

            return response;
        }
    }
}