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

        private readonly string _defaultCulture;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        public SettingsService(ILogger logger, IUserService userService, ILocalizationService localizationService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _defaultCulture = localizationService.GetDefaultLanguageIsoCode() ?? throw new ArgumentNullException(nameof(localizationService));
        }

        /// <summary>
        /// Load the Preflight settings from the JSON file in app_plugins
        /// </summary>
        public PreflightSettings Get(string culture)
        {
            culture = culture == "default" ? _defaultCulture : culture;

            if (HttpContext.Current.Request.IsLocal)
                return GetSettings(culture);

            PreflightSettings fromCache = Current.AppCaches.RuntimeCache.GetCacheItem(KnownStrings.SettingsCacheKey + culture, () => GetSettings(culture), new TimeSpan(24, 0, 0), false);

            if (fromCache != null)
            {
                return fromCache;
            }

            _logger.Error<SettingsService>(new NullReferenceException("Could not get Preflight settings"));

            return null;
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
                _logger.Error<SettingsService>(ex, "Could not save Preflight settings");
                return false;
            }
        }

        private PreflightSettings GetSettings(string culture)
        {
            // only get here when nothing is cached 
            List<SettingsModel> settings;

            // json initially stores the core checks only
            // once it has been saved in the backoffice, settings store all current plugins, with alias
            using (var file = new StreamReader(KnownStrings.SettingsFilePath.Replace("{culture}", culture)))
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
            return new PreflightSettings
            {
                Culture = culture,
                Settings = settings.DistinctBy(s => (s.Tab, s.Label)).ToList(),
                Tabs = tabs.GroupBy(x => x.Name)
                    .Select(y => y.First())
                    .OrderBy(i => i.Name != SettingsTabNames.General)
                    .ThenBy(i => i.Name).ToList()
            };
        }
    }
}
