using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbInterfaceGenerator
{
    class Schema
    {
        public string name { get; set; }

        public List<Table> tables { get; set; }
    }

    class Table
    {
        public string name { get; set; }
        public int id { get; set; }
        public List<Field> fields { get; set; }

        public bool hasForeignKey()
        {
            return fields.FirstOrDefault(field => field.isForiegnKey) != null;
        }

        internal bool addAttribute(string strName, string strValue)
        {
            if (strValue != null)
            {
                switch (strName)
                {
                    case "Name":
                        name = strValue;
                        break;
                    case "Id":
                        id = Int32.Parse(strValue);
                        break;
                    default:
                        return false;
                }
                return true;
            }
            return false;
        }
    }

    class ForeignKey
    {
        public Field field { get; set; }
        public int tableReferenceId { get; set; }
        public int fieldReferenceId { get; set; }

        public Table tableReference { get; set; }

        public bool update(List<Table> listTables)
        {
            tableReference = listTables.FirstOrDefault(table => table.id == tableReferenceId);
            return tableReference != null;
        }

        public string outputName
        {
            get
            {
                String strOutput = field.name.IndexOf("_id") > 0
                    ? field.name.Substring(0, field.name.Length - 3)
                    : field.name;
                return TypescriptTableWriter.toCamelCase(strOutput);
            }
        }
    }

    class Field
    {
        public string name { get; set; }
        public int id { get; set; }
        public string type { get; set; }
        public bool isPrimaryKey { get; set; }
        public bool isForiegnKey { get; set; }

        public bool isListSelect { get; set; }

        public ForeignKey foreignKey { get; set; }

        internal bool addAttribute(string strName, string strValue)
        {
            if (strValue != null)
            {
                switch (strName)
                {
                    case "Name":
                        name = strValue;
                        break;
                    case "Id":
                        id = Int32.Parse(strValue);
                        break;
                    case "Type":
                        type = strValue;
                        break;
                    case "IsPrimaryKey":
                        isPrimaryKey = strValue == "1";
                        break;
                    case "IsSelectList":
                        isListSelect = strValue == "true";
                        break;
                    case "ColumnReferencesTableId":
                        addForiegnKey();
                        foreignKey.tableReferenceId = Int32.Parse(strValue);
                        break;
                    case "ColumnReferencesTableColumnId":
                        addForiegnKey();
                        foreignKey.fieldReferenceId = Int32.Parse(strValue);
                        break;
                    default:
                        return false;
                }
                return true;
            }
            return false;
        }

        private void addForiegnKey()
        {
            isForiegnKey = true;
            if (foreignKey == null)
            {
                foreignKey = new ForeignKey();
                foreignKey.field = this;
            }
        }
    }
}
