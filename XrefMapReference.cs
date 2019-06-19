using YamlDotNet.Serialization;

namespace NormandErwan.DocFxForUnity
{
    /// <summary>
    /// Represents a reference item on a <see cref="XrefMap"/> file.
    /// </summary>
    public class XrefMapReference
    {
        public string uid { get; set; }

        public string name { get; set; }

        [YamlMember(Alias = "name.vb")]
        public string nameVb { get; set; }

        public string href { get; set; }

        public string commentId { get; set; }

        public string isSpec { get; set; }

        public string fullName { get; set; }

        [YamlMember(Alias = "fullName.vb")]
        public string fullNameVb { get; set; }

        public string nameWithType { get; set; }

        [YamlMember(Alias = "nameWithType.vb")]
        public string nameWithTypeVb { get; set; }
    }
}