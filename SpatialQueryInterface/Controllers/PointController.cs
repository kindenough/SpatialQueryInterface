using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.OracleClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using log4net;
using Newtonsoft.Json;

//1.当点在面内
//2.不在面内时创建缓冲区10米或者指定的距离，求出相交面的wyid，没有则返回空字符
namespace SpatialQueryInterface.Controllers
{
    public class PointController : ApiController
    {
        // GET api/<controller>
        //public IEnumerable<string> Get()
        public string Get()
        {
            //return new string[] { "value1", "value2" };
            string logpath = System.Web.HttpContext.Current.Server.MapPath("~\\log\\logfile.log");
            string newValue = File.ReadAllText(logpath);
            return newValue;
        }

        public string Get(string id)
        {
            string DataBase = System.Configuration.ConfigurationManager.AppSettings["DataBase"];
            if (DataBase=="mssql")
            {
                return GetFromMssql(id);
            }
            else if(DataBase=="oracle")
            {
                return GetFromOracle(id);
            }

            return "配置文件DataBase值必须为mssql或者oracle";
        }

        // GET api/<controller>/5
        //MSSQL 2008 R2
        private string GetFromMssql(string id)
        {
            string ret="";
            var res = new Res();
            res.success = true;
            res.message = "";
            try 
            {
                string BufferDistance = System.Configuration.ConfigurationManager.AppSettings["BufferDistance"];
                double dis = double.Parse(BufferDistance);
                var paramlist = id.Split(',');
                if (paramlist.Length < 2)
                {   
                    res.success = false;
                    res.message = id + " 参数错误！";
                }
                else
                {
                    double x = double.Parse(paramlist[0]);
                    double y = double.Parse(paramlist[1]);
                    double distance = paramlist.Length < 3 ? dis : double.Parse(paramlist[2]);

                    using (var ctx = new GEODBEntities1())
                    {
                        string sql1 = string.Format("SELECT * FROM DLDYPL WHERE Shape.STIntersects(geometry::STGeomFromText(('POINT({0} {1})'),4490))=1", x, y);
                        string sql2 = string.Format("SELECT * FROM DLDYPL WHERE Shape.STIntersects(geometry::STGeomFromText(('POINT({0} {1})'),4490).STBuffer({2}))=1", x, y, distance / 111000);
                        string sql = string.Format("IF EXISTS ({0}) {1} ELSE {2}", sql1, sql1, sql2);
                        var dldypls = ctx.Database.SqlQuery<DLDYPL>(sql);

                        foreach (var item in dldypls)
                        {
                            RetInfo ri = new RetInfo();
                            ri.wyid = item.WYID;
                            ri.name = item.NAME;
                            ri.dldymc = item.DLDYMC;
                            res.infos.Add(ri);
                        }
                        res.total = res.infos.Count;
                        if (res.infos.Count == 0)
                        {
                            res.message = "查询为空";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                res.success = false;
                res.message = "出错了！" + ex.Message;
                AppLog.Error(id+"，"+ex.Message);
            }

            ret = JsonConvert.SerializeObject(res);

            string LogInfo = System.Configuration.ConfigurationManager.AppSettings["LogInfo"];

            if (LogInfo=="1")
            {
                AppLog.Info(id + "=" + ret);
            }

            return ret;
        }

        //Oracle 11g sde/sde
        private string GetFromOracle(string id)
        {
            string ret = "";
            var res = new Res();
            res.success = true;
            res.message = "";
            string ConnectionString="Data Source=orcl;user=sde;password=sde;";//写连接串

            try
            {
                string BufferDistance = System.Configuration.ConfigurationManager.AppSettings["BufferDistance"];
                string MaxBufferDistance = System.Configuration.ConfigurationManager.AppSettings["MaxBufferDistance"];
                string StepBufferDistance = System.Configuration.ConfigurationManager.AppSettings["StepBufferDistance"];
                double dis = double.Parse(BufferDistance);
                double maxdis = double.Parse(MaxBufferDistance);
                double stepdis = double.Parse(StepBufferDistance);
                var paramlist = id.Split(',');
                if (paramlist.Length < 2)
                {
                    res.success = false;
                    res.message = id + " 参数错误！";
                }
                else
                {
                    double x = double.Parse(paramlist[0]);
                    double y = double.Parse(paramlist[1]);
                    double distance = paramlist.Length < 3 ? dis : double.Parse(paramlist[2]);

                    //select t.wyid,t.name from dldypl t where sde.st_intersects(t.shape,sde.st_buffer(sde.st_point(118.089570,  24.491467,4490),0.00119))=1
                    string sql = string.Format("select t.wyid,t.name,t.DLDYMC from dldypl t where sde.st_intersects(t.shape,sde.st_buffer(sde.st_point({0},{1},4490),{2}))=1", x, y, 0);
                    OracleDataReader thisReader = OracleHelper.ExecuteReader(ConnectionString, System.Data.CommandType.Text, sql, null);
                    while (thisReader.Read())
                    {
                        RetInfo info = new RetInfo();
                        info.wyid = thisReader["wyid"].ToString();
                        info.name = thisReader["name"].ToString();
                        info.dldymc = thisReader["dldymc"].ToString();
                        res.infos.Add(info);
                        res.message = string.Format("点在道路上");
                    }

                    while (res.infos.Count == 0 && distance < maxdis)
                    {
                        sql = string.Format("select t.wyid,t.name,t.DLDYMC from dldypl t where sde.st_intersects(t.shape,sde.st_buffer(sde.st_point({0},{1},4490),{2}))=1", x, y, distance / 111000);//distance);
                        thisReader = OracleHelper.ExecuteReader(ConnectionString, System.Data.CommandType.Text, sql, null);
                        while (thisReader.Read())
                        {
                            RetInfo info = new RetInfo();
                            info.wyid = thisReader["wyid"].ToString();
                            info.name = thisReader["name"].ToString();
                            info.dldymc = thisReader["dldymc"].ToString();
                            res.infos.Add(info);
                        }
                        res.message = string.Format("缓冲距离={0}米", distance);
                        distance += stepdis;
                    }
                    res.total = res.infos.Count;
                }
                AppLog.Info(id + "=" + res.infos.Count);
            }
            catch (Exception ex)
            {
                res.success = false;
                res.message = "出错了！" + ex.Message;
                AppLog.Error(id + "，" + ex.Message);
            }

            ret = JsonConvert.SerializeObject(res);
            return ret;
        }

        // POST api/<controller>
        public string Post([FromBody]string value)
        {
            return value;
        }

        // PUT api/<controller>/5
        public string Put(int id, [FromBody]string value)
        {
            return value;
        }

        // DELETE api/<controller>/5
        public string Delete(int id)
        {
            return id.ToString();
        }
    }

    public class Res
    {
        public bool success;
        public string message;
        public List<RetInfo> infos = new List<RetInfo>();
        public int total = 0;
    }

    public class RetInfo
    {
        public string name;
        public string wyid;
        public string dldymc;
    }
}