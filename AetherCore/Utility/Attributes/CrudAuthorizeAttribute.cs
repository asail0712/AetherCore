using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AetherCore.Utility.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class CrudAuthorizeAttribute : Attribute
    {
        public string? CreateAuthScheme { get; set; }
        public string? GetAuthScheme { get; set; }
        public string? GetAllAuthScheme { get; set; }
        public string? UpdateAuthScheme { get; set; }
        public string? DeleteAuthScheme { get; set; }

        public CrudAuthorizeAttribute()
        {

        }

        public CrudAuthorizeAttribute(string scheme)
        {
            CreateAuthScheme    = scheme;
            GetAuthScheme       = scheme;
            GetAllAuthScheme    = scheme;
            UpdateAuthScheme    = scheme;
            DeleteAuthScheme    = scheme;
        }

        public string? GetSchemeByActionName(string actionName)
        {
            return actionName switch
            {
                "Create"    => CreateAuthScheme,
                "Get"       => GetAuthScheme,
                "GetAll"    => GetAllAuthScheme,
                "Update"    => UpdateAuthScheme,
                "Delete"    => DeleteAuthScheme,
                _ => null
            };
        }

        public string[] GetSchemesByActionName(string actionName)
        {
            var raw = GetSchemeByActionName(actionName);

            if (string.IsNullOrWhiteSpace(raw))
                return Array.Empty<string>();

            return raw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
    }
}
