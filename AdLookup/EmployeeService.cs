using System.Collections.Generic;
using System.Configuration;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace AdLookup
{
    public class EmployeeService
    {
        private readonly string _adUserName, _adPassword, _adUrl, _domain;

        public EmployeeService()
        {
            _adUserName = ConfigurationManager.AppSettings["AD_User"];
            _adPassword = ConfigurationManager.AppSettings["AD_Password"];
            _adUrl = ConfigurationManager.AppSettings["AD_Url"];
            _domain = ConfigurationManager.AppSettings["Domain"];
        }

        public async Task<List<Employee>> GetEmployeeEntries(EmployeeSearchType type, string searchString)
        {
            switch (type)
            {
                case EmployeeSearchType.EmployeeId:
                    return await SearchAd("employeeID", searchString);
                case EmployeeSearchType.FirstName:
                    return await SearchAd("firstName", searchString);
                case EmployeeSearchType.LastName:
                    return await SearchAd("sn", searchString);
                case EmployeeSearchType.DisplayName:
                    return await SearchAd("displayName", searchString);
                case EmployeeSearchType.ShortName:
                    return await SearchAd("sAMAccountName", searchString);
                case EmployeeSearchType.Email:
                    return await SearchAd("userprincipalname", searchString);
                case EmployeeSearchType.Department:
                    return await SearchAd("department", searchString);
                case EmployeeSearchType.Location:
                    return await SearchAd("physicaldeliveryofficename", searchString);
                case EmployeeSearchType.Mobile:
                    return await SearchAd("mobile", searchString);
                case EmployeeSearchType.OfficePhone:
                    return await SearchAd("telephonenumber", searchString);
                case EmployeeSearchType.OtherPhone:
                    return await SearchAd("othertelephone", searchString);
                case EmployeeSearchType.Fax:
                    return await SearchAd("facsimileTelephone­Number", searchString);
                case EmployeeSearchType.Title:
                    return await SearchAd("title", searchString);
                default: return new List<Employee>();
            }
        }

        public async Task<List<Employee>> GetEmployeeByShortName(string shortName)
        {
            return await GetEmployeeEntries(EmployeeSearchType.ShortName, shortName);
        }

        public async Task<List<Employee>> GetEmployeeByEmail(string mail)
        {
            return await GetEmployeeEntries(EmployeeSearchType.Email, mail);
        }

        public async Task<List<Employee>> GetEmployeeByDisplayName(string displayName)
        {
            return await GetEmployeeEntries(EmployeeSearchType.DisplayName, displayName);
        }

        public async Task<List<Employee>> GetEmployeeByEmpNr(int empNumber)
        {
            return await GetEmployeeEntries(EmployeeSearchType.EmployeeId, empNumber.ToString());
        }

        public async Task<List<Employee>> GetMembersInGroup(string groupName)
        {
            return await Task.Run(async () =>
            {
                var result = new List<Employee>();

                using (var context = new PrincipalContext(ContextType.Domain, _domain))
                {
                    using (var groupPrincipal = GroupPrincipal.FindByIdentity(context, groupName))
                    {
                        if (groupPrincipal == null) return result;

                        var shortnames = groupPrincipal.GetMembers(true).Select(member => member.SamAccountName);

                        foreach (var name in shortnames)
                        {
                            var employeeEntries = await GetEmployeeByShortName(name);
                            if (employeeEntries != null && employeeEntries.Any())
                                result.AddRange(employeeEntries);
                        }
                    }
                }
                return result;
            });
        }

        // Alternative way of getting employee info
        private List<Employee> SearchEmp(string query)
        {
            var result = new List<Employee>();

            using (var context = new PrincipalContext(ContextType.Domain, _domain))
            {
                using (UserPrincipal member = UserPrincipal.FindByIdentity(context, query))
                {
                    var mail = member.EmailAddress;
                    var empid = member.EmployeeId;
                    var desc = member.Description;
                    var dispname = member.DisplayName;
                    var distname = member.DistinguishedName;
                    var groups = member.GetGroups();
                    var givenname = member.GivenName;
                    var homedir = member.HomeDirectory;
                    var homedrive = member.HomeDrive;
                    var name = member.Name;
                    var middleName = member.MiddleName;
                    var surname =member.Surname;
                    var userprincname = member.UserPrincipalName;
                    var phonenr = member.VoiceTelephoneNumber;
                }
            }

            return result;
        }

        public async Task<List<string>>  FindGroupsForMember(string shortname)
        {
            return await Task.Run(() =>
            {
                var result = new List<string>();

                using (var context = new PrincipalContext(ContextType.Domain, _domain))
                {
                    using (UserPrincipal member = UserPrincipal.FindByIdentity(context, shortname))
                    {
                        if (member == null) return result;
                        PrincipalSearchResult<Principal> groups = member.GetGroups();

                        result.AddRange(groups.Select(grp => grp.Name));
                    }
                }
                return result;
            });
        } 

        private async Task<List<Employee>> SearchAd(string type, string searchString)
        {
            DirectoryEntry entry = new DirectoryEntry(_adUrl);
            entry.Username = _adUserName;
            entry.Password = _adPassword;
            DirectorySearcher search = new DirectorySearcher(entry);
            search.SearchScope = SearchScope.Subtree;
            search.SizeLimit = 500;
            search.Filter = @"(&(objectClass=user)(objectCategory=person)(" + type + "=" + searchString + "))";
            search.PropertiesToLoad.Add("EmployeeId");
            search.PropertiesToLoad.Add("givenName");
            search.PropertiesToLoad.Add("sn");
            search.PropertiesToLoad.Add("displayName");
            search.PropertiesToLoad.Add("userprincipalname");
            search.PropertiesToLoad.Add("sAMAccountName");
            search.PropertiesToLoad.Add("department");
            search.PropertiesToLoad.Add("physicaldeliveryofficename");
            search.PropertiesToLoad.Add("mobile");
            search.PropertiesToLoad.Add("fax");
            search.PropertiesToLoad.Add("telephonenumber");
            search.PropertiesToLoad.Add("othertelephone");
            search.PropertiesToLoad.Add("title");

            return await Task.Run(() =>
            {
                SearchResultCollection results = search.FindAll();

                var employees = new List<Employee>();
                foreach (SearchResult result in results)
                {
                    if (result.Properties["EmployeeId"].Count != 0)
                    {
                        var employee = new Employee();
                        employee.EmployeeId = (result.Properties["employeeId"].Count == 0) ? "" : (string)result.Properties["employeeId"][0];
                        employee.FirstName = (result.Properties["givenName"].Count == 0) ? "" : (string)result.Properties["givenName"][0];
                        employee.LastName = (result.Properties["sn"].Count == 0) ? "" : (string)result.Properties["sn"][0];
                        employee.DisplayName = (result.Properties["DisplayName"].Count == 0) ? "" : (string)result.Properties["DisplayName"][0];
                        employee.ShortName = (result.Properties["samaccountname"].Count == 0) ? "" : (string)result.Properties["samaccountname"][0];
                        employee.Email = (result.Properties["userprincipalname"].Count == 0) ? "" : (string)result.Properties["userprincipalname"][0];
                        employee.Location = (result.Properties["physicaldeliveryofficename"].Count == 0) ? "" : (string)result.Properties["physicaldeliveryofficename"][0];
                        employee.OfficePhone = (result.Properties["telephonenumber"].Count == 0) ? "" : (string)result.Properties["telephonenumber"][0];
                        employee.Fax = (result.Properties["othertelephone"].Count == 0) ? "" : (string)result.Properties["othertelephone"][0];
                        employee.Mobile = (result.Properties["mobile"].Count == 0) ? "" : (string)result.Properties["mobile"][0];
                        employee.Title = (result.Properties["title"].Count == 0) ? "" : (string)result.Properties["title"][0];
                        employee.Department = (result.Properties["department"].Count == 0) ? "" : (string)result.Properties["department"][0];
                        employees.Add(employee);
                    }
                }
                return employees.Count == 0 ? new List<Employee>() : employees;
            });
        }
    }
}
