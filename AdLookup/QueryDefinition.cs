using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AdLookup
{
    class QueryDefinition
    {
        public Func<string, Task<List<Employee>>> DoSearch { get; set; }

        public Predicate<string> CanSearch { get; set; }

        public Func<List<Employee>, string> CreateLogMessage { get; set; }

        public string Query { get; set; }
    }
}
