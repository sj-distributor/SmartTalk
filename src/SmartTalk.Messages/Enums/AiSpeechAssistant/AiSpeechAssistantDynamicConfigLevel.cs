namespace SmartTalk.Messages.Enums.AiSpeechAssistant;

public enum AiSpeechAssistantDynamicConfigLevel
{
    // 系统层：定义数据源（如 POS / Hifood）
    System = 1,

    // 类目层：按场景分类（如 价格 / 库存 / 订单）
    Category = 2,

    // 数据层：字段级映射（如 product_price -> 商品价格）
    Data = 3
}
