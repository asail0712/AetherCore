using AetherCore.Entities;
using AetherCore.Utility;
using AetherCore.Utility.Lincense;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Reflection;

namespace AetherCore.DataAccess
{
    // 泛型的 EF Core SQL 資料存取基底類別
    public abstract class SqlDataAccess<TEntity> : IDataAccess<TEntity>
        where TEntity : class, IDBEntity, new()
    {
        protected readonly DbContext _dbContext;
        protected readonly DbSet<TEntity> _dbSet;

        private string _searchFieldName = nameof(IDBEntity.Id);
        private List<string> _noUpdateList;

        protected SqlDataAccess(DbContext dbContext)
        {
            _dbContext = dbContext;
            _dbSet = dbContext.Set<TEntity>();
            _noUpdateList = new List<string>();
        }

        // 加入不允許被更新的欄位名稱
        protected void AddNoUpdateKey(string noUpdateKey)
        {
            _noUpdateList.Add(noUpdateKey);
            _noUpdateList = _noUpdateList.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        // SQL 索引請在 DbContext.OnModelCreating 使用 HasIndex 設定；此處只設定查詢 key 欄位
        protected void EnsureIndexCreated(string searchFieldName)
        {
            _searchFieldName = searchFieldName;
        }

        public virtual async Task<TEntity?> InsertAsync(TEntity entity)
        {
            await XPlanLicenseRuntime.EnsureValidOrThrowAsync();

            await _dbSet.AddAsync(entity);
            await _dbContext.SaveChangesAsync();

            return entity;
        }

        public virtual async Task<List<TEntity>?> QueryAllAsync()
        {
            return await _dbSet.ToListAsync();
        }

        public virtual async Task<TEntity?> QueryAsync(string key)
        {
            return await _dbSet.FirstOrDefaultAsync(BuildKeyPredicate(key));
        }

        public virtual async Task<List<TEntity>?> QueryAsync(List<string> keys)
        {
            if (keys == null || keys.Count == 0)
            {
                return null;
            }

            return await _dbSet.Where(BuildKeysPredicate(keys)).ToListAsync();
        }

        public virtual async Task<List<TEntity>?> QueryAsync(Expression<Func<TEntity, bool>> predicate)
        {
            return await _dbSet.Where(predicate).ToListAsync();
        }

        public virtual async Task<bool> UpdateAsync(string key, TEntity entity)
        {
            var existingEntity = await _dbSet.FirstOrDefaultAsync(BuildKeyPredicate(key));

            if (existingEntity == null)
            {
                return false;
            }

            entity.UpdatedAt = DateTime.UtcNow;

            var entry = _dbContext.Entry(existingEntity);
            entry.CurrentValues.SetValues(entity);

            foreach (var fieldName in GetExcludedFieldNames())
            {
                var property = entry.Properties.FirstOrDefault(p =>
                    string.Equals(p.Metadata.Name, fieldName, StringComparison.OrdinalIgnoreCase));

                if (property != null)
                {
                    property.IsModified = false;
                }
            }

            entry.Property(nameof(IDBEntity.UpdatedAt)).CurrentValue = DateTime.UtcNow;
            entry.Property(nameof(IDBEntity.UpdatedAt)).IsModified = true;

            return await _dbContext.SaveChangesAsync() > 0;
        }

        public virtual async Task<bool> UpdateAsync(
            Expression<Func<TEntity, bool>> predicate,
            params UpdateField<TEntity>[] updates)
        {
            if (updates == null || updates.Length == 0)
            {
                return false;
            }

            var entities = await _dbSet.Where(predicate).ToListAsync();

            if (entities.Count == 0)
            {
                return false;
            }

            foreach (var update in updates)
            {
                var memberName = GetSelectedMemberName(update.FieldSelector);
                ValidateUpdatableField(memberName);
                var property = GetEntityProperty(memberName);

                foreach (var entity in entities)
                {
                    property.SetValue(entity, ConvertValue(update.Value, property.PropertyType));
                }
            }

            foreach (var entity in entities)
            {
                entity.UpdatedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();
            return true;
        }

        public virtual async Task<bool> DeleteAsync(string key)
        {
            var entity = await _dbSet.FirstOrDefaultAsync(BuildKeyPredicate(key));

            if (entity == null)
            {
                return false;
            }

            _dbSet.Remove(entity);
            return await _dbContext.SaveChangesAsync() > 0;
        }

        public virtual async Task<bool> ExistsAsync(string key)
        {
            return await _dbSet.AnyAsync(BuildKeyPredicate(key));
        }

        public virtual async Task<bool> ExistsAsync(List<string> keys)
        {
            if (keys == null || keys.Count == 0)
            {
                return false;
            }

            return await _dbSet.AnyAsync(BuildKeysPredicate(keys));
        }

        public virtual async Task<TEntity?> FindLastAsync()
        {
            return await _dbSet
                .OrderByDescending(entity => entity.UpdatedAt)
                .FirstOrDefaultAsync();
        }

        private Expression<Func<TEntity, bool>> BuildKeyPredicate(string key)
        {
            var parameter = Expression.Parameter(typeof(TEntity), "entity");
            var property = Expression.Property(parameter, GetEntityProperty(_searchFieldName));
            var convertedKey = ConvertValue(key, property.Type);
            var constant = Expression.Constant(convertedKey, property.Type);
            var equals = Expression.Equal(property, constant);

            return Expression.Lambda<Func<TEntity, bool>>(equals, parameter);
        }

        private Expression<Func<TEntity, bool>> BuildKeysPredicate(List<string> keys)
        {
            var propertyInfo = GetEntityProperty(_searchFieldName);
            var parameter = Expression.Parameter(typeof(TEntity), "entity");
            var property = Expression.Property(parameter, propertyInfo);
            var typedKeys = CreateTypedKeyList(keys, property.Type);
            var containsMethod = typedKeys.GetType().GetMethod(nameof(List<string>.Contains), new[] { property.Type })!;
            var contains = Expression.Call(Expression.Constant(typedKeys), containsMethod, property);

            return Expression.Lambda<Func<TEntity, bool>>(contains, parameter);
        }

        private PropertyInfo GetEntityProperty(string propertyName)
        {
            return typeof(TEntity)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(property => string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Property '{propertyName}' was not found on entity '{typeof(TEntity).Name}'.");
        }

        private void ValidateUpdatableField(string sourceMemberName)
        {
            var excludedFields = GetExcludedFieldNames();

            if (excludedFields.Contains(sourceMemberName, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Field '{sourceMemberName}' is not allowed to be updated.");
            }
        }

        private HashSet<string> GetExcludedFieldNames()
        {
            return new HashSet<string>(
                new[] { nameof(IDBEntity.Id), nameof(IDBEntity.CreatedAt), _searchFieldName }
                    .Concat(_noUpdateList ?? Enumerable.Empty<string>()),
                StringComparer.OrdinalIgnoreCase);
        }

        private static string GetSelectedMemberName(LambdaExpression fieldSelector)
        {
            return fieldSelector.Body switch
            {
                MemberExpression memberExpression => memberExpression.Member.Name,
                UnaryExpression { Operand: MemberExpression memberExpression } => memberExpression.Member.Name,
                _ => throw new NotSupportedException("Only direct member access expressions are supported.")
            };
        }

        private static object? ConvertValue(object? value, Type targetType)
        {
            if (value == null)
            {
                return null;
            }

            var nonNullableType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (nonNullableType.IsEnum)
            {
                return value is string enumString
                    ? Enum.Parse(nonNullableType, enumString)
                    : Enum.ToObject(nonNullableType, value);
            }

            if (nonNullableType.IsAssignableFrom(value.GetType()))
            {
                return value;
            }

            return Convert.ChangeType(value, nonNullableType);
        }

        private static object CreateTypedKeyList(List<string> keys, Type itemType)
        {
            var listType = typeof(List<>).MakeGenericType(itemType);
            var list = Activator.CreateInstance(listType)!;
            var addMethod = listType.GetMethod("Add")!;

            foreach (var key in keys)
            {
                addMethod.Invoke(list, new[] { ConvertValue(key, itemType) });
            }

            return list;
        }
    }
}
