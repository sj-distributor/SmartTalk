using System.Data;
using System.Text;
using DbUp.Engine;

namespace SmartTalk.Core.DbUpFile.Scripts_2025;

public class Script0065AddAutoTestScenarioData : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        var stringBuilder = new StringBuilder();
        
        stringBuilder.AppendLine($@"
            INSERT INTO auto_test_scenario ( key_name, name,  input_schema, output_schema,  action_config, action_type, created_at) 
            VALUES ('AiOrder', 'AiOrder', 
                    '{{ ""recording"": {{ ""type"": ""string"", ""required"": true, ""desc"": ""录音信息（如URL或标识）"" }},""orderId"": {{ ""type"": ""string"", ""required"": true, ""desc"": ""订单唯一编号"" }},
                 ""customerId"": {{ ""type"": ""string"", ""required"": true, ""desc"": ""客户唯一编号"" }},""detail"": {{
                        ""type"": ""array"",
                        ""required"": true,
                        ""items"": {{
                            ""type"": ""object"",
                            ""properties"": {{
                                ""SerialNumber"": {{ ""type"": ""number"", ""desc"": ""序号"" }},
                                ""Quantity"": {{ ""type"": ""integer"", ""desc"": ""数量"" }},
                                ""ItemDesc"": {{ ""type"": ""number"", ""desc"": ""内容"" }}
                            }}
                        }},
                        ""desc"": ""订单明细列表""
                    }}}}',
                    '{{ }}', '{{ }}',  0,  NOW()); ");
        
        return stringBuilder.ToString();
    }
}