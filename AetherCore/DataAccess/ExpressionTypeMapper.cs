using System.Linq.Expressions;
using System.Reflection;

namespace AetherCore.DataAccess
{
    internal static class ExpressionTypeMapper
    {
        public static Expression<Func<TDestination, bool>> MapPredicate<TSource, TDestination>(
            Expression<Func<TSource, bool>> predicate)
        {
            var targetParameter = Expression.Parameter(typeof(TDestination), predicate.Parameters[0].Name);
            var visitor = new MemberAccessVisitor(predicate.Parameters[0], targetParameter);
            var body = visitor.Visit(predicate.Body)
                ?? throw new InvalidOperationException("Predicate mapping failed.");

            return Expression.Lambda<Func<TDestination, bool>>(body, targetParameter);
        }

        private sealed class MemberAccessVisitor : ExpressionVisitor
        {
            private readonly ParameterExpression _sourceParameter;
            private readonly ParameterExpression _targetParameter;

            public MemberAccessVisitor(ParameterExpression sourceParameter, ParameterExpression targetParameter)
            {
                _sourceParameter = sourceParameter;
                _targetParameter = targetParameter;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                return node == _sourceParameter ? _targetParameter : base.VisitParameter(node);
            }

            protected override Expression VisitMember(MemberExpression node)
            {
                var visitedExpression = Visit(node.Expression);
                if (visitedExpression == null)
                {
                    return base.VisitMember(node);
                }

                if (visitedExpression.Type == node.Expression?.Type)
                {
                    return Expression.MakeMemberAccess(visitedExpression, node.Member);
                }

                var mappedMember = FindMappedMember(visitedExpression.Type, node.Member);
                return Expression.MakeMemberAccess(visitedExpression, mappedMember);
            }

            private static MemberInfo FindMappedMember(Type targetType, MemberInfo sourceMember)
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase;

                return sourceMember.MemberType switch
                {
                    MemberTypes.Property => targetType.GetProperty(sourceMember.Name, flags)
                        ?? throw new InvalidOperationException($"Cannot map property '{sourceMember.Name}' to '{targetType.Name}'."),
                    MemberTypes.Field => targetType.GetField(sourceMember.Name, flags)
                        ?? throw new InvalidOperationException($"Cannot map field '{sourceMember.Name}' to '{targetType.Name}'."),
                    _ => throw new NotSupportedException($"Member type '{sourceMember.MemberType}' is not supported.")
                };
            }
        }
    }
}
