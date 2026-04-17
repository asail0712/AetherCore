using AetherCore.DataAccess;
using AetherCore.Utility.Attributes;
using AutoMapper;
using Common.DTO.TEMPLATE_NAME;
using DataAccess.Interface;
using Microsoft.Extensions.DependencyInjection;

namespace DataAccess
{
    [AutoInject(ServiceLifetime.Scoped)]
    public class TEMPLATE_NAMEDataAccess : MongoEntityDataAccess<TEMPLATE_NAMEEntity, TEMPLATE_NAMEDocument>, ITEMPLATE_NAMEDataAccess
    {
        public TEMPLATE_NAMEDataAccess(IMapper mapper)
            : base(mapper)
        {
        }
    }
}
