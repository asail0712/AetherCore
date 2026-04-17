using Microsoft.Extensions.DependencyInjection;

namespace AetherCore.Utility.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class AutoInjectAttribute : Attribute
    {
        public ServiceLifetime Lifetime { get; }

        public AutoInjectAttribute(ServiceLifetime lifetime)
        {
            Lifetime = lifetime;
        }
    }
}
