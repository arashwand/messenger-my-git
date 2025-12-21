namespace Messenger.API.RequestDTOs
{
    public record UpdateClassGroupRequest(string LevelName, string ClassTiming, bool IsActive, int LeftSes, DateTime EndDate);
}
