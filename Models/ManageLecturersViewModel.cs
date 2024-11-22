namespace monthly_claiming_system.Models
{
    public class ManageLecturersViewModel
    {
        public List<Lecturer> Lecturers { get; set; } // List of lecturers to be displayed
        public Lecturer LecturerToEdit { get; set; }  // Lecturer to be edited
    }
}
