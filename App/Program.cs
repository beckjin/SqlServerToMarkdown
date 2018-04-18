using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;

namespace App
{
    class Program
    {
        const string ConnString = @"Database=MDProject;Server=172.17.30.108;User ID=sa;Password=mingdao!@#123;Pooling=true;Max Pool Size=32767;Min Pool Size=0;";
        const string FileName = "MDProject.md";

        static void Main(string[] args)
        {
            var path = AppDomain.CurrentDomain.BaseDirectory + FileName;
            File.Delete(path);

            var tables = ExecuteDataTable("select * from INFORMATION_SCHEMA.TABLES");
            foreach (DataRow tableName in tables.Rows)
            {
                var tablename = (string)tableName["TABLE_NAME"];

                var Rows = new List<string>();
                Rows.Add("### " + tablename);
                Rows.Add("");
                Rows.Add("| 字段| 数据类型|是否为主键|是否允许为NULL|默认值|描述|");
                Rows.Add("|-----|---------|----------|--------------|------|----|");

                DataTable tableColumns = ExecuteDataTable("select * from INFORMATION_SCHEMA.COLUMNS where TABLE_NAME=@tablename", new SqlParameter("tablename", tablename));
                DataTable tableColumnNames = ExecuteDataTable("select b.name,a.value from(SELECT minor_id, value FROM sys.extended_properties WHERE major_id = OBJECT_ID(@tablename)) a ,(SELECT name, column_id FROM sys.columns where object_id = OBJECT_ID(@tablename) ) b where a.minor_id = b.column_id", new SqlParameter("tablename", tablename));

                foreach (DataRow column in tableColumns.Rows)
                {
                    var name = (string)column["COLUMN_NAME"];
                    var desc = string.Empty;
                    foreach (DataRow item in tableColumnNames.Rows)
                    {
                        if (item["name"].ToString() == name)
                        {
                            desc = item["value"].ToString();
                            break;
                        }
                    }

                    var mdtablerow = new MDTableRow();
                    mdtablerow.ColumnName = name;
                    mdtablerow.DataType = column["DATA_TYPE"].ToString();
                    mdtablerow.Desc = desc;
                    mdtablerow.CharacterMaximumLength = column["CHARACTER_MAXIMUM_LENGTH"].ToString();
                    mdtablerow.ColumnDefault = column["COLUMN_DEFAULT"].ToString();
                    mdtablerow.IsNullable = column["IS_NULLABLE"].ToString();

                    Rows.Add(WriteTableRow(mdtablerow));
                }

                Rows.Add("");
                Rows.Add("");
                File.AppendAllLines(path, Rows);
                Console.WriteLine(tablename + " 生成完成");
            }
            Console.WriteLine();
            Console.WriteLine("生成结束");
            Console.ReadKey();
        }

        public static string WriteTableRow(MDTableRow mdtablerow)
        {
            var datatype = mdtablerow.CharacterMaximumLength == "" || mdtablerow.CharacterMaximumLength == "2147483647"
                ? mdtablerow.DataType : mdtablerow.DataType + "(" + mdtablerow.CharacterMaximumLength + ")";

            var str = string.Format("|{0}|{1}|{2}|{3}|{4}|{5}|"
                , mdtablerow.ColumnName
                , datatype
                , mdtablerow.ColumnName == "ID" ? "PK" : string.Empty
                , mdtablerow.IsNullable == "NO" ? "false" : "true"
                , mdtablerow.ColumnDefault
                , mdtablerow.Desc
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