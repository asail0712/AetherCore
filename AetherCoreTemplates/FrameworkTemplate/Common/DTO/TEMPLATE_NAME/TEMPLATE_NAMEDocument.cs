using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Entities;
using AetherCore.Entities;

namespace Common.DTO.TEMPLATE_NAME
{
    public class TEMPLATE_NAMEDocument : IEntity, IDBEntity
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id                { get; set; } = string.Empty;
        public DateTime CreatedAt       { get; set; }                                     // 建立時間
        public DateTime UpdatedAt       { get; set; }                                     // 更新時間

        // 實作 IEntity
        public object GenerateNewID()   => ObjectId.GenerateNewId().ToString()!;
        public bool HasDefaultID()      => string.IsNullOrEmpty(Id);
    }
}
