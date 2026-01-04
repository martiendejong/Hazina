using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Hazina.Tools.Models
{
    /// <summary>
    /// Represents a reusable brand document fragment (header or footer) that can be
    /// injected into generated documents like business plans, company letters, and menus.
    /// </summary>
    public class BrandDocumentFragment : ChatResponse<BrandDocumentFragment>
    {
        /// <summary>
        /// Type of fragment: "header" or "footer"
        /// </summary>
        public string FragmentType { get; set; } = "header";

        /// <summary>
        /// Document type this fragment is designed for: "company-letter", "business-plan", "menu"
        /// </summary>
        public string DocumentType { get; set; } = "";

        /// <summary>
        /// Layout definition for the fragment structure
        /// </summary>
        public FragmentLayout Layout { get; set; } = new FragmentLayout();

        /// <summary>
        /// Content slots containing the actual values for the fragment
        /// Key is the slot identifier (e.g., "company-name", "address", "phone")
        /// </summary>
        public Dictionary<string, FragmentSlot> Slots { get; set; } = new Dictionary<string, FragmentSlot>();

        /// <summary>
        /// Brand styling tokens applied to this fragment
        /// </summary>
        public FragmentStyling Styling { get; set; } = new FragmentStyling();

        /// <summary>
        /// AI rationale for design decisions made during generation
        /// </summary>
        public string Feedback { get; set; } = "";

        /// <summary>
        /// Metadata about the fragment's generation and dependencies
        /// </summary>
        public FragmentMetadata Metadata { get; set; } = new FragmentMetadata();

        [JsonIgnore]
        public override BrandDocumentFragment _example => new BrandDocumentFragment
        {
            FragmentType = "header",
            DocumentType = "company-letter",
            Layout = new FragmentLayout
            {
                Structure = "row",
                Alignment = "space-between",
                Elements = new List<LayoutElement>
                {
                    new LayoutElement
                    {
                        Type = "logo",
                        Position = "left",
                        Size = "medium"
                    },
                    new LayoutElement
                    {
                        Type = "slot",
                        SlotId = "company-info",
                        Position = "right",
                        Size = "medium"
                    }
                }
            },
            Slots = new Dictionary<string, FragmentSlot>
            {
                { "company-name", new FragmentSlot
                    {
                        Value = "Acme Corporation",
                        Source = "brand",
                        Editable = true,
                        Generatable = true,
                        Label = "Company Name"
                    }
                },
                { "address", new FragmentSlot
                    {
                        Value = "123 Business Street, City",
                        Source = "gathered",
                        Editable = true,
                        Generatable = false,
                        Label = "Address",
                        GatheredDataKey = "address"
                    }
                }
            },
            Styling = new FragmentStyling
            {
                PrimaryColor = "#2C1810",
                SecondaryColor = "#D4A574",
                BackgroundColor = "#FFFFFF",
                FontFamily = "Arial",
                FontSize = "12pt"
            },
            Feedback = "I positioned the logo on the left for immediate brand recognition..."
        };

        [JsonIgnore]
        public override string _signature => @"{
  FragmentType: string,
  DocumentType: string,
  Layout: {
    Structure: string,
    Alignment: string,
    Elements: [{
      Type: string,
      SlotId: string,
      Position: string,
      Size: string,
      Style: object
    }]
  },
  Slots: {
    [key: string]: {
      Value: string,
      Source: string,
      Editable: boolean,
      Generatable: boolean,
      Label: string,
      GatheredDataKey: string
    }
  },
  Styling: {
    PrimaryColor: string,
    SecondaryColor: string,
    BackgroundColor: string,
    FontFamily: string,
    FontSize: string,
    LogoUrl: string
  },
  Feedback: string,
  Metadata: {
    GeneratedAt: string,
    UpdatedAt: string,
    Version: number,
    Dependencies: [string]
  }
}";
    }

    /// <summary>
    /// Defines the visual structure and element placement of a fragment
    /// </summary>
    public class FragmentLayout
    {
        /// <summary>
        /// Layout structure type: "row", "column", "grid"
        /// </summary>
        public string Structure { get; set; } = "row";

        /// <summary>
        /// Content alignment: "left", "center", "right", "space-between"
        /// </summary>
        public string Alignment { get; set; } = "left";

        /// <summary>
        /// Ordered list of layout elements defining the visual structure
        /// </summary>
        public List<LayoutElement> Elements { get; set; } = new List<LayoutElement>();
    }

    /// <summary>
    /// Individual element within a fragment layout
    /// </summary>
    public class LayoutElement
    {
        /// <summary>
        /// Element type: "logo", "text", "divider", "spacer", "slot"
        /// </summary>
        public string Type { get; set; } = "text";

        /// <summary>
        /// If Type="slot", references a key in the Slots dictionary
        /// </summary>
        public string SlotId { get; set; } = "";

        /// <summary>
        /// Position within the layout: "left", "center", "right"
        /// </summary>
        public string Position { get; set; } = "left";

        /// <summary>
        /// Size specification: "small", "medium", "large", or specific dimensions
        /// </summary>
        public string Size { get; set; } = "medium";

        /// <summary>
        /// Additional inline styling for this element
        /// </summary>
        public Dictionary<string, string> Style { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// A content slot within a fragment that can hold editable or generated values
    /// </summary>
    public class FragmentSlot
    {
        /// <summary>
        /// Current value of the slot
        /// </summary>
        public string Value { get; set; } = "";

        /// <summary>
        /// Source of the value: "gathered", "generated", "brand", "user"
        /// - gathered: From gathered-data store (phone, address, etc.)
        /// - generated: AI-generated content
        /// - brand: Derived from brand profile/assets
        /// - user: Manually edited by user
        /// </summary>
        public string Source { get; set; } = "generated";

        /// <summary>
        /// Whether the user can modify this slot value
        /// </summary>
        public bool Editable { get; set; } = true;

        /// <summary>
        /// Whether AI can regenerate this slot. False for factual data like phone/address
        /// </summary>
        public bool Generatable { get; set; } = true;

        /// <summary>
        /// Display label for the editing UI
        /// </summary>
        public string Label { get; set; } = "";

        /// <summary>
        /// If Source="gathered", the key to look up in gathered-data store
        /// </summary>
        public string GatheredDataKey { get; set; } = "";
    }

    /// <summary>
    /// Brand styling tokens applied to a fragment
    /// </summary>
    public class FragmentStyling
    {
        /// <summary>
        /// Primary brand color (hex)
        /// </summary>
        public string PrimaryColor { get; set; } = "";

        /// <summary>
        /// Secondary brand color (hex)
        /// </summary>
        public string SecondaryColor { get; set; } = "";

        /// <summary>
        /// Background color (hex)
        /// </summary>
        public string BackgroundColor { get; set; } = "";

        /// <summary>
        /// Font family name
        /// </summary>
        public string FontFamily { get; set; } = "";

        /// <summary>
        /// Font size specification
        /// </summary>
        public string FontSize { get; set; } = "";

        /// <summary>
        /// URL or base64 of the brand logo
        /// </summary>
        public string LogoUrl { get; set; } = "";
    }

    /// <summary>
    /// Metadata about fragment generation and dependencies
    /// </summary>
    public class FragmentMetadata
    {
        /// <summary>
        /// When the fragment was first generated
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the fragment was last updated
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Version number for tracking changes
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// List of brand asset dependencies (e.g., "logo", "color-scheme")
        /// Used to suggest regeneration when dependencies change
        /// </summary>
        public List<string> Dependencies { get; set; } = new List<string>();
    }
}
