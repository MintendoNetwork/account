using System.Xml.Serialization;

// XML element list for account creation stuff
[XmlRoot("person")]
public class PersonDoc
{
    [XmlElement("birth_date")]
    public string? BirthDate { get; set; }

    [XmlElement("user_id")]
    public string? UserId { get; set; }

    [XmlElement("password")]
    public string? Password { get; set; }

    [XmlElement("country")]
    public string? Country { get; set; }

    [XmlElement("language")]
    public string? Language { get; set; }

    [XmlElement("tz_name")]
    public string? TzName { get; set; }

    [XmlElement("agreement")]
    public List<AgreementDoc> Agreements { get; set; } = new();

    [XmlElement("email")]
    public EmailDoc? Email { get; set; }

    [XmlElement("gender")]
    public string? Gender { get; set; }

    [XmlElement("region")]
    public int Region { get; set; }

    [XmlElement("marketing_flag")]
    public string? MarketingFlag { get; set; }

    [XmlElement("off_device_flag")]
    public string? OffDeviceFlag { get; set; }

    [XmlElement("mii")]
    public MiiDoc? Mii { get; set; }
}

public class AgreementDoc
{
    [XmlElement("agreement_date")]
    public string? AgreementDate { get; set; }

    [XmlElement("country")]
    public string? Country { get; set; }

    [XmlElement("type")]
    public string? Type { get; set; }

    [XmlElement("version")]
    public string? Version { get; set; }
}

public class EmailDoc
{
    [XmlElement("address")]
    public string? Address { get; set; }

    [XmlElement("owned")]
    public string? Owned { get; set; }

    [XmlElement("parent")]
    public string? Parent { get; set; }

    [XmlElement("primary")]
    public string? Primary { get; set; }

    [XmlElement("validated")]
    public string? Validated { get; set; }

    [XmlElement("type")]
    public string? Type { get; set; }
}

public class MiiDoc
{
    [XmlElement("name")]
    public string? Name { get; set; }

    [XmlElement("primary")]
    public string? Primary { get; set; }

    [XmlElement("data")]
    public string? Data { get; set; }
}