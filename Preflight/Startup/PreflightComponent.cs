using ClientDependency.Core;
using Preflight.Constants;
using Preflight.Extensions;
using Preflight.Models;
using Preflight.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Http.Filters;
using System.Web.Routing;
using Umbraco.Core.Composing;
using Umbraco.Core.Events;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using Umbraco.Core.Services.Implement;
using Umbraco.Web;
using Umbraco.Web.Editors;
using Umbraco.Web.JavaScript;
using Umbraco.Web.Models.ContentEditing;

namespace Preflight.Startup
{
    public class PreflightComponent : IComponent
    {
        private readonly ISettingsService _settingsService;
        private readonly IContentChecker _contentChecker;
        private readonly string _defaultCulture;

        public PreflightComponent(ILocalizationService localizationService, ISettingsService settingsService, IContentChecker contentChecker)
        {
            _settingsService = settingsService;
            _contentChecker = contentChecker;

            _defaultCulture = localizationService.GetDefaultLanguageIsoCode();
        }

        public void Initialize()
        {
            GlobalConfiguration.Configuration.MessageHandlers.Add(new NotificationsHandler());
            GlobalConfiguration.Configuration.MapHttpAttributeRoutes();

            ServerVariablesParser.Parsing += ServerVariablesParser_Parsing;
            ContentService.Saving += ContentService_Saving;
            EditorModelEventManager.SendingContentModel += EditorModelEventManager_SendingContentModel;
        }

        public void Terminate()
        {

        }

        private void EditorModelEventManager_SendingContentModel(HttpActionExecutedContext sender, EditorModelEventArgs<ContentItemDisplay> e)
        {
            var removeContentApp = new List<bool>();
            int idx = 0;

            foreach (var variant in e.Model.Variants)
            {
                removeContentApp.Add(false);

                // get variant or default settings - will be default if only one culture is configured
                List<SettingsModel> settings = _settingsService.Get(variant.Language?.IsoCode ?? _defaultCulture).Settings;

                if (settings == null || !settings.Any())
                    continue;
                
                var groupSetting = settings.FirstOrDefault(x => string.Equals(x.Label, KnownSettings.UserGroupOptIn, StringComparison.InvariantCultureIgnoreCase));
                var testablePropsSetting = settings.FirstOrDefault(x => string.Equals(x.Label, KnownSettings.PropertiesToTest, StringComparison.InvariantCultureIgnoreCase));

                if (groupSetting != null && groupSetting.Value.HasValue())
                {
                    var currentUserGroups = e.UmbracoContext.Security.CurrentUser?.Groups?.Select(x => x.Name) ?? new List<string>();
                    if (currentUserGroups.Any())
                    {
                        bool include = groupSetting.Value.Split(',').Intersect(currentUserGroups).Any();

                        if (!include)
                            e.Model.ContentApps = e.Model.ContentApps.Where(x => x.Name != KnownStrings.Name);
                    }
                }

                // remove preflight app if content type doesn't include testable properties
                if (testablePropsSetting != null)
                {
                    var defaultVariant = e.Model.Variants.FirstOrDefault();
                    var properties = defaultVariant.Tabs.SelectMany(x => x.Properties.Select(y => y.Editor)).Distinct();

                    var isTestable = properties.Intersect(testablePropsSetting.Value.Split(',')).Any();

                    if (!isTestable)
                    {
                        removeContentApp[idx] = true;
                    }
                }

                idx += 1;
            }

            if (removeContentApp.All(x => x == true))
            {
                e.Model.ContentApps = e.Model.ContentApps.Where(x => x.Name != KnownStrings.Name);
            }
        }

        /// <summary>
        /// Add preflight-specific values to the servervariables dictionary
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="dictionary"></param>
        private static void ServerVariablesParser_Parsing(object sender, Dictionary<string, object> dictionary)
        {
            var urlHelper = new System.Web.Mvc.UrlHelper(new RequestContext(new HttpContextWrapper(HttpContext.Current), new RouteData()));
            IDictionary<string, object> settings = dictionary["umbracoSettings"].ToDictionary();

            dictionary.Add("Preflight", new Dictionary<string, object>
            {
                { "ContentFailedChecks", KnownStrings.ContentFailedChecks },
                { "PluginPath", $"{settings["appPluginsPath"]}/preflight/backoffice" },
                { "PropertyTypesToCheck", KnownPropertyAlias.All },
                { "ApiPath", urlHelper.GetUmbracoApiServiceBaseUrl<Api.ApiController>(controller => controller.GetSettings(1, "en-US", false)) }
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContentService_Saving(IContentService sender, SaveEventArgs<IContent> e)
        {
            // TODO => this should be culture-aware
            List<SettingsModel> settings = _settingsService.Get(null).Settings;

            // only check if current user group is opted in to testing on save
            var groupSetting = settings.FirstOrDefault(x => string.Equals(x.Label, KnownSettings.UserGroupOptIn, StringComparison.InvariantCultureIgnoreCase));
            if (groupSetting != null && groupSetting.Value.HasValue())
            {
                var currentUserGroups = Umbraco.Web.Composing.Current.UmbracoContext.Security.CurrentUser?.Groups?.Select(x => x.Name) ?? new List<string>();
                if (currentUserGroups.Any())
                {
                    bool testOnSave = groupSetting.Value.Split(',').Intersect(currentUserGroups).Any();

                    if (!testOnSave)
                        return;
                }
            }

            if (!settings.GetValue<bool>(KnownSettings.BindSaveHandler)) return;

            var cancelSaveOnFail = settings.GetValue<bool>(KnownSettings.CancelSaveOnFail);

            IContent content = e.SavedEntities.First();

            // TODO => this should be culture aware
            // message should always be empty, since there must be settings if we're running tests
            string message = _contentChecker.CheckContent(content, null, true, out bool failed);

            // at least one property on the current document fails the preflight check
            if (!failed) return;

            // these values are retrieved in the notifications handler, and passed down to the client
            HttpContext.Current.Items["PreflightFailed"] = true;
            HttpContext.Current.Items["PreflightCancelSaveOnFail"] = cancelSaveOnFail;
            HttpContext.Current.Items["PreflightNodeId"] = content.Id;

            if (e.CanCancel && cancelSaveOnFail)
            {
                e.CancelOperation(new EventMessage("PreflightFailed", content.Id.ToString()));
            }
        }
    }
}
