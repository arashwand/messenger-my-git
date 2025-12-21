namespace Messenger.DTOs;

public class MessageFoulReportDto
{
    public long MessageId { get; set; }
    public string FoulDesc { get; set; }
}

public class MessageFoulReportModelDto
{
    public long? MessageFoulReportId { get; set; }
    public DateTime? FoulReportDateTime { get; set; }
    public long MessageId { get; set; }
    public long FoulReporterUserId { get; set; }
    public string FoulDesc { get; set; }

    public MessageDto MessageDto { get; set; }
    
}
