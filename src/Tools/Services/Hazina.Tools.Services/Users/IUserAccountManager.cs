using Hazina.Tools.Services.Users;
using Common.Models;
using Hazina.Tools.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Models.DTO;

namespace Hazina.Tools.Services.Users
{
    public interface IUserAccountManager
    {
        Task<bool> CheckPassword(string account, string password);
        Task<Result<HazinaStoreUser>> Create(HazinaStoreUser user);
        Task<Result<HazinaStoreUser>> Update(HazinaStoreUser user);
        Task<Result<string>> Delete(string userId);
        Task<HazinaStoreUser> GetUser(string id);
        Task<List<HazinaStoreUser>> GetUsers();
        Task UpdateRoles(HazinaStoreUser user);
    }
}

