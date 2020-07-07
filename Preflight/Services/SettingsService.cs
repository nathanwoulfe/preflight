using Lucene.Net.Documents;
using Lucene.Net.Search;
using Newtonsoft.Json;
using Preflight.Constants;
using Preflight.Extensions;
using Preflight.Models;
using Preflight.Plugins;
using Preflight.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Logging;
using Umbraco.Core.Services;
using Umbraco.Web.Composing;

namespace Preflight.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly ILogger _logger;
        private readonly IUserService _userService;
        private readonly ILocalizationService _localizationService;

        private readonly string _defaultCulture;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        public SettingsService(ILogger logger, IUserService userService, ILocalizationService localizationService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));

            _defaultCulture = localizationService.GetDefaultLanguageIsoCode();
        }

        /// <summary>
        /// Load the Preflight settings from the JSON file in app_plugins
        /// </summary>
        public PreflightSettings Get(string culture, bool fallbackToDefault)
        {
            culture = culture == "default" ? _defaultCulture : culture;

            // no caching for testing/dev
            //if (HttpContext.Current.Request.IsLocal)
            //    return GetSettings(culture, fallbackToDefault);

            PreflightSettings fromCache = Current.AppCaches.RuntimeCache.GetCacheItem(KnownStrings.SettingsCacheKey + culture, () => GetSettings(culture, fallbackToDefault), new TimeSpan(24, 0, 0), false);

            if (fromCache != null)
            {
                return fromCache;
            }

            _logger.Error<SettingsService>(new NullReferenceException($"Could not get Preflight settings for culture: {culture}"));

            return default;
        }

        /// <summary>
        /// Get the testable properties for the current culture, from the relevant settings file
        /// Avoids watching unnecessary editors on the client side
        /// </summary>
        /// <param name="culture"></param>
        /// <param name="alias"></param>
        /// <returns></returns>
        public List<string> GetPropertiesForCurrent(string culture, string alias)
        {
            var settings = GetSettings(culture, false).Settings;
            var documentTypesToTest = settings.GetValue(KnownSettings.DocumentTypesToTest);

            // if current alias is not listed in types to test, do nothing
            if (documentTypesToTest.HasValue() && !documentTypesToTest.Contains(alias) || settings.GetValue<bool>(KnownSettings.DisableAllTests))
                return new List<string>();

            return settings.GetValue(KnownSettings.PropertiesToTest)?.Split(',').ToList() ?? default;
        }

        /// <summary>
        /// Save the Preflight settings to the JSON file in app_plugins and update cache
        /// </summary>
        public bool Save(PreflightSettings settings)
        {
            settings.Culture = settings.Culture == "default" ? _defaultCulture : settings.Culture;

            try
            {
                Current.AppCaches.RuntimeCache.InsertCacheItem(KnownStrings.SettingsCacheKey + settings.Culture, () => settings, new TimeSpan(24, 0, 0), false);

                // only persist the settings, tabs can be regenerated on startup
                using (var file = new StreamWriter(KnownStrings.SettingsFilePath.Replace("{culture}", settings.Culture), false))
                {
                    var serializer = new JsonSerializer();
                    serializer.Serialize(file, settings.Settings);
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error<SettingsService>(ex, "$Could not save Preflight settings for culture: {culture}");
                return false;
            }
        }

        private PreflightSettings GetSettings(string culture, bool fallbackToDefault)
        {
            // only get here when nothing is cached 
            List<SettingsModel> settings;
            var resp = new PreflightSettings();
            string settingsPath = KnownStrings.SettingsFilePath.Replace("{culture}", culture);

            // if no settings file exists for the culture
            // check for a fallback otherwise return a UI message
            if (!File.Exists(settingsPath))
            {
                var lang = _localizationService.GetLanguageByIsoCode(culture);

                if (!lang.FallbackLanguageId.HasValue)
                {
                    resp.Culture = culture;
                    resp.Message = $"No settings exist for culture: {lang.CultureName}";

                    if (!fallbackToDefault)
                    {
                        return resp;
                    }
                }

                if (fallbackToDefault)
                {
                    culture = fallbackToDefault ? _defaultCulture : _localizationService.GetLanguageIsoCodeById(lang.FallbackLanguageId.Value);
                }              

                settingsPath = KnownStrings.SettingsFilePath.Replace("{culture}", culture);
            }

            // json initially stores the core checks only
            // once it has been saved in the backoffice, settings store all current plugins, with alias
            using (var file = new StreamReader(settingsPath))
            {
                string json = file.ReadToEnd();
                settings = JsonConvert.DeserializeObject<List<SettingsModel>>(json);
            }

            // populate prevalues for the groups setting
            // intersect ensures any removed groups aren't kept as settings values
            // since group name and alias can differ, needs to store both in the prevalue, and manage rebuilding this on the client side
            var allGroups = _userService.GetAllUserGroups();
            var groupSetting = settings.FirstOrDefault(x => string.Equals(x.Label, KnownSettings.UserGroupOptIn, StringComparison.InvariantCultureIgnoreCase));

            if (groupSetting != null)
            {
                var groupNames = allGroups.Select(x => new { value = x.Name, key = x.Alias });
                groupSetting.Prevalues = groupNames;

                if (groupSetting.Value.HasValue())
                {
                    var groupSettingValue = groupSetting.Value.Split(',').Intersect(groupNames.Select(x => x.value));
                    groupSetting.Value = string.Join(",", groupSettingValue);
                }
            }

            // populate prevlaues for subsetting testable properties, default value to all if nothing exists
            var testablePropertiesProp = settings.FirstOrDefault(x => string.Equals(x.Label, KnownSettings.PropertiesToTest, StringComparison.InvariantCultureIgnoreCase));

            if (testablePropertiesProp != null)
            {
                testablePropertiesProp.Prevalues = KnownPropertyAlias.All.Select(x => new { value = x, key = x });

                if (!testablePropertiesProp.Value.HasValue())
                {
                    testablePropertiesProp.Value = string.Join(",", KnownPropertyAlias.All);
                }
            }

            // add tabs for core items
            List<SettingsTab> tabs = new List<SettingsTab>();

            // get any plugins and add their settings
            // once settings have been saved from the backoffice, need to check that plugins aren't added twice
            var pluginProvider = new PluginProvider();
            var plugins = pluginProvider.Get();

            foreach (IPreflightPlugin plugin in plugins)
            {
                foreach (SettingsModel setting in plugin.Settings)
                {
                    if (!settings.Any(x => x.Alias == setting.Alias))
                    {
                        setting.Tab = plugin.Name;
                        settings.Add(setting);
                    }
                }

                // generate a tab from the plugin if not added already
                // send back the summary and description for the plugin as part of the tab object for display in the settings view
                tabs.Add(new SettingsTab
                {
                    Name = plugin.Name,
                    Description = plugin.Description,
                    Summary = plugin.Summary
                });
            }

            foreach (SettingsModel s in settings)
            {
                if (!s.Alias.HasValue())
                {
                    s.Alias = s.Label.Camel();
                }

                if (!tabs.Any(x => x.Name == s.Tab))
                {
                    tabs.Add(new SettingsTab
                    {
                        Name = s.Tab
                    });
                }
            }

            // tabs are sorted alpha, with general first
            resp.Settings = settings.DistinctBy(s => (s.Tab, s.Label)).ToList();
            resp.Tabs = tabs.GroupBy(x => x.Name)
                    .Select(y => y.First())
                    .OrderBy(i => i.Name != SettingsTabNames.General)
                    .ThenBy(i => i.Name).ToList();

            resp.Culture = resp.Culture ?? culture;

            return resp;
        }
    }
}
