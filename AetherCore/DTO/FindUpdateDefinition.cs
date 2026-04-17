using MongoDB.Driver;

namespace AetherCore.DTO
{
    public class FindUpdateDefinition<TDocument>
    {
        public Func<FilterDefinitionBuilder<TDocument>, FilterDefinition<TDocument>> Filter { get; set; }
        public Func<UpdateDefinitionBuilder<TDocument>, UpdateDefinition<TDocument>> Update { get; set; }
        public FindUpdateDefinition<TDocument> Where(Func<FilterDefinitionBuilder<TDocument>, FilterDefinition<TDocument>> filter)
        {
            Filter = filter;
            return this;
        }
        public FindUpdateDefinition<TDocument> UpdateDef(
            Func<UpdateDefinitionBuilder<TDocument>, UpdateDefinition<TDocument>> update)
        {
            Update = update;
            return this;
        }
        public bool ReturnAfter { get; set; } = true;
    }

}
