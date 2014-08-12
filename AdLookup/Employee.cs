using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AdLookup
{
    public class Employee
    {
        public Employee()
        {
            Title = String.Empty;
            OtherPhone = String.Empty;
            Mobile = String.Empty;
            Fax = String.Empty;
            OfficePhone = String.Empty;
            EmployeeId = String.Empty;
            Location = String.Empty;
            Department = String.Empty;
            ShortName = String.Empty;
            Email = String.Empty;
            DisplayName = String.Empty;
            LastName = String.Empty;
            FirstName = String.Empty;
        }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string DisplayName { get; set; }

        public string Email { get; set; }

        public string ShortName { get; set; }

        public string Department { get; set; }

        public string Location { get; set; }

        public string EmployeeId { get; set; }

        public string OfficePhone { get; set; }

        public string Fax { get; set; }

        public string Mobile { get; set; }

        public string OtherPhone { get; set; }

        public string Title { get; set; }
    }
}
