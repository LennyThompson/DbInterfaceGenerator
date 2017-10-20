using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbInterfaceGenerator
{

    class TypescriptTableWriter
    {
        private Table m_tableWrite;
        private static string INDENT_INCREMENT = "    ";

        internal TypescriptTableWriter(Table tableWrite)
        {
            m_tableWrite = tableWrite;
        }

        internal bool write(TextWriter writeCode)
        {
            writeCode.WriteLine();
            string indent = "";

            // Declare interface

            writeCode.WriteLine("export interface {0}", getInterfaceName(m_tableWrite));
            writeCode.WriteLine("{");
            indent += INDENT_INCREMENT;

            // fields

            m_tableWrite.fields.ForEach(
                field => writeCode.WriteLine("{0}{1}: {2};", indent, field.name, toTypescriptType(field.type))
            );
            m_tableWrite.fields.Where(field => field.isForiegnKey).Select(
                field =>
                {
                    writeCode.WriteLine("{0}{1}: {2};", indent, field.foreignKey.outputName, getInterfaceName(field.foreignKey.tableReference));
                    return true;
                }
            ).ToList();
            writeCode.WriteLine("}");
            indent = indent.Remove(0, 4);

            // Declare implementing class

            writeCode.WriteLine("export class {0} implements {1}", getClassName(m_tableWrite), getInterfaceName(m_tableWrite));
            writeCode.WriteLine("{");
            indent += INDENT_INCREMENT;

            // fields from interface

            m_tableWrite.fields.ForEach(
                field => writeCode.WriteLine("{0}{1}: {2};", indent, field.name, toTypescriptType(field.type))
            );
            m_tableWrite.fields.Where(field => field.isForiegnKey).Select(
                field =>
                {
                    writeCode.WriteLine("{0}{1}: {2};", indent, field.foreignKey.outputName, getInterfaceName(field.foreignKey.tableReference));
                    return true;
                }
            ).ToList();
            writeCode.WriteLine();

            // Constructor

            writeCode.WriteLine("{0}constructor(objFrom: {1})", indent, getInterfaceName(m_tableWrite));
            writeCode.WriteLine("{0}{{", indent);
            indent += INDENT_INCREMENT;
            m_tableWrite.fields.ForEach(
                field => writeCode.WriteLine("{0}this.{1} = objFrom.{1};", indent, field.name)
            );
            indent = indent.Remove(0, 4);
            writeCode.WriteLine("{0}}}", indent);

            writeCode.WriteLine();

            // getQuery - return select query from lodash template, based on primary keys

            writeCode.WriteLine
            (
                "{0}static getQuery({1}): string", indent,
                string.Join(",", m_tableWrite.fields.Where(field => field.isPrimaryKey)
                    .Select(field => string.Format("{0}?: {1}", field.name, toTypescriptType(field.type))).ToArray())
            );
            writeCode.WriteLine("{0}{{", indent);
            indent += INDENT_INCREMENT;
            writeCode.WriteLine("{0}let strJoin: string = ''", indent);
            writeCode.WriteLine("{0}const template : any = lodash.template(`SELECT ", indent);
            indent += INDENT_INCREMENT;
            writeCode.WriteLine("{0}{1}", indent, string.Join(",", m_tableWrite.fields.Select(field => field.name).ToArray()));
            indent = indent.Remove(0, 4);
            writeCode.WriteLine("{0}FROM {1}", indent, m_tableWrite.name);
            indent += INDENT_INCREMENT;
            writeCode.WriteLine("{0}<%if({1}){{%>WHERE {2}<%}}%>`);", indent,
                string.Join(" || ", m_tableWrite.fields.Where(field => field.isPrimaryKey)
                    .Select(field => field.name).ToArray()),
                string.Join(" ", m_tableWrite.fields.Where(field => field.isPrimaryKey)
                    .Select(field => getFieldLodashWhereClause(field)).ToArray())
            );
            indent = indent.Remove(0, 4);
            writeCode.WriteLine("{0}return template({{ {1} }})", indent, string.Join(", ", m_tableWrite.fields.Where(field => field.isPrimaryKey)
                .Select(field => string.Format("{0} : {0}", field.name)).ToArray()));
            indent = indent.Remove(0, 4);
            writeCode.WriteLine("{0}}}", indent);

            // fill - single item

            writeCode.WriteLine();
            writeCode.WriteLine("{0}static async fill(connector: SqlConnector, sqlPool: any, {1}): Promise<{2}>",
                indent,
                string.Join(", ", m_tableWrite.fields.Where(field => field.isPrimaryKey)
                    .Select(field => string.Format("{0}?: {1}", field.name, toTypescriptType(field.type))).ToArray()),
                getInterfaceName(m_tableWrite)
            );
            writeCode.WriteLine("{0}{{", indent);
            indent += INDENT_INCREMENT;
            writeCode.WriteLine("{0}let query: string = {1}.getQuery({2});",
                indent,
                getClassName(m_tableWrite),
                string.Join(", ", m_tableWrite.fields.Where(field => field.isPrimaryKey)
                    .Select(field => field.name).ToArray())
            );

            writeCode.WriteLine("{0}let results = await connector.query(sqlPool, query);", indent);
            if (m_tableWrite.hasForeignKey())
            {
                writeCode.WriteLine("{0}let newObj: {1} = new {1}(results.recordset[0]);", indent, getClassName(m_tableWrite));
                writeCode.WriteLine("{0}await {1}.fillChildren(connector, sqlPool, newObj);", indent,
                    getClassName(m_tableWrite));
                writeCode.WriteLine("{0}return newObj;", indent);
            }
            else
            {
                writeCode.WriteLine("{0}return new {1}(results.recordset[0]);", indent, getClassName(m_tableWrite));
            }

            indent = indent.Remove(0, 4);
            writeCode.WriteLine("{0}}}", indent);

            // fillList - All/multiple items

            writeCode.WriteLine();
            writeCode.WriteLine("{0}static async fillList(connector: SqlConnector, sqlPool: any): Promise<{1}[]>",
                indent,
                getFillListReturnType(m_tableWrite)
            );
            writeCode.WriteLine("{0}{{", indent);
            indent += INDENT_INCREMENT;
            writeCode.WriteLine("{0}let query: string = {1}.getQuery();",
                indent,
                getClassName(m_tableWrite)
            );

            writeCode.WriteLine("{0}let results = await connector.query(sqlPool, query);", indent);
            writeCode.WriteLine("{0}return lodash(results.recordset)", indent);
            indent += INDENT_INCREMENT;
            writeCode.WriteLine("{0}.map(", indent);
            indent += INDENT_INCREMENT;
            if (m_tableWrite.hasForeignKey())
            {
                writeCode.WriteLine("{0}async (record: {1}) =>", indent, getInterfaceName(m_tableWrite));
                writeCode.WriteLine("{0}{{", indent);
                indent += INDENT_INCREMENT;
                writeCode.WriteLine("{0}let newObj: {1} = new {1}(record);", indent, getClassName(m_tableWrite));
                writeCode.WriteLine("{0}await {1}.fillChildren(connector, sqlPool, newObj);", indent,
                    getClassName(m_tableWrite));
                writeCode.WriteLine("{0}return newObj;", indent);
                indent = indent.Remove(0, 4);
                writeCode.WriteLine("{0}}}", indent);
            }
            else
            {
                writeCode.WriteLine("{0}(record: {1}) => new {2}(record)", indent, getInterfaceName(m_tableWrite), getClassName(m_tableWrite));
            }
            indent = indent.Remove(0, 4);
            writeCode.WriteLine("{0})", indent);
            writeCode.WriteLine("{0}.value();", indent);
            indent = indent.Remove(0, 4);

            indent = indent.Remove(0, 4);
            writeCode.WriteLine("{0}}}", indent);

            if (m_tableWrite.hasForeignKey())
            {
                writeCode.WriteLine("{0}static async fillChildren(connector: SqlConnector, sqlPool: any, newObj: {1})",
                    indent, getClassName(m_tableWrite));
                writeCode.WriteLine("{0}{{", indent);
                indent += INDENT_INCREMENT;
                m_tableWrite.fields.Where(field => field.isForiegnKey)
                    .Select(field =>
                        {
                            writeCode.WriteLine(
                                "{0}newObj.{1} = await {2}.fill(connector, sqlPool, {3});",
                                indent,
                                field.foreignKey.outputName,
                                getClassName(field.foreignKey.tableReference),
                                string.Join(", ", field.foreignKey.tableReference.fields
                                    .Where(foreignField => foreignField.isPrimaryKey)
                                    .Select(foreignField => foreignField.id != field.foreignKey.fieldReferenceId
                                        ? "null"
                                        : string.Format("newObj.{0}", field.name)).ToArray()
                                )
                            );
                            return true;
                        }
                    )
                    .ToList();
                indent = indent.Remove(0, 4);
                writeCode.WriteLine("{0}}}", indent);
            }
            // End of impl class declaration

            indent = indent.Remove(0, 4);
            writeCode.WriteLine("}");

            return true;
        }

        public static string getInterfaceName(Table tableFor)
        {
            return toCamelCase(tableFor.name);
        }

        public static string getClassName(Table tableFor)
        {
            return getInterfaceName(tableFor) + "Impl";
        }

        static string getFillListReturnType(Table tableFor)
        {
            if (tableFor.hasForeignKey())
            {
                return string.Format("Promise<{0}>", getInterfaceName(tableFor));
            }
            else
            {
                return getInterfaceName(tableFor);
            }
        }

        static string getFieldLodashWhereClause(Field fieldFor)
        {
            string strWhereValue = string.Format("<%={0}%>", fieldFor.name);
            switch (toTypescriptType(fieldFor.type))
            {
                case "string":
                    strWhereValue = string.Format("'<%={0}%>'", fieldFor.name);
                    break;
                case "Date":
                    strWhereValue = string.Format("'<%=moment({0}).format('YYYY-MM-DD 00:00:00.000')%>'", fieldFor.name);
                    break;
            }
            return string.Format("<%if({0}){{%><%=strJoin%> {0} = {1}<% strJoin = 'ADD' }}%>", fieldFor.name, strWhereValue);
        }

        public static string toCamelCase(string strFrom)
        {
            bool bNextCapital = true;
            return string.Concat(strFrom.Select(ch =>
                    {
                        if (bNextCapital)
                        {
                            char chReturn = Char.ToUpper(ch);
                            bNextCapital = false;
                            return chReturn;
                        }
                        bNextCapital = ch == '_';
                        return ch;
                    }
                )
                .Where(ch => ch != '_')
                .ToList());
        }

        static string toTypescriptType(string strType)
        {
            switch (strType)
            {
                case "int":
                case "tinyint":
                case "smallint":
                case "float":
                case "real":
                case "decimal":
                case "numeric":
                    return "number";
                case "varchar":
                case "uniqueidentifier":
                case "nchar":
                    return "string";
                case "char":
                    return "string";
                case "datetime":
                case "timestamp":
                    return "Date";
                case "bit":
                case "varbinary":   // WTF?
                    return "boolean";
                case "image":
                    return "string"; // WTF?
                default:
                    return "***unknown type***" + strType;
            }
        }
    }
}
