using AutoMapper;

namespace Common.DTO.TEMPLATE_NAME
{
    public class TEMPLATE_NAMEProfile : Profile
    {
        public TEMPLATE_NAMEProfile()
        {
            CreateMap<TEMPLATE_NAMERequest, TEMPLATE_NAMEEntity>();
            CreateMap<TEMPLATE_NAMEEntity, TEMPLATE_NAMEResponse>();
            CreateMap<TEMPLATE_NAMEEntity, TEMPLATE_NAMEDocument>();
            CreateMap<TEMPLATE_NAMEDocument, TEMPLATE_NAMEEntity>();
        }
    }

}
