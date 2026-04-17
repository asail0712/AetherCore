using AetherCore.Repository;
using AetherCore.Utility.Attributes;
using AetherCore.Utility.Caches;
using Common.DTO.TEMPLATE_NAME;
using DataAccess.Interface;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Repository.Interface;

namespace Repository
{
    [AutoInject(ServiceLifetime.Scoped)]
    public class TEMPLATE_NAMERepository : GenericRepository<TEMPLATE_NAMEEntity, ITEMPLATE_NAMEDataAccess>, ITEMPLATE_NAMERepository
    {
        public TEMPLATE_NAMERepository(ITEMPLATE_NAMEDataAccess dataAccess, IMemoryCache memoryCache, IOptions<CacheSettings> cacheSettings) 
            : base(dataAccess, memoryCache, cacheSettings)
        {

        }
    }
}
