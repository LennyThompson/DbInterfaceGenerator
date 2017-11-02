using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbInterfaceGenerator
{
    enum TypeModifier
    {
        boolean,
        none
    }

    class Schema
    {
        public string name { get; set; }

        public List<Table> tables { get; set; }
    }

    class Table
    {
        private Dictionary<String, Field> m_mapFields;

        internal Table()
        {
            m_mapFields = new Dictionary<string, Field>();
        }
        public string name { get; set; }
        public int id { get; set; }

        public List<Field> fields
        {
            get { return m_mapFields.Values.ToList(); }
        }

        public bool hasForeignKey
        {
            get { return fields.FirstOrDefault(field => field.isForiegnKey) != null; }
        }

        public bool hasListSelectFields
        {
            get { return fields.FirstOrDefault(field => field.isListSelect) != null; }
        }

        public bool hasPrimaryKey
        {
            get { return fields.FirstOrDefault(field => field.isPrimaryKey) != null; }
        }

        public Field findField(string strName)
        {
            return m_mapFields[strName];
        }

        internal void addField(Field fieldAdd)
        {
            if (m_mapFields.ContainsKey(fieldAdd.name))
            {
                m_mapFields[fieldAdd.name] = fieldAdd;
            }
            else
            {
                m_mapFields.Add(fieldAdd.name, fieldAdd);
            }
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
        public virtual string name { get; set; }
        public virtual int id { get; set; }
        public virtual string type { get; set; }
        public virtual bool isPrimaryKey { get; set; }
        public virtual bool isForiegnKey { get; set; }

        public virtual bool isListSelect { get { return false; } }

        public virtual ForeignKey foreignKey { get; set; }

        protected TypeModifier modifer { get; set; }
        protected String valueTransform { get; set; }
        internal virtual bool addAttribute(string strName, string strValue)
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
                    case "IsBoolean":
                        modifer = TypeModifier.boolean;
                        valueTransform = strValue;
                        break;
                    case "IsPrimaryKey":
                        isPrimaryKey = strValue == "1";
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

    class SelectListField : Field
    {
        protected Field m_field;
        protected List<KeyValuePair<String, String>> m_listAssociatedFields;
        public SelectListField(Field fieldFrom)
        {
            m_listAssociatedFields = new List<KeyValuePair<String, String>>();
            m_field = fieldFrom;
        }

        public override string name
        {
            get { return m_field.name; } set { m_field.name = value; }
        }
        public override int id
        {
            get { return m_field.id; }
            set { m_field.id = value; }
        }

        public override string type
        {
            get { return m_field.type; }
            set { m_field.type = value; }
        }

        public override bool isPrimaryKey
        {
            get { return m_field.isPrimaryKey; }
            set { m_field.isPrimaryKey = value; }
        }

        public override bool isForiegnKey
        {
            get { return m_field.isForiegnKey; }
            set { m_field.isForiegnKey = value; }
        }

        public override ForeignKey foreignKey
        {
            get { return m_field.foreignKey; }
            set { m_field.foreignKey = value; }
        }


        internal override bool addAttribute(string strName, string strValue)
        {
            m_listAssociatedFields.Add(new KeyValuePair<string, string>(strName, strValue));
            return true;
        }

        public override bool isListSelect { get { return true; } }

        public bool hasExtraFields { get { return m_listAssociatedFields.Count > 0; } }

        public KeyValuePair<String, String>[] extraFields
        {
            get { return m_listAssociatedFields.ToArray(); }
        }
    }


}
