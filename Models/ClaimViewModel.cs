namespace monthly_claiming_system.Models
{
    public class ClaimViewModel
    {
        public int Id { get; set; }
        public string? LecturerName { get; set; } 
        public string? AdditionalNotes { get; set; } 
        public string LecturerDepartment { get; set; } 
        public string Month { get; set; } 
        public string? SupportingDocument { get; set; }
        public decimal HoursWorked { get; set; }
        public decimal HourlyRate { get; set; }
        public decimal TotalClaim { get; set; }
        public string Status { get; set; }
        public string RejectionReason { get; set; }
    }
}
