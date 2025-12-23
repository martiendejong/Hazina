
namespace DevGPT.GenerationTools.Models
{
    public interface IDevGPTStoreUserInfo
    {
        string FirstName { get; set; }
        string Id { get; set; }
        string LastName { get; set; }
        List<string> Projects { get; set; }
        string Role { get; set; }
    }
}