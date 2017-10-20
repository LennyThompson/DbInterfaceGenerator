using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbInterfaceGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            LoadXmlSqlSchema loadSchema = new LoadXmlSqlSchema("C:\\Dev\\Temp\\cougar_tables_2.xml");

            Console.WriteLine("Schema found = " + loadSchema.schema.Count);

            loadSchema.generateTypescript("C:\\Dev\\Temp\\cougar_tables");
        }
    }
}
