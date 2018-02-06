using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.NotificationsModule.Data.Abstractions;
using VirtoCommerce.NotificationsModule.Data.Model;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.NotificationsModule.Web.Controllers
{


    [Authorize]
    [Route("api/notifications")]
    public class NotificationsController : Controller
    {
        private readonly INotificationService _notificationService;
        private readonly INotificationTemplateService _notificationTemplateService;

        public NotificationsController(INotificationService notificationService, INotificationTemplateService notificationTemplateService)
        {
            _notificationService = notificationService;
            _notificationTemplateService = notificationTemplateService;
        }

        [HttpGet]
        [Produces("application/json")]
        [ProducesResponseType(typeof(GenericSearchResult<NotificationResult>), 200)]
        public IActionResult GetNotifications(NotificationSearchCriteria searchCriteria)
        {
            var notifications = _notificationService.SearchNotifications(searchCriteria);
            return Ok(notifications);
        }

        [HttpGet]
        [Route("{type}")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(NotificationResult), 200)]
        public IActionResult GetNotificationByType(string type)
        {
            var notification = _notificationService.GetNotificationByTypeId(type);
            return Ok(notification);
        }

        [HttpPost]
        [Route("{type}")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(void), 200)]
        public IActionResult UpdateNotification([FromBody] Notification notification)
        {
            _notificationService.UpdateNotification(notification);

            return StatusCode((int)HttpStatusCode.NoContent);
        }

        [HttpGet]
        [Route("{type}/templates")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(NotificationTemplateResult[]), 200)]
        public IActionResult GetTemplatesByNotificationType(string type, string objectId, string objectTypeId)
        {
            var templates = _notificationTemplateService.GetNotificationTemplatesByNotification(type, objectId, objectTypeId);
            return Ok(templates);
        }

        [HttpGet]
        [Route("{type}/templates/{id}")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(NotificationTemplateResult), 200)]
        public IActionResult GetTemplateById(string type, string id)
        {
            var template = _notificationTemplateService.GetById(type, id);
            return Ok(template);
        }
    }
}
