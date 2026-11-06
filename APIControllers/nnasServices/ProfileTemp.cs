using System.Globalization;
using System.Xml.Linq;

public record ProfileRow(
    int Pid, string Username, string BirthDate, string Gender, string Country, string Language,
    int Region, string TzName, int UtcOffset,
    bool Active, bool Marketing, bool OffDevice, DateTime CreatedAt, DateTime UpdatedAt,
    string EmailAddress, long EmailId, bool EmailParent, bool EmailPrimary, bool EmailReachable,
    bool EmailValidated, DateTime? EmailValidatedAt,
    string MiiName, bool MiiPrimary, string MiiData, long MiiId, string MiiHash, long MiiImageId);

public static class ProfileXml
{
    static string YN(bool b) => b ? "Y" : "N";
    static string Date(DateTime d) => d.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

    public static string Build(ProfileRow r, string cdnBaseUrl)
    {
        string imageUrl = $"{cdnBaseUrl}/mii/{r.Pid}/standard.tga";

        var person = new XElement("person",
            new XElement("active_flag", YN(r.Active)),
            new XElement("birth_date", r.BirthDate),
            new XElement("country", r.Country),
            new XElement("create_date", Date(r.CreatedAt)),
            
            // Probably not really needed to be filled, but maybe it's a TODO?
            new XElement("device_attributes"), 
            new XElement("gender", r.Gender),
            new XElement("language", r.Language),
            new XElement("updated", Date(r.UpdatedAt)),
            new XElement("marketing_flag", YN(r.Marketing)),
            new XElement("off_device_flag", YN(r.OffDevice)),
            new XElement("pid", r.Pid),
            new XElement("email",
                new XElement("address", r.EmailAddress),
                new XElement("id", r.EmailId),
                new XElement("parent", YN(r.EmailParent)),
                new XElement("primary", YN(r.EmailPrimary)),
                new XElement("reachable", YN(r.EmailReachable)),
                new XElement("type", "DEFAULT"),
                new XElement("updated_by", "USER"),
                new XElement("validated", YN(r.EmailValidated)),
                new XElement("validated_date",
                    r.EmailValidated && r.EmailValidatedAt is { } v ? Date(v) : "")),
            new XElement("mii",
                new XElement("status", "COMPLETED"),
                new XElement("data", r.MiiData.Replace("\r", "").Replace("\n", "")),
                new XElement("id", r.MiiId),
                new XElement("mii_hash", r.MiiHash),
                new XElement("mii_images",
                    new XElement("mii_image",
                        new XElement("cached_url", imageUrl),
                        new XElement("id", r.MiiImageId),
                        new XElement("url", imageUrl),
                        new XElement("type", "standard"))),
                new XElement("name", r.MiiName),
                new XElement("primary", YN(r.MiiPrimary))),
            new XElement("region", r.Region),
            new XElement("tz_name", r.TzName),
            new XElement("user_id", r.Username),
            new XElement("utc_offset", r.UtcOffset));

        return "<?xml version=\"1.0\"?>" + person.ToString(SaveOptions.DisableFormatting);
    }
}