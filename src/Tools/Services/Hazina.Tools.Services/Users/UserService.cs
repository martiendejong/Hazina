using Hazina.Tools.Services.Users;
using Common.Models;
using Common.Models.DTO;
using Hazina.Tools.Models;
using HazinaStore.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hazina.Tools.Data;

using Hazina.Tools.Data;
namespace HazinaStore.Services
{
    public class UserService
    {
        private readonly HazinaStoreConfig _config;
        private readonly IUserAccountManager _accountManager;
        private readonly ProjectsRepository _projects;

        public UserService(HazinaStoreConfig config, IUserAccountManager accountManager, ProjectsRepository projects)
        {
            _config = config;
            _accountManager = accountManager;
            _projects = projects;
        }

        public Task<bool> CheckPassword(string account, string password)
            => _accountManager.CheckPassword(account, password);

        public Task<Result<HazinaStoreUser>> Create(HazinaStoreUser user)
            => _accountManager.Create(user);

        public Task<Result<HazinaStoreUser>> Update(HazinaStoreUser user)
            => _accountManager.Update(user);

        public Task<Result<string>> Delete(string userId)
            => _accountManager.Delete(userId);

        public Task<HazinaStoreUser> GetUser(string id)
            => _accountManager.GetUser(id);

        public Task<List<HazinaStoreUser>> GetUsers()
            => _accountManager.GetUsers();

        public async Task<List<HazinaStoreUser>> GetUsers(string project)
        {
            var users = await _accountManager.GetUsers();
            return users.Where(u => u.Projects != null && u.Projects.Contains(project)).ToList();
        }
    }
}

