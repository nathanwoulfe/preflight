using Microsoft.AspNet.SignalR;
using Newtonsoft.Json.Linq;
using Preflight.Constants;
using Preflight.Extensions;
using Preflight.Models;
using Preflight.Plugins;
using Preflight.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Configuration.Grid;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using Umbraco.Web.Composing;

namespace Preflight.Services
{
    /// <summary>
    /// Where the magic happens. ContentChecker extracts property values and passes them into the set of plugins for testing
    /// </summary>
    internal class ContentChecker : IContentChecker
    {
        private readonly IContentService _contentService;
        private readonly IContentTypeService _contentTypeService;
        private readonly ISettingsService _settingsService;
        private readonly ILogger _logger;
        private readonly IHubContext _hubContext;

        private readonly string _defaultCulture;

        private int _id;
        private bool _fromSave;
        private List<SettingsModel> _settings;
        private string _testableProperties;

        private IEnumerable<IGridEditorConfig> _gridEditorConfig;

        public ContentChecker(ISettingsService settingsService, IContentService contentService, ILocalizationService localizationService,
            IContentTypeService contentTypeService, ILogger logger)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _contentService = contentService ?? throw new ArgumentNullException(nameof(contentService));
            _contentTypeService = contentTypeService ?? throw new ArgumentNullException(nameof(contentTypeService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _hubContext = GlobalHost.ConnectionManager.GetHubContext<PreflightHub>();
            _defaultCulture = localizationService.GetDefaultLanguageIsoCode();
        }

        /// <summary>
        /// Intialise variables for this testing run
        /// </summary>
        private string Initialise(string culture, out string realCulture)
        {
            realCulture = culture == "default" ? _defaultCulture : culture;
            var settings = _settingsService.Get(realCulture);

            if (settings.Settings != null && settings.Settings.Any())
            {
                _settings = settings.Settings;
                _testableProperties = _settings.GetValue(KnownSettings.PropertiesToTest);
            } else
            {
                return settings.Message;
            }

            if (_settings.GetValue<bool>(KnownSettings.DisableAllTests))
            {
                return "Preflight is currently disabled - enable it in Settings";
            }

            _gridEditorConfig = Current.Configs.Grids().EditorsConfig.Editors;

            return string.Empty;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dirtyProperties"></param>
        public bool CheckDirty(DirtyProperties dirtyProperties)
        {
            Initialise(dirtyProperties.Culture, out string culture);

            _id = dirtyProperties.Id;
            var failed = false;

            foreach (SimpleProperty prop in dirtyProperties.Properties)
            {
                string propValue = prop.Value?.ToString();

                // only continue if the prop has a value
                if (!propValue.HasValue())
                {
                    _hubContext.Clients.All.PreflightTest(new PreflightPropertyResponseModel
                    {
                        Name = prop.Name,
                        Remove = true
                    });
                    continue;
                }

                failed = TestAndBroadcast(prop.Name, propValue, prop.Editor, culture) || failed;
            }

            _hubContext.Clients.All.PreflightComplete();

            return failed;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="fromSave"></param>
        /// <returns></returns>
        public string CheckContent(int id, string culture, bool fromSave, out bool failed) {
            var message = CheckContent(_contentService.GetById(id), culture, fromSave, out bool innerFailed);
            failed = innerFailed;

            return message;
        }


        /// <summary>
        /// Checks all testable properties on the given IContent item
        /// </summary>
        /// <param name="content"></param>
        /// <param name="fromSave"></param>
        /// <returns></returns>
        public string CheckContent(IContent content, string culture, bool fromSave, out bool failed)
        {
            var message = Initialise(culture, out culture);

            _fromSave = fromSave;
            failed = false;

            if (message.HasValue())
            {
                failed = true;
                _hubContext.Clients.All.PreflightComplete();

                return message;
            }

            IEnumerable<Property> props = content.GetPreflightProperties();

            foreach (Property prop in props)
            {
                // don't use GetValue<T>() => extract the correct culture from the values collection
                var rawPropertyValue = prop.Values.First(x => x.Culture == culture || x.Culture == null);

                // check published first, then fall back to unpublished
                string propValue = rawPropertyValue.PublishedValue?.ToString();
                propValue = propValue ?? rawPropertyValue.EditedValue?.ToString();

                // only continue if the prop has a value
                if (!propValue.HasValue())
                {
                    _hubContext.Clients.All.PreflightTest(new PreflightPropertyResponseModel
                    {
                        Name = prop.PropertyType.Name,
                        Remove = true
                    });

                    continue;
                }

                failed = TestAndBroadcast(prop.PropertyType.Name, propValue, prop.PropertyType.PropertyEditorAlias, culture) || failed;
            }

            _hubContext.Clients.All.PreflightComplete();

            return string.Empty;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="alias"></param>
        /// <returns></returns>
        private bool TestAndBroadcast(string name, string value, string alias, string culture)
        {
            List<PreflightPropertyResponseModel> testResult = new List<PreflightPropertyResponseModel>();

            bool failed = false;

            switch (alias)
            {
                case KnownPropertyAlias.NestedContent:
                    testResult = ExtractValuesFromNestedContentProperty(name, value);
                    break;
                case KnownPropertyAlias.Grid:
                    testResult = ExtractValuesFromGridProperty(name, value);
                    break;
                case KnownPropertyAlias.Rte:
                case KnownPropertyAlias.Textarea:
                case KnownPropertyAlias.Textbox:
                    testResult = RunPluginsAgainstValue(name, value, alias).AsEnumerableOfOne().ToList();
                    break;
            }

            // return the results via signalr for perceived perf
            foreach (PreflightPropertyResponseModel result in testResult)
            {
                // ignore results where no plugins ran
                if (result.Plugins.Count > 0)
                {
                    if (result.Failed)
                    {
                        failed = true;
                    }

                    // check this client-side to ensure correct variant receives the result
                    result.Culture = culture;

                    // announce the result
                    _hubContext.Clients.All.PreflightTest(result);
                }
            }

            return failed;
        }


        /// <summary>
        /// Extracts the testable values from a Nested Content instance, and passes each to <see cref="ProcessJTokenValues" />
        /// </summary>
        /// <param name="propName"></param>
        /// <param name="propValue"></param>
        /// <returns></returns>
        private List<PreflightPropertyResponseModel> ExtractValuesFromNestedContentProperty(string propName, string propValue)
        {
            Dictionary<string, IContentType> cache = new Dictionary<string, IContentType>();
            List<PreflightPropertyResponseModel> response = new List<PreflightPropertyResponseModel>();

            JArray asJson = JArray.Parse(propValue);
            var index = 1;

            foreach (JObject o in asJson)
            {
                var typeAlias = o.Value<string>(KnownStrings.NcAlias);
                var type = cache.ContainsKey(typeAlias) ? cache[typeAlias] : _contentTypeService.Get(typeAlias);

                if (!cache.ContainsKey(typeAlias))
                {
                    cache.Add(typeAlias, type);
                }

                var propsFromType = type.GetPreflightProperties();

                foreach (var property in propsFromType)
                {
                    var value = o.Value<string>(property.Alias);
                    string label = $"{propName} (Item {index} - {property.Name})";

                    if (!value.HasValue())
                    {
                        _hubContext.Clients.All.PreflightTest(new PreflightPropertyResponseModel
                        {
                            Name = property.Name,
                            Label = label,
                            Remove = true
                        });
                    }
                    else
                    {
                        PreflightPropertyResponseModel model = RunPluginsAgainstValue(propName, value, property.PropertyEditorAlias, KnownPropertyAlias.NestedContent);

                        model.Label = label;
                        model.TotalTests = model.Plugins.Aggregate(0, (acc, x) => acc + x.TotalTests);

                        response.Add(model);
                    }
                }

                index += 1;
            }

            return response;
        }


        /// <summary>
        /// Extracts the testable values from a single Grid editor, and passes each to <see cref="ProcessJTokenValues" />
        /// </summary>
        /// <param name="propName"></param>
        /// <param name="propValue"></param>
        /// <returns></returns>
        private List<PreflightPropertyResponseModel> ExtractValuesFromGridProperty(string name, string propValue)
        {
            JObject asJson = JObject.Parse(propValue);
            List<PreflightPropertyResponseModel> response = new List<PreflightPropertyResponseModel>();

            IEnumerable<JToken> rows = asJson.SelectTokens("..rows");
            foreach (JToken row in rows)
            {
                string rowName = row[0].Value<string>("name");
                IEnumerable<JToken> editors = row.SelectTokens(KnownStrings.GridRteJsonPath);

                foreach (JToken editor in editors)
                {
                    // this is a bit messy - maps the grid editor view to the knownpropertyalias value
                    var editorViewAlias = "";
                    var editorAlias = editor.SelectToken("$..editor.alias")?.ToString();

                    var gridEditorConfig = _gridEditorConfig.FirstOrDefault(x => x.Alias == editorAlias);
                    var gridEditorName = gridEditorConfig.Name;
                    var gridEditorView = gridEditorConfig.View;

                    string label = $"{name} ({rowName} - {gridEditorName})";

                    JToken value = editor.SelectToken(KnownStrings.GridValueJsonPath);
                    if (value == null || !value.ToString().HasValue())
                    {
                        _hubContext.Clients.All.PreflightTest(new PreflightPropertyResponseModel
                        {
                            Name = gridEditorName,
                            Label = label,
                            Remove = true
                        }); ;
                    }
                    else
                    {
                        if (gridEditorView.HasValue())
                        {
                            switch (gridEditorView)
                            {
                                case "rte": editorViewAlias = KnownPropertyAlias.Rte; break;
                                case "textstring": editorViewAlias = KnownPropertyAlias.Textbox; break;
                                case "textarea": editorViewAlias = KnownPropertyAlias.Textarea; break;
                            }
                        }

                        PreflightPropertyResponseModel model = RunPluginsAgainstValue(name, value.ToString(), editorViewAlias, KnownPropertyAlias.Grid);

                        model.Label = label;
                        model.TotalTests = model.Plugins.Aggregate(0, (acc, x) => acc + x.TotalTests);

                        response.Add(model);
                    }
                }
            }

            return response;
        }


        /// <summary>
        /// Runs the set of plugins against the given string
        /// </summary>
        /// <param name="name"></param>
        /// <param name="val"></param>
        /// <returns></returns>
        private PreflightPropertyResponseModel RunPluginsAgainstValue(string name, string val, string alias, string parentAlias = "")
        {
            var model = new PreflightPropertyResponseModel
            {
                Label = name,
                Name = name
            };

            if (val == null || !_testableProperties.Contains(alias) || (parentAlias.HasValue() && !_testableProperties.Contains(parentAlias)))
                return model;

            var plugins = new PluginProvider().Get();

            foreach (IPreflightPlugin plugin in plugins)
            {
                // settings on the plugin are the defaults - set to correct values from _settings
                plugin.Settings = _settings.Where(s => s.Tab == plugin.Name)?.ToList();

                // ignore disabled plugins
                if (plugin.IsDisabled()) continue;
                if (!_fromSave && plugin.IsOnSaveOnly()) continue;

                string propsToTest = plugin.Settings.FirstOrDefault(x => x.Alias.Contains("PropertiesToTest"))?.Value ?? string.Join(",", KnownPropertyAlias.All);

                // only continue if the field alias is include for testing, or the parent alias has been set, and is included for testing
                if (!propsToTest.Contains(alias) || (parentAlias.HasValue() && !propsToTest.Contains(parentAlias))) continue;

                try
                {
                    Type pluginType = plugin.GetType();
                    if (pluginType.GetMethod("Check") == null) continue;

                    plugin.Check(_id, val, _settings);

                    if (plugin.Result != null)
                    {
                        if (plugin.FailedCount == 0)
                        {
                            plugin.FailedCount = plugin.Failed ? 1 : 0;
                        }

                        model.Plugins.Add(plugin);
                    }
                }
                catch (Exception e)
                {
                    _logger.Error<ContentChecker>(e, "Preflight couldn't take off...");
                }
            }

            // mark as failed if any sub-tests have failed
            model.FailedCount = model.Plugins.Sum(x => x.FailedCount);
            model.Failed = model.FailedCount > 0;

            model.Plugins = model.Plugins.OrderBy(p => p.SortOrder).ToList();
            model.TotalTests = model.Plugins.Aggregate(0, (acc, x) => acc + x.TotalTests);

            return model;
        }
    }
}
