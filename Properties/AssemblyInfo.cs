using Rhino.PlugIns;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Plug-in Description Attributes - all of these are optional.
// These will show in Rhino's option dialog, in the tab Plug-ins.
[assembly: PlugInDescription(DescriptionType.Address, "Lodz, Prochnika 35/22")]
[assembly: PlugInDescription(DescriptionType.Country, "Poland")]
[assembly: PlugInDescription(DescriptionType.Email, "hello@differential.studio")]
[assembly: PlugInDescription(DescriptionType.Organization, "Differential")]
[assembly: PlugInDescription(DescriptionType.UpdateUrl, "https://github.com/differential-studio/RhinoM8")]
[assembly: PlugInDescription(DescriptionType.WebSite, "https://www.differential.studio")]

// Icons should be Windows .ico files and contain 32-bit images in the following sizes: 16, 24, 32, 48, and 256.
[assembly: PlugInDescription(DescriptionType.Icon, "RhinoM8.EmbeddedResources.plugin-utility.ico")]

// The following GUID is for the ID of the typelib if this project is exposed to COM
// This will also be the Guid of the Rhino plug-in
[assembly: Guid("7d68e79b-c477-488f-aac5-d0beed2008b8")]
