using System;
using System.CodeDom;
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

            buildGetQuery(writeCode, indent);

            // fill - single item, and multi item

            buildSingleItemFill(writeCode, indent);
            buildAllItemFill(writeCode, indent);

            // fillList - All/multiple items

            buildListFillers(writeCode, indent);

            if (m_tableWrite.hasForeignKey)
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

        private void buildListFillers(TextWriter writeCode, string indent)
        {
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
            if (m_tableWrite.hasForeignKey)
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
            if (m_tableWrite.hasListSelectFields)
            {
                m_tableWrite.fields.Where(field => field.isListSelect)
                    .Select(
                        fieldList =>
                        {
                            writeCode.WriteLine();
                            writeCode.WriteLine
                            (
                                "{0}static async fillListOf{1}(connector: SqlConnector, sqlPool: any, {2}): Promise<{3}[]>", indent,
                                toCamelCase(fieldList.name),
                                fieldAsParamDeclaration(fieldList),
                                getFillListReturnType(m_tableWrite)
                            );
                            writeCode.WriteLine("{0}{{", indent);
                            indent += INDENT_INCREMENT;
                            writeCode.WriteLine("{0}let query: string = {1}.getQueryListOf{2}({3});",
                                indent,
                                getClassName(m_tableWrite),
                                toCamelCase(fieldList.name),
                                fieldAsParamUsage(fieldList)
                            );

                            writeCode.WriteLine("{0}let results = await connector.query(sqlPool, query);", indent);
                            writeCode.WriteLine("{0}return lodash(results.recordset)", indent);
                            indent += INDENT_INCREMENT;
                            writeCode.WriteLine("{0}.map(", indent);
                            indent += INDENT_INCREMENT;
                            if (m_tableWrite.hasForeignKey)
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
                            return true;
                        }
                    )
                    .ToList();
            }
        }

        private string fieldAsParamDeclaration(Field fieldFor)
        {
            Func<Field, String> fnBuildParam = field => String.Format("{0}: {1}", field.name, toTypescriptType(field.type));
            Func<Field, String> fnBuildListParam = field => String.Format("list_{0}: {1}[]", field.name, toTypescriptType(field.type));
            if (fieldFor.isListSelect)
            {
                SelectListField selectField = (SelectListField) fieldFor;
                string strReturn = fnBuildListParam(fieldFor);
                strReturn += String.Join("", selectField.extraFields
                        .Select
                        (
                            pair => ", " +  (pair.Value == "single" ? 
                                    fnBuildParam(m_tableWrite.findField(pair.Key)) : fnBuildListParam(m_tableWrite.findField(pair.Key)))
                        )
                    );
                return strReturn;
            }
            else
            {
                return fnBuildParam(fieldFor);
            }
        }

        private string fieldAsParamUsage(Field fieldFor)
        {
            if (fieldFor.isListSelect)
            {
                SelectListField selectField = (SelectListField)fieldFor;
                string strReturn = String.Format("list_{0}", selectField.name);
                strReturn += String.Join("", selectField.extraFields
                    .Select
                    (
                        pair => ", " + (pair.Value == "single" ?
                                    pair.Key : String.Format("list_{0}", pair.Key))
                    )
                );
                return strReturn;
            }
            else
            {
                return fieldFor.name;
            }
        }

        private void buildSingleItemFill(TextWriter writeCode, string indent)
        {
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
            if (m_tableWrite.hasForeignKey)
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
        }

        private void buildAllItemFill(TextWriter writeCode, string indent)
        {
            writeCode.WriteLine();
            writeCode.WriteLine("{0}static async fillAll(connector: SqlConnector, sqlPool: any, {1}): Promise<{2}>",
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
            if (m_tableWrite.hasForeignKey)
            {
                writeCode.WriteLine("{0}let newObj: {1} = lodash(results.recordset)", indent, getClassName(m_tableWrite));
                indent += INDENT_INCREMENT;
                writeCode.WriteLine("{0}.map((record: {1}) =>", indent, getInterfaceName(m_tableWrite));
                indent += INDENT_INCREMENT;
                writeCode.WriteLine("{0}{{", indent);
                indent += INDENT_INCREMENT;
                writeCode.WriteLine("{0}let newObj: {1} = new {1}(record);", indent, getClassName(m_tableWrite));
                writeCode.WriteLine("{0}await {1}.fillChildren(connector, sqlPool, newObj);", indent,
                    getClassName(m_tableWrite));
                writeCode.WriteLine("{0}return newObj;", indent);
                indent = indent.Remove(0, 4);
                writeCode.WriteLine("{0}}}", indent);
                indent = indent.Remove(0, 4);
                writeCode.WriteLine("{0})", indent);
                writeCode.WriteLine("{0}.value()", indent);
                indent = indent.Remove(0, 4);
            }
            else
            {
                writeCode.WriteLine("{0}return lodash(results.recordset)", indent);
                indent += INDENT_INCREMENT;
                writeCode.WriteLine("{0}.map((record: {1}) => new {2}Impl(record))", indent, getInterfaceName(m_tableWrite), getClassName(m_tableWrite));
                writeCode.WriteLine("{0}.value();", indent);
                indent = indent.Remove(0, 4);
            }

            indent = indent.Remove(0, 4);
            writeCode.WriteLine("{0}}}", indent);
        }

        private void buildGetQuery(TextWriter writeCode, string indent)
        {
            writeCode.WriteLine
            (
                "{0}static getQuery({1}): string", indent,
                string.Join(",", m_tableWrite.fields.Where(field => field.isPrimaryKey)
                    .Select(field => string.Format("{0}?: {1}", field.name, toTypescriptType(field.type))).ToArray())
            );
            writeCode.WriteLine("{0}{{", indent);
            indent += INDENT_INCREMENT;
            writeCode.WriteLine("{0}const template : any = lodash.template(`SELECT ", indent);
            indent += INDENT_INCREMENT;
            writeCode.WriteLine("{0}{1}", indent, string.Join(",", m_tableWrite.fields.Select(field => field.name).ToArray()));
            indent = indent.Remove(0, 4);
            if (m_tableWrite.hasPrimaryKey)
            {
                writeCode.WriteLine("{0}FROM {1}", indent, m_tableWrite.name);
                indent += INDENT_INCREMENT;
                writeCode.WriteLine("{0}<%if({1}){{%>WHERE {2}<%}}%>`);", indent,
                    string.Join(" || ", m_tableWrite.fields.Where(field => field.isPrimaryKey)
                        .Select(field => field.name).ToArray()),
                    string.Join(" ", m_tableWrite.fields.Where(field => field.isPrimaryKey)
                        .Select(field => getFieldLodashWhereClause(field)).ToArray())
                );
                indent = indent.Remove(0, 4);
            }
            else
            {
                writeCode.WriteLine("{0}FROM {1}`);", indent, m_tableWrite.name);
            }
            writeCode.WriteLine("{0}return template({{ {1}{2} }});", indent, 
                string.Join(", ", m_tableWrite.fields.Where(field => field.isPrimaryKey)
                .Select(field => string.Format("{0} : {1}", field.name, fieldAsTemplateMemberValue(field))).ToArray()),
                m_tableWrite.hasPrimaryKey ? ", join: ''" : "");
            indent = indent.Remove(0, 4);
            writeCode.WriteLine("{0}}}", indent);

            if (m_tableWrite.hasListSelectFields)
            {
                m_tableWrite.fields.Where(field => field.isListSelect)
                    .Select(
                        fieldList =>
                        {
                            writeCode.WriteLine
                            (
                                "{0}static getQueryListOf{1}({2}): string", indent,
                                    toCamelCase(fieldList.name),
                                    fieldAsParamDeclaration(fieldList)
                            );
                            writeCode.WriteLine("{0}{{", indent);
                            indent += INDENT_INCREMENT;
                            writeCode.WriteLine("{0}const template : any = lodash.template(`SELECT ", indent);
                            indent += INDENT_INCREMENT;
                            writeCode.WriteLine("{0}{1}", indent,
                                string.Join(",", m_tableWrite.fields.Select(field => field.name).ToArray()));
                            indent = indent.Remove(0, 4);
                            writeCode.WriteLine("{0}FROM {1}", indent, m_tableWrite.name);
                            indent += INDENT_INCREMENT;
                            writeCode.WriteLine("{0}WHERE {1}`);", indent,
                                fieldAsWhereClause(fieldList)
                            );
                            indent = indent.Remove(0, 4);
                            writeCode.WriteLine("{0}return template({{ {1} }});", indent,
                                fieldAsTemplateMember(fieldList));
                            indent = indent.Remove(0, 4);
                            writeCode.WriteLine("{0}}}", indent);
                            return true;
                        }
                    )   
                    .ToList();
            }
        }

        private string fieldAsTemplateMember(Field fieldFor)
        {
            Func<Field, String> fnListMember =
                field =>
                {
                    switch (toTypescriptType(field.type))
                    {
                        case "string":
                            return string.Format("list_{0}: lodash(list_{0}).map(item => \"'\" + item + \"'\").value()",
                            field.name);
                        default:
                            return string.Format("list_{0}: list_{0}", field.name);
                    }
                };
            Func<Field, String> fnSingleMember = field => string.Format("{0}: {1}", field.name, fieldAsTemplateMemberValue(field));

            if (fieldFor.isListSelect)
            {
                SelectListField selectField = (SelectListField)fieldFor;
                string strReturn = fnListMember(fieldFor);
                strReturn += string.Join
                (
                    "",
                    selectField.extraFields.Select(
                        pair => ", " + (pair.Value == "single"
                            ? fnSingleMember(m_tableWrite.findField(pair.Key))
                            : fnListMember(m_tableWrite.findField(pair.Key)))
                    )
                );
                return strReturn;
            }
            else
            {
                return fnSingleMember(fieldFor);
            }
        }

        private string fieldAsWhereClause(Field fieldFor)
        {
            Func<Field, String> fnListWhere =
                field => string.Format("{0} in (<%= _(list_{0}).join(',')%>)", field.name);
            Func<Field, String> fnSingleWhere = field => fieldAsWhereSelect(field);

            if (fieldFor.isListSelect)
            {
                SelectListField selectField = (SelectListField)fieldFor;
                string strReturn = fnListWhere(selectField);
                strReturn += String.Join("", selectField.extraFields
                    .Select
                    (
                        pair => " AND " + (pair.Value == "single" ?
                                    fnSingleWhere(m_tableWrite.findField(pair.Key)) : fnListWhere(m_tableWrite.findField(pair.Key)))
                    )
                );
                return strReturn;
            }
            else
            {
                return fnSingleWhere(fieldFor);
            }
        }

        private string fieldAsTemplateMemberValue(Field field)
        {
            switch (toTypescriptType((field.type)))
            {
                case "Date":
                    return String.Format("moment({0}).format('YYYY-MM-DD 00:00:00.000')", field.name);
                default:
                    return field.name;
            };
        }

        private string fieldAsWhereSelect(Field fieldFor)
        {
            return string.Format("{0} = {1}", fieldFor.name, fieldAsTemplateValue(fieldFor));
        }

        private string fieldAsTemplateValue(Field fieldFor)
        {
            switch (toTypescriptType(fieldFor.type))
            {
                case "string":
                case "Date":
                    return string.Format("'<%={0}%>'", fieldFor.name);
                default:
                    return string.Format("<%={0}%>", fieldFor.name);
            }
        }

        private string fieldAsLiteralTemplateValue(Field fieldFor)
        {
            switch (toTypescriptType(fieldFor.type))
            {
                case "string":
                    return string.Format("'{0}'", fieldFor.name);
                default:
                    return fieldFor.name;
            }
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
            if (tableFor.hasForeignKey)
            {
                return string.Format("Promise<{0}>", getInterfaceName(tableFor));
            }
            else
            {
                return getInterfaceName(tableFor);
            }
        }

        string getFieldLodashWhereClause(Field fieldFor)
        {
            return string.Format("<%if({0}){{%><%=join%> {1}<% join = 'AND'; }}%>", fieldFor.name, fieldAsWhereSelect(fieldFor));
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
