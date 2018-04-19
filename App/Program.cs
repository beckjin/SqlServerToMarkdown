using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;

namespace App
{
    class Program
    {
        const string ConnString = @"Database=MDLog_New;Server=172.17.30.108;User ID=sa;Password=mingdao!@#123;Pooling=true;Max Pool Size=32767;Min Pool Size=0;";
        const string FileName = "MDLog.md";

        static void Main(string[] args)
        {
            var path = AppDomain.CurrentDomain.BaseDirectory + FileName;
            File.Delete(path);

            var tables = ExecuteDataTable("select * from INFORMATION_SCHEMA.TABLES");
            var tableNames = new List<string>();
            foreach (DataRow tableName in tables.Rows)
            {
                tableNames.Add((string)tableName["TABLE_NAME"]);
                tableNames.Sort();
            }

            foreach (var tableName in tableNames)
            {
                var tableDesc = string.Empty;
                var filterTable = true;

                DataTable tableProperties = ExecuteDataTable("select name,value from sys.extended_properties where OBJECT_NAME(major_id)=@tablename and minor_id=0", new SqlParameter("tablename", tableName));
                if (tableProperties.Rows.Count > 0)
                {
                    foreach (DataRow item in tableProperties.Rows)
                    {
                        if (item["name"].ToString() == "desc")
                        {
                            tableDesc = item["value"].ToString();
                        }
                        else if (item["name"].ToString() == "effect")
                        {
                            filterTable = !Convert.ToBoolean(item["value"]);
                        }
                    }
                }

                if (filterTable)
                    continue;

                DataTable tableColumns = ExecuteDataTable("select * from INFORMATION_SCHEMA.COLUMNS where TABLE_NAME=@tablename", new SqlParameter("tablename", tableName));
                DataTable tableColumnProperties = ExecuteDataTable("select objname as name, value from fn_listextendedproperty (NULL, 'user', 'dbo', 'table', @tablename, 'column', default)", new SqlParameter("tablename", tableName));
                DataTable tablePKColumns = ExecuteDataTable("EXEC sp_pkeys @table_name=@tablename", new SqlParameter("tablename", tableName));

                var pkPropertyName = string.Empty;
                if (tablePKColumns.Rows.Count > 0)
                {
                    pkPropertyName = tablePKColumns.Rows[0]["COLUMN_NAME"].ToString();
                }

                var Rows = new List<string>();
                Rows.Add("### " + tableName + " " + tableDesc);
                Rows.Add("");
                Rows.Add("| 字段| 类型|主键|允许空|默认值|描述|");
                Rows.Add("|-----|-----|----|------|------|----|");

                foreach (DataRow column in tableColumns.Rows)
                {
                    var name = (string)column["COLUMN_NAME"];
                    var desc = string.Empty;
                    foreach (DataRow item in tableColumnProperties.Rows)
                    {
                        if (item["name"].ToString() == name)
                        {
                            desc = item["value"].ToString();
                            break;
                        }
                    }

                    var row = new MDTableRow();
                    row.ColumnName = name;
                    row.IsPrimaryKey = pkPropertyName == name;
                    row.DataType = column["DATA_TYPE"].ToString();
                    row.Desc = desc;
                    row.CharacterMaximumLength = column["CHARACTER_MAXIMUM_LENGTH"].ToString();
                    row.ColumnDefault = column["COLUMN_DEFAULT"].ToString();
                    row.IsNullable = column["IS_NULLABLE"].ToString();

                    Rows.Add(WriteTableRow(row));
                }

                Rows.Add("");
                Rows.Add("");
                File.AppendAllLines(path, Rows);
                Console.WriteLine(tableName + " 生成完成");
            }
            Console.WriteLine();
            Console.WriteLine("生成结束");
            Console.ReadKey();
        }

        public static string WriteTableRow(MDTableRow row)
        {
            var datatype = row.CharacterMaximumLength == "" || row.CharacterMaximumLength == "2147483647"
                ? row.DataType : row.DataType + "(" + row.CharacterMaximumLength + ")";

            var str = string.Format("|{0}|{1}|{2}|{3}|{4}|{5}|"
                , row.ColumnName
                , datatype
                , row.IsPrimaryKey ? "PK" : string.Empty
                , row.IsNullable == "NO" ? "false" : "true"
                , row.ColumnDefault
                , row.Desc
                );
            return str;
        }

        public static DataTable ExecuteDataTable(string cmdText, params SqlParameter[] parameters)
        {
            using (SqlConnection conn = new SqlConnection(ConnString))
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = cmdText;
                    cmd.Parameters.AddRange(parameters);
                    DataTable dt = new DataTable();
                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    class MDTableRow
    {
        public string ColumnName { get; set; }
        public string DataType { get; set; }
        public string Desc { get; set; }
        public string CharacterMaximumLength { get; set; }
        public string ColumnDefault { get; set; }
        public string IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
    }
}