namespace monthly_claiming_system.Models
{
    public class SupportingDocuments
    {
            public int Id { get; set; }
            public int ClaimId { get; set; }
            public string FileName { get; set; }
            public string FilePath { get; set; }
        
    }
}


