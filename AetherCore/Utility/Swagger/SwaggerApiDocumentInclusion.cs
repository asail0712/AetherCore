using AetherCore.Controller;
using AetherCore.Utility.Attributes;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using System.Reflection;

namespace AetherCore.Utility.Swagger
{
    public static class SwaggerApiDocumentInclusion
    {
        private static readonly Dictionary<string, CrudOperation> CrudActionOperations = new()
        {
            ["Create"] = CrudOperation.Create,
            ["GetAll"] = CrudOperation.ReadAll,
            ["Get"] = CrudOperation.Read,
            ["Update"] = CrudOperation.Update,
            ["Delete"] = CrudOperation.Delete,
        };

        public static bool Include(string documentName, ApiDescription apiDescription)
        {
            if (apiDescription.ActionDescriptor is not ControllerActionDescriptor actionDescriptor)
            {
                return false;
            }

            if (CrudActionOperations.TryGetValue(actionDescriptor.ActionName, out var crudOperation))
            {
                return GetControllerAttributes(actionDescriptor)
                    .Any(attr => IsMatch(attr, documentName, crudOperation));
            }

            return GetMethodAttributes(actionDescriptor)
                .Any(attr => IsMatch(attr, documentName));
        }

        private static IEnumerable<SwaggerApiAttribute> GetControllerAttributes(ControllerActionDescriptor actionDescriptor)
        {
            var controllerAttributes = actionDescriptor.ControllerTypeInfo
                .GetCustomAttributes<SwaggerApiAttribute>(inherit: true)
                .ToArray();

            if (controllerAttributes.Length > 0)
            {
                return controllerAttributes;
            }

            return actionDescriptor.EndpointMetadata
                .OfType<SwaggerApiAttribute>();
        }

        private static IEnumerable<SwaggerApiAttribute> GetMethodAttributes(ControllerActionDescriptor actionDescriptor)
        {
            return actionDescriptor.MethodInfo
                .GetCustomAttributes<SwaggerApiAttribute>(inherit: true);
        }

        private static bool IsMatch(SwaggerApiAttribute attribute, string documentName)
        {
            return attribute.DocumentNames.Contains(documentName, StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsMatch(SwaggerApiAttribute attribute, string documentName, CrudOperation crudOperation)
        {
            return IsMatch(attribute, documentName) &&
                   (attribute.CrudOperations & crudOperation) == crudOperation;
        }
    }
}
