using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Devarchive.Net.Routing.Map
{
    public static class DBHelper
    {
        public class RouteData
        {
            public RouteData()
            {
                Coordinates = new List<RouteDataPoint>();
            }

            public List<RouteDataPoint> Coordinates { get; set; }
            public double Distance { get; set; }
        }

        public class RouteDataPoint
        {
            public int Index { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
        }

        public static bool HasData()
        {
            var sql = "SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Routes]')";
            var result = false;
            var connectionString = ConfigurationManager.ConnectionStrings["RoutingConnectionString"].ConnectionString;
            using (var conn = new System.Data.SqlClient.SqlConnection(connectionString))
            {
                using (var cmd = new System.Data.SqlClient.SqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandType = System.Data.CommandType.Text;
                    cmd.CommandText = sql;
                    try
                    {
                        conn.Open();

                        using (var r = cmd.ExecuteReader())
                        {
                            try
                            {
                                while (r.Read())
                                {
                                    result = r.GetInt32(0) == 1;
                                    break;
                                }
                            }
                            finally
                            {
                                r.Close();
                            }
                        }
                    }
                    finally
                    {
                        conn.Close();
                    }
                }
            }
            return result;
        }

        public static void InsertDBData(Action<string> updateStatus)
        {
            var ion = "SET IDENTITY_INSERT [dbo].[Routes] OFF";
            var ioff = "SET IDENTITY_INSERT [dbo].[Routes] OFF";

            updateStatus("adding structure...");
            ExecuteSqlFile(GetFilePath("script_structure.sql"));

            updateStatus("inserting data...");
            var file = new FileInfo(GetFilePath("script_insert_data.sql"));
            using (var fileStream = file.OpenText())
            {
                var i = 0;
                var sb = new StringBuilder();
                //sb.AppendLine(ion);

                while (!fileStream.EndOfStream)
                {
                    i++;
                    sb.AppendLine(fileStream.ReadLine());
                    if (i % 1000 == 0)
                    {
                        //sb.AppendLine(ioff);
                        ExecuteSqlText(sb.ToString());
                        sb.Clear();
                        updateStatus(String.Format("inserted {0} rows of data", i));
                    }
                }
                //sb.AppendLine(ioff);
                ExecuteSqlText(sb.ToString());
                updateStatus("inserted all data...");
            }

            updateStatus("setting up spatial index...");
            ExecuteSqlFile(GetFilePath("script_setup_spatial_index.sql"));
        }

        private static string GetFilePath(string filename)
        {
            return Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), filename);
        }

        private static void ExecuteSqlText(string script)
        {
            var connectionString = ConfigurationManager.ConnectionStrings["RoutingConnectionString"].ConnectionString;
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var trans = conn.BeginTransaction();
                try
                {
                    var sb = new StringBuilder();
                    var subScript = sb.ToString();
                    using (var reader = new StringReader(script))
                    {
                        while (reader.Peek() != -1)
                        {
                            var line = reader.ReadLine();
                            if (line == "GO")
                            {
                                subScript = sb.ToString();
                                if (!String.IsNullOrEmpty(subScript))
                                {
                                    using (var cmd = new System.Data.SqlClient.SqlCommand())
                                    {
                                        cmd.Connection = conn;
                                        cmd.Transaction = trans;
                                        cmd.CommandType = System.Data.CommandType.Text;
                                        cmd.CommandText = subScript;
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                                sb.Clear();
                            }
                            else
                            {
                                sb.AppendLine(line);
                            }
                        }
                        subScript = sb.ToString();
                        if (!String.IsNullOrEmpty(subScript))
                        {
                            using (var cmd = new System.Data.SqlClient.SqlCommand())
                            {
                                cmd.Connection = conn;
                                cmd.Transaction = trans;
                                cmd.CommandType = System.Data.CommandType.Text;
                                cmd.CommandText = subScript;
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }

                    trans.Commit();
                }
                catch
                {
                    trans.Rollback();
                    throw;
                }
                finally
                {
                    conn.Close();
                }
            }
        }

        private static void ExecuteSqlFile(string filename)
        {
            var file = new FileInfo(filename);
            using (var fileStream = file.OpenText())
            {
                var script = fileStream.ReadToEnd();
                ExecuteSqlText(script);
            }
        }

        public static RouteData GetRouteData(double x1, double y1, double x2, double y2)
        {
            var result = new RouteData();
            var connectionString = ConfigurationManager.ConnectionStrings["RoutingConnectionString"].ConnectionString;
            using (var conn = new System.Data.SqlClient.SqlConnection(connectionString))
            {
                using (var cmd = new System.Data.SqlClient.SqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.CommandText = "GetFastestRoute";
                    try
                    {
                        conn.Open();


                        cmd.Parameters.AddWithValue("@FLat", x1);
                        cmd.Parameters.AddWithValue("@FLon", y1);
                        cmd.Parameters.AddWithValue("@TLat", x2);
                        cmd.Parameters.AddWithValue("@TLon", y2);

                        double distance = 0;
                        using (var r = cmd.ExecuteReader())
                        {
                            try
                            {
                                while (r.Read())
                                {
                                    result.Coordinates.Add(new RouteDataPoint
                                    {
                                        X = r.GetFloat(1),
                                        Y = r.GetFloat(0),
                                        Index = r.GetInt32(3),
                                    });
                                    distance = Convert.ToDouble(r.GetFloat(4));
                                }
                            }
                            finally
                            {
                                r.Close();
                            }
                        }
                        result.Distance = distance;
                    }
                    finally
                    {
                        conn.Close();
                    }
                }
            }
            return result;
        }
    }
}
