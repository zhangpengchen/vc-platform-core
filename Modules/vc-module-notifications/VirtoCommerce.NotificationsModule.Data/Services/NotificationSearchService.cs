using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.NotificationsModule.Core.Model;
using VirtoCommerce.NotificationsModule.Core.Services;
using VirtoCommerce.NotificationsModule.Data.Model;
using VirtoCommerce.NotificationsModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.NotificationsModule.Data.Services
{
    public class NotificationSearchService : INotificationSearchService
    {
        private readonly Func<INotificationRepository> _repositoryFactory;
        public NotificationSearchService(Func<INotificationRepository> repositoryFactory)
        {
            _repositoryFactory = repositoryFactory;
        }

        

        public  GenericSearchResult<Notification> SearchNotifications(NotificationSearchCriteria criteria)
        {
            var query = AbstractTypeFactory<Notification>.AllTypeInfos
                .Where(t => t.AllSubclasses.Any(s => s != t.Type && s.IsSubclassOf(typeof(Notification))))
                .Select(n => n.Type)
                .AsQueryable();
            
            if (!string.IsNullOrEmpty(criteria.Keyword))
            {
                query = query.Where(n => n.Name.Contains(criteria.Keyword));
            }

            var totalCount = query.Count();

            var sortInfos = criteria.SortInfos;
            if (sortInfos.IsNullOrEmpty())
            {
                sortInfos = new[] { new SortInfo { SortColumn = ReflectionUtility.GetPropertyName<Notification>(x => x.Type), SortDirection = SortDirection.Ascending } };
            }

            var collection = query.OrderBySortInfos(sortInfos).Skip(criteria.Skip).Take(criteria.Take).ToList();

            var list = collection.Select(t =>
            {
                var result = AbstractTypeFactory<Notification>.TryCreateInstance(t.Name);
                NotificationEntity notificationEntity;
                using (var repository = _repositoryFactory())
                {
                    notificationEntity = repository.GetEntityForListByType(t.Name, criteria.TenantId, criteria.TenantType);
                }
                return notificationEntity != null ? notificationEntity.ToModel(result) : result;
            }).ToList();

            return new GenericSearchResult<Notification>
            {
                Results = list,
                TotalCount = totalCount
            };
        }
    }
}
