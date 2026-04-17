using AetherCore.Service;
using AetherCore.Utility.Attributes;
using AutoMapper;
using Common.DTO.TEMPLATE_NAME;
using Microsoft.Extensions.DependencyInjection;
using Repository.Interface;
using Service.Interface;

namespace Service
{
    [AutoInject(ServiceLifetime.Scoped)]
    public class TEMPLATE_NAMEService : GenericService<TEMPLATE_NAMEEntity, TEMPLATE_NAMERequest, TEMPLATE_NAMEResponse, ITEMPLATE_NAMERepository>, ITEMPLATE_NAMEService
    {
        public TEMPLATE_NAMEService(ITEMPLATE_NAMERepository repo, IMapper mapper) 
            : base(repo, mapper)
        {
        }
    }
}
