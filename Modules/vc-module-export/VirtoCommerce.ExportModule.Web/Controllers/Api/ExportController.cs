using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;
using VirtoCommerce.ExportModule.Core.Model;
using VirtoCommerce.ExportModule.Core.Services;
using VirtoCommerce.ExportModule.Data.Model;
using VirtoCommerce.ExportModule.Web.BackgroundJobs;
using VirtoCommerce.ExportModule.Web.Model;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Core.ExportImport.PushNotifications;
using VirtoCommerce.Platform.Core.PushNotifications;
using VirtoCommerce.Platform.Core.Security;

namespace VirtoCommerce.ExportModule.Web.Controllers
{
    [Route("api/export")]
    public class ExportController : Controller
    {
        private readonly IEnumerable<Func<IExportProviderConfiguration, Stream, IExportProvider>> _exportProviderFactories;
        private readonly IKnownExportTypesRegistrar _knownExportTypesRegistrar;
        private readonly IUserNameResolver _userNameResolver;
        private readonly IPushNotificationManager _pushNotificationManager;
        private readonly PlatformOptions _platformOptions;

        public ExportController(
            IEnumerable<Func<IExportProviderConfiguration, Stream, IExportProvider>> exportProviderFactories,
            IKnownExportTypesRegistrar knownExportTypesRegistrar,
            IUserNameResolver userNameResolver,
            IPushNotificationManager pushNotificationManager,
            IOptions<PlatformOptions> platformOptions)
        {
            _exportProviderFactories = exportProviderFactories;
            _knownExportTypesRegistrar = knownExportTypesRegistrar;
            _userNameResolver = userNameResolver;
            _pushNotificationManager = pushNotificationManager;
            _platformOptions = platformOptions.Value;
        }

        /// <summary>
        /// Gets the list of types ready to be exported
        /// </summary>
        /// <returns>The list of exported known types</returns>
        [HttpGet]
        [Route("knowntypes")]
        public ActionResult<ExportedTypeDefinition[]> GetExportedKnownTypes()
        {
            return Ok(_knownExportTypesRegistrar.GetRegisteredTypes());
        }

        /// <summary>
        /// Gets the list of available export providers
        /// </summary>
        /// <returns>The list of export providers</returns>
        [HttpGet]
        [Route("providers")]
        public ActionResult<IExportProvider[]> GetExportProviders()
        {
            return Ok(_exportProviderFactories.Select(x =>
            {
                using (var ms = new MemoryStream())
                {
                    return x(new EmptyProviderConfiguration(), ms);
                }
            }).ToArray());
        }

        /// <summary>
        /// Starts export task
        /// </summary>
        /// <param name="request">Export task description</param>
        /// <returns>Export task id</returns>
        [HttpPost]
        [Route("run")]
        public ActionResult<PlatformExportPushNotification> RunExport([FromBody]ExportDataRequest request)
        {
            var notification = new PlatformExportPushNotification(_userNameResolver.GetCurrentUserName())
            {
                Title = $"{request.ExportTypeName} export task",
                Description = "starting export...."
            };
            _pushNotificationManager.Send(notification);

            var jobId = BackgroundJob.Enqueue<ExportJob>(x => x.ExportBackgroundAsync(request, notification, JobCancellationToken.Null, null));
            notification.JobId = jobId;

            return Ok(notification);
        }

        /// <summary>
        /// Attempts to cancel export task
        /// </summary>
        /// <param name="cancellationRequest">Cancellation request with task id</param>
        /// <returns></returns>
        [HttpPost]
        [Route("task/cancel")]
        public ActionResult CancelExport([FromBody]ExportCancellationRequest cancellationRequest)
        {
            BackgroundJob.Delete(cancellationRequest.JobId);
            return Ok();
        }

        /// <summary>
        /// Downloads file by its name
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("download/{fileName}")]
        public ActionResult DownloadExportFile([FromRoute] string fileName)
        {
            var localTmpFolder = Path.GetFullPath(Path.Combine(_platformOptions.DefaultExportFolder));
            var localPath = Path.Combine(localTmpFolder, Path.GetFileName(_platformOptions.DefaultExportFileName));

            //Load source data only from local file system 
            using (var stream = System.IO.File.Open(localPath, FileMode.Open))
            {
                var provider = new FileExtensionContentTypeProvider();
                if (!provider.TryGetContentType(localPath, out var contentType))
                {
                    contentType = "application/octet-stream";
                }
                return PhysicalFile(localPath, contentType);
            }
        }
    }
}
