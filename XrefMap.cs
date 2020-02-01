namespace DocFxForUnity
{
    /// <summary>
    /// Represents a xref map file.
    /// </summary>
    public class XrefMap<TReference> where TReference : XrefMapReference
    {
        public bool sorted { get; set; }

        public TReference[] references { get; set; }
    }
}