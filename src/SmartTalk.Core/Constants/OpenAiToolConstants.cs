namespace SmartTalk.Core.Constants;

public static class OpenAiToolConstants
{
    // Ai speech assistant tools
    public const string Hangup = "hangup";
    public const string TransferCall = "transfer_call";
    public const string ConfirmOrder = "order";
    public const string HandlePhoneOrderIssues = "handle_phone_order_issues";
    public const string HandleThirdPartyDelayedDelivery = "handle_third_party_delayed_delivery";
    public const string HandleThirdPartyPickupTimeChange = "handle_third_party_pickup_time_change";
    public const string HandleThirdPartyFoodQuality = "handle_third_party_food_quality";
    public const string HandleThirdPartyUnexpectedIssues = "handle_third_party_unexpected_issues";
    public const string HandlePromotionCalls = "handle_promotion_calls";
    public const string CheckOrderStatus = "check_order_status";
    public const string RequestOrderDelivery = "request_order_delivery";
    public const string ConfirmCustomerInformation = "confirm_customer_name_phone";
    public const string ConfirmPickupTime = "confirm_pickup_time";
    public const string RepeatOrder = "repeat_order";
    public const string SatisfyOrder = "satisfy_order";
    
    public const string Complaint = "complaint";
    public const string DeliveryTracking = "delivery_tracking";
    public const string DriverDeliveryRelatedCommunication = "driver_delivery_related_communication";
    public const string LessGoodsDelivered = "less_goods_delivered";
    public const string ReturnGoods = "return_goods";
    public const string Refund = "refund";
    public const string RefuseToAcceptGoods = "refuse_to_accept_goods";
    public const string PickUpGoodsFromTheWarehouse = "pick_up_goods_from_the_warehouse";

    public const string CalculateOrderAmount = "calculate_order_amount";
}