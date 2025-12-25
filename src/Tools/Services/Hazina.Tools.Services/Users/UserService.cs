using DevGPT.GenerationTools.Services.Users;
using Common.Models;
using Common.Models.DTO;
using DevGPT.GenerationTools.Models;
using DevGPTStore.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevGPT.GenerationTools.Data;

using DevGPT.GenerationTools.Data;
namespace DevGPTStore.Services
{
    public class UserService
    {
        private readonly DevGPTStoreConfig _config;
        private readonly IUserAccountManager _accountManager;
        private readonly ProjectsRepository _projects;

        public UserService(DevGPTStoreConfig config, IUserAccountManager accountManager, ProjectsRepository projects)
        {
            _config = config;
            _accountManager = accountManager;
            _projects = projects;
        }

        public Task<bool> CheckPassword(string account, string password)
            => _accountManager.CheckPassword(account, password);

        public Task<Result<DevGPTStoreUser>> Create(DevGPTStoreUser user)
            => _accountManager.Create(user);

        public Task<Result<DevGPTStoreUser>> Update(DevGPTStoreUser user)
            => _accountManager.Update(user);

        public Task<Result<string>> Delete(string userId)
            => _accountManager.Delete(userId);

        public Task<DevGPTStoreUser> GetUser(string id)
            => _accountManager.GetUser(id);

        public Task<List<DevGPTStoreUser>> GetUsers()
            => _accountManager.GetUsers();

        public async Task<List<DevGPTStoreUser>> GetUsers(string project)
        {
            var users = await _accountManager.GetUsers();
            return users.Where(u => u.Projects != null && u.Projects.Contains(project)).ToList();
        }
    }
}

