using DevGPT.GenerationTools.Models;
﻿using System.Collections.Generic;

namespace DevGPT.GenerationTools.Models
{
    public class DevGPTStoreUser : IDevGPTStoreUserInfo
    {
        public string Id { get; set; } = "";
        public string Account { get; set; } = "";
        public string Email { get; set; } = "";
        public string? Password { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Role { get; set; } = "Extern";
        public List<string> Projects { get; set; } = new List<string>();
        public DevGPTStoreUserInfo GetUserInfo() => new DevGPTStoreUserInfo
        {
            Id = Id,
            FirstName = FirstName,
            LastName = LastName,
            Projects = Projects,
            Role = Role
        };
        public void SetUserInfo(IDevGPTStoreUserInfo userInfo)
        {
            FirstName = userInfo.FirstName;
            LastName = userInfo.LastName;
            Projects = userInfo.Projects;
            Role = userInfo.Role;
        }
    }

    public class DevGPTStoreUserInfo : IDevGPTStoreUserInfo
    {
        public string Id { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Role { get; set; } = "Extern";
        public List<string> Projects { get; set; } = new List<string>();
    }
}
