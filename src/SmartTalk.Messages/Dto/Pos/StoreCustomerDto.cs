namespace SmartTalk.Messages.Dto.Pos;

public class StoreCustomerDto
{
    public int Id { get; set; }

    public int StoreId { get; set; }

    public string Name { get; set; }

    public string Phone { get; set; }

    public string Address { get; set; }

    public string Latitude { get; set; }

    public string Longitude { get; set; }

    public string Notes { get; set; }
    
    public string Timezone { get; set; }

    public bool IsDeleted { get; set; }

    public int? CreatedBy { get; set; }

    public DateTimeOffset CreatedDate { get; set; }

    public int? LastModifiedBy { get; set; }

    public DateTimeOffset LastModifiedDate { get; set; }
}