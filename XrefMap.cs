namespace NormandErwan.DocFxForUnity
{
    /// <summary>
    /// Represents a xref map file.
    /// </summary>
    public class XrefMap
    {
        public bool sorted { get; set; }

        public XrefMapReference[] references { get; set; }
    }
}