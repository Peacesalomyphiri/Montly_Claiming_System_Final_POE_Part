 Montly_Claiming_System
 Contract Monthly Claim System
Description
The Contract Monthly Claim System is a web-based application designed to manage and process monthly claims for lecturers. The system allows lecturers to submit their claims, and administrators to review, approve, or reject claims. It also includes functionalities for managing lecturer details and generating reports for approved claims.
Features
Lecturer Management: Admin can add, edit, and manage lecturers' information (Name, Department, Email).
Claim Submission: Lecturers can submit their claims, including the number of hours worked and hourly rate.
Claim Management: Admin can approve or reject claims based on set criteria.
Claim History: Lecturers can view their past claims and their status.
Reporting: Admin can generate reports for approved claims.
User Authentication: Secure login system for lecturers and admins.
 Installation

1. Clone the repository:
   git clone https://github.com/Peacesalomyphiri/Montly_Claiming_System_Final_POE_Part.git
2. Navigate to the project directory:
   cd Monthly-Claiming-System
3. Install dependencies:
   - Make sure you have .NET Core SDK installed.
   - Run the following command to restore the necessary packages:
     dotnet restore
4. Set up the database:
   - Create and configure the necessary database (e.g., SQL Server or MySQL).
   - Apply migrations to set up the database schema:
     ```bash
     dotnet ef database update
     ```
5. Run the application:
   - Start the application:
     ```bash
     dotnet run
     ```
   - Open the browser and go to `https://localhost:7152/' to view the system.

Usage
- Lecturers can log in to submit their monthly claims and view the status of their past claims.
- Administrators can manage lecturers, review submitted claims, approve/reject claims, and generate reports for approved claims.
Technologies Used
- Backend: ASP.NET Core MVC
- Frontend: HTML, CSS, JavaScript (Bootstrap for styling)
- Database: Wamp Server / myphpAdmin MySQL / localhost
- Authentication: ASP.NET Core Identity


