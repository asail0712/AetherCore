using System.Linq.Expressions;

namespace AetherCore.Utility
{
    public sealed record UpdateField<TEntity>(LambdaExpression FieldSelector, object? Value)
    {
        public static UpdateField<TEntity> Set<TField>(Expression<Func<TEntity, TField>> fieldSelector, TField value)
        {
            return new UpdateField<TEntity>(fieldSelector, value);
        }
    }
}
