using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.PricingModule.Core.Model;

namespace VirtoCommerce.Domain.Pricing.Events
{
    public class PriceChangingEvent : GenericChangedEntryEvent<Price>
    {
        public PriceChangingEvent(IEnumerable<GenericChangedEntry<Price>> changedEntries)
            : base(changedEntries)
        {
        }
    }
}
