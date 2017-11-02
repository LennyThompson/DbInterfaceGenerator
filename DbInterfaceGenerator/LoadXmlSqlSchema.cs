using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace DbInterfaceGenerator
{
    class LoadXmlSqlSchema
    {

        private Dictionary<string, Schema> m_mapSchema;

        public Dictionary<string, Schema> schema { get { return m_mapSchema; } }

        public LoadXmlSqlSchema(string strFileName)
        {
            m_mapSchema = new Dictionary<string, Schema>();
            using (XmlTextReader xmlReader = new XmlTextReader(strFileName))
            {
                Table tableCurr = null;
                Field fieldCurr = null;
                while (xmlReader.Read())
                {
                    switch (xmlReader.NodeType)
                    {
                        case XmlNodeType.Element: // The node is an element.
                            switch (xmlReader.Name)
                            {
                                case "Tables":
                                    break;
                                case "Table":
                                    tableCurr = new Table();
                                    tableCurr.addAttribute("Name", xmlReader.GetAttribute("Name"));
                                    tableCurr.addAttribute("Id", xmlReader.GetAttribute("Id"));
                                    string strSchemaName = xmlReader.GetAttribute("Schema");
                                    Schema schemaCurr = null;
                                    if (strSchemaName != null)
                                    {
                                        strSchemaName = "default";
                                    }

                                    if (!m_mapSchema.ContainsKey(strSchemaName))
                                    {
                                        schemaCurr = new Schema();
                                        schemaCurr.name = strSchemaName;
                                        schemaCurr.tables = new List<Table>();
                                        m_mapSchema.Add(strSchemaName, schemaCurr);
                                    }
                                    else
                                    {
                                        schemaCurr = m_mapSchema[strSchemaName];
                                    }
                                    
                                    schemaCurr.tables.Add(tableCurr);
                                    break;
                                case "Column":
                                    fieldCurr = new Field();
                                    fieldCurr.addAttribute("Name", xmlReader.GetAttribute("Name"));
                                    fieldCurr.addAttribute("Id", xmlReader.GetAttribute("Id"));
                                    fieldCurr.addAttribute("Type", xmlReader.GetAttribute("Type"));
                                    fieldCurr.addAttribute("IsPrimaryKey", xmlReader.GetAttribute("IsPrimaryKey"));
                                    fieldCurr.addAttribute("IsSelectList", xmlReader.GetAttribute("IsSelectList"));
                                    fieldCurr.addAttribute("ColumnReferencesTableId", xmlReader.GetAttribute("ColumnReferencesTableId"));
                                    fieldCurr.addAttribute("ColumnReferencesTableColumnId", xmlReader.GetAttribute("ColumnReferencesTableColumnId"));
                                    tableCurr.addField(fieldCurr);
                                    break;
                                case "SelectList":
                                    fieldCurr = new SelectListField(fieldCurr);
                                    if (xmlReader.MoveToFirstAttribute())
                                    {
                                        do
                                        {
                                            fieldCurr.addAttribute(xmlReader.Name, xmlReader.Value);
                                        } while (xmlReader.MoveToNextAttribute());
                                    }
                                    tableCurr.addField(fieldCurr);
                                    break;
                                default:
                                    break;

                            }
                            break;
                        case XmlNodeType.Text: //Display the text in each element.
                            break;
                        case XmlNodeType.Attribute:
                            break;
                        case XmlNodeType.EndElement: //Display the end of the element.
                            switch (xmlReader.Name)
                            {
                                case "Tables":
                                    break;
                                case "Table":
                                    tableCurr = null;
                                    fieldCurr = null;
                                    break;
                                case "Column":
                                    fieldCurr = null;
                                    break;
                                default:
                                    break;
                            }
                            break;

                    }
                }
            }
        }

        public bool generateTypescript(string strFileTo)
        {
            schema.Values.ToList().ForEach(
                schema =>
                {
                    schema.tables.ForEach(
                        table =>
                        {
                            string strTableFile = Path.Combine(strFileTo, string.Format("{0}.ts", table.name));
                            using (TextWriter writeCode = new StreamWriter(strTableFile))
                            {
                                writeCode.WriteLine("import * as moment from \"moment\";");
                                writeCode.WriteLine("import * as lodash from \"lodash\";");
                                writeCode.WriteLine("import {SqlConnector} from \"./../SqlServerConnector\";");
                                writeCode.WriteLine("");
                                table.fields.Where(field => field.isForiegnKey)
                                    .Select(field => field.foreignKey.update(schema.tables)).ToList();
                                table.fields.Where(field => field.isForiegnKey)
                                    .GroupBy(field => field.foreignKey.tableReference.name)
                                    .Select(fields => fields.FirstOrDefault())
                                    .Select(field => 
                                        {
                                            writeCode.WriteLine
                                            (
                                                "import {{{0}, {1}}} from './{2}'",
                                                TypescriptTableWriter.getInterfaceName(field.foreignKey.tableReference),
                                                TypescriptTableWriter.getClassName(field.foreignKey.tableReference),
                                                field.foreignKey.tableReference.name
                                            );
                                            return true;
                                        }
                                    )
                                    .ToArray();
                                writeCode.WriteLine("");
                                TypescriptTableWriter tableWriter = new TypescriptTableWriter(table);
                                tableWriter.write(writeCode);
                            }
                        }
                    );
                }
            );
            return true;
        }

    }
}
