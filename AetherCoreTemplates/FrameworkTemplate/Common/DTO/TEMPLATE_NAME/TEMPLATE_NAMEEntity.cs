using AetherCore.Entities;

namespace Common.DTO.TEMPLATE_NAME
{
    public class TEMPLATE_NAMEEntity : IDBEntity
    {        
        public string Id                { get; set; } = string.Empty;
        public DateTime CreatedAt       { get; set; }             // 建立時間
        public DateTime UpdatedAt       { get; set; }             // 更新時間

        public TEMPLATE_NAMEEntity() 
        {
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
