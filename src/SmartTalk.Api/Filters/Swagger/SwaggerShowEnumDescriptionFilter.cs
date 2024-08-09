using System.Reflection;
using System.ComponentModel;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SmartTalk.Api.Filters.Swagger;

public class SwaggerShowEnumDescriptionFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type.IsEnum)
        {
            var titleItems = new List<string>();
            
            foreach (var e in Enum.GetValues(context.Type))
            {
                var fieldName = e.ToString();
                
                if (fieldName != null)
                {
                    titleItems.Add($"{(int)e}：{context.Type.GetField(fieldName)?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? fieldName}");
                }
            }
            
            schema.Description = string.Join("；", titleItems);
        }
    }
}