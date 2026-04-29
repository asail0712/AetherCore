using AetherCore.Controller;

namespace AetherCore.Utility.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
    public sealed class SwaggerApiAttribute : Attribute
    {
        public IReadOnlyCollection<string> DocumentNames { get; }
        public CrudOperation CrudOperations { get; set; } = CrudOperation.All;

        public SwaggerApiAttribute(params string[] documentNames)
        {
            DocumentNames = documentNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .ToArray();
        }
    }
}
