using System;
using System.Collections.Generic;
using System.Data;
using System.Runtime.CompilerServices;
using DevGPT.GenerationTools.Models.WordPress.Blogs;
using DevGPT.GenerationTools.Models.Social;

namespace DevGPT.GenerationTools.Models
{
    public class Project : Serializer<Project>
    {
        public DateTime Created { get; set; } = new DateTime(2025, 1, 1);
        public bool Archived { get; set; } = false;
        public string? Id { get; set; } = "";
        public string Name { get; set; } = "Projectnaam";
        public string Description { get; set; }
        public string Status { get; set; } = "INITIAL";
        public string? Website { get; set; }
        public string? Employees { get; set; }
        public string? Address { get; set; }
        public string? Industry { get; set; }
        public string? Persona { get; set; }
        public string? ContactName { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public List<SocialMediaAddress> SocialMediaAddresses { get; set; } = new List<SocialMediaAddress>();
        //public List<DevGPTStoreDocument> Documents { get; set; } = new List<DevGPTStoreDocument>();

        public string? KlantSpecifiekePrompt { get; set; }
        public string? Doelgroep { get; set; }
        public List<string> SamenwerkingOptions { get; set; } = new List<string>();
        public List<string> Countries { get; set; } = new List<string>();
        public List<Place> Places { get; set; } = new List<Place>();
        public int? FromAge { get; set; }
        public int? ToAge { get; set; }
        public string? Taal { get; set; }
        public string? KenmerkenDoelgroep { get; set; }
        // Projects can be identified as e.g. "chat" projects via this field.
        public string ProjectType { get; set; } = "customer";
        public bool IsPinned { get; set; } = false; // Added for pinning support

        // --- ADD WORDPRESS SETTINGS FIELD ---
        public WordpressSettings? Wordpress { get; set; }

        // --- PROJECT SETTINGS ---
        public bool NotificationsEnabled { get; set; } = true;
        public string Language { get; set; } = "en";
        public ImageModel ImageModel { get; set; } = ImageModel.DallE3;
    }

    public class Place
    {
        public string Plaats { get; set; }
        public string Straal { get; set; }
    }

    public class WordpressSettings
    {
        public string BaseUrl { get; set; } = "";

        public string Username { get; set; } = "";

        public string Password { get; set; } = "";

        public string CustomHTML { get; set; } = "";

        public string CustomCSS { get; set; } = "";

        public string CustomJS { get; set; } = "";
    }
}

