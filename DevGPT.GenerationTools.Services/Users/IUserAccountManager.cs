using DevGPT.GenerationTools.Services.Users;
using Common.Models;
using DevGPT.GenerationTools.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Models.DTO;

namespace DevGPT.GenerationTools.Services.Users
{
    public interface IUserAccountManager
    {
        Task<bool> CheckPassword(string account, string password);
        Task<Result<DevGPTStoreUser>> Create(DevGPTStoreUser user);
        Task<Result<DevGPTStoreUser>> Update(DevGPTStoreUser user);
        Task<Result<string>> Delete(string userId);
        Task<DevGPTStoreUser> GetUser(string id);
        Task<List<DevGPTStoreUser>> GetUsers();
        Task UpdateRoles(DevGPTStoreUser user);
    }
}

