using Hazina.Tools.Models;
﻿using System.Collections.Generic;

namespace Hazina.Tools.Models
{
    public class HazinaStoreUser : IHazinaStoreUserInfo
    {
        public string Id { get; set; } = "";
        public string Account { get; set; } = "";
        public string Email { get; set; } = "";
        public string? Password { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Role { get; set; } = "Extern";
        public List<string> Projects { get; set; } = new List<string>();
        public HazinaStoreUserInfo GetUserInfo() => new HazinaStoreUserInfo
        {
            Id = Id,
            FirstName = FirstName,
            LastName = LastName,
            Projects = Projects,
            Role = Role
        };
        public void SetUserInfo(IHazinaStoreUserInfo userInfo)
        {
            FirstName = userInfo.FirstName;
            LastName = userInfo.LastName;
            Projects = userInfo.Projects;
            Role = userInfo.Role;
        }
    }

    public class HazinaStoreUserInfo : IHazinaStoreUserInfo
    {
        public string Id { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Role { get; set; } = "Extern";
        public List<string> Projects { get; set; } = new List<string>();
    }
}
