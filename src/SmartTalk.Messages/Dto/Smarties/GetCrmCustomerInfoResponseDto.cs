namespace SmartTalk.Messages.Dto.Smarties;

/// <summary>CRM customer info response.</summary>
public class GetCrmCustomerInfoResponseDto
{
    /// <summary>CRM customer info list.</summary>
    public List<CrmCustomerInfoDto> Data { get; set; } = [];
}

/// <summary>CRM customer info.</summary>
public class CrmCustomerInfoDto
{
    /// <summary>Customer name.</summary>
    public string Name { get; set; }

    /// <summary>Customer address.</summary>
    public string Address { get; set; }

    /// <summary>Purchased products.</summary>
    public List<CrmCustomerProductDto> Products { get; set; } = [];
}

/// <summary>CRM product info.</summary>
public class CrmCustomerProductDto
{
    /// <summary>Product name.</summary>
    public string Name { get; set; }

    /// <summary>Created time.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Product attributes.</summary>
    public List<CrmCustomerProductAttributeDto> Attributes { get; set; } = [];
}

/// <summary>CRM product attribute info.</summary>
public class CrmCustomerProductAttributeDto
{
    /// <summary>Attribute name.</summary>
    public string Name { get; set; }

    /// <summary>Attribute options.</summary>
    public List<CrmCustomerProductAttributeOptionDto> Options { get; set; } = [];
}

/// <summary>CRM product attribute option info.</summary>
public class CrmCustomerProductAttributeOptionDto
{
    /// <summary>Option name.</summary>
    public string Name { get; set; }
}
