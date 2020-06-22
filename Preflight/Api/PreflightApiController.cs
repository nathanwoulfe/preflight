using Preflight.Models;
using Preflight.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Http;
using Umbraco.Web.Mvc;
using Umbraco.Web.WebApi;

namespace Preflight.Api
{
    [RoutePrefix("umbraco/backoffice/preflight/api")]
    [PluginController("preflight")]
    public class ApiController : UmbracoAuthorizedApiController
    {
        private readonly ISettingsService _settingsService;
        private readonly IContentChecker _contentChecker;

        public ApiController(ISettingsService settingsService, IContentChecker contentChecker)
        {
            _settingsService = settingsService;
            _contentChecker = contentChecker;
        }

        /// <summary>
        /// Get Preflight settings object
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("getSettings")]
        public IHttpActionResult GetSettings()
        {
            var id = "en-US";

            try
            {
                return Ok(new
                {
                    status = HttpStatusCode.OK,
                    data = _settingsService.Get(id)
                });
            }
            catch (Exception ex)
            {
                return Error(ex.Message);
            }
        }

        /// <summary>
        /// Get Preflight settings object value
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("getSettingValue/{id}/{culture}")]
        public IHttpActionResult GetSettingValue(string id, string culture = "")
        {
            try
            {
                List<SettingsModel> settings = _settingsService.Get(culture).Settings;
                SettingsModel model = settings.First(s => s.Alias == id);

                return Ok(new
                {
                    status = HttpStatusCode.OK,
                    value = model?.Value
                });
            }
            catch (Exception ex)
            {
                return Error(ex.Message);
            }
        }

        /// <summary>
        /// Sabve Preflight settings object
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("saveSettings")]
        public IHttpActionResult SaveSettings(PreflightSettings settings)
        {
            try
            {
                return Ok(new
                {
                    status = HttpStatusCode.OK,
                    data = _settingsService.Save(settings)
                });
            }
            catch (Exception ex)
            {
                return Error(ex.Message);
            }
        }

        /// <summary>
        /// Entry point for all content checking
        /// </summary>
        /// <param name="id">Node id</param>
        /// <returns></returns>
        [HttpGet]
        [Route("check/{id}/{culture}")]
        public IHttpActionResult Check(int id, string culture = "")
        {
            try
            {
                return Ok(new
                {
                    status = HttpStatusCode.OK,
                    failed = _contentChecker.CheckContent(id, culture)
                });
            }
            catch (Exception ex)
            {
                return Error(ex.Message);
            }
        }

        /// <summary>
        /// Entry point for checking sub-set of properties
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("checkdirty")]
        public IHttpActionResult CheckDirty(DirtyProperties data)
        {
            try
            {
                return Ok(new
                {
                    status = HttpStatusCode.OK,
                    failed = _contentChecker.CheckDirty(data)
                });
            }
            catch (Exception ex)
            {
                return Error(ex.Message);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private IHttpActionResult Error(string message)
        {
            return Ok(new
            {
                status = HttpStatusCode.InternalServerError,
                data = message
            });
        }
    }
}
