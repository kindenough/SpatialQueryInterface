using System;
using System.Collections.Generic;
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
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/<controller>/5
        public string Get(string id)
        {
            string ret="";
            var res = new Res();
            res.success = true;
            res.wyid = "";
            res.message = "";
            try 
	        {	   
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
                    double distance = paramlist.Length<3 ? 0 : double.Parse(paramlist[2]);

                    using (var ctx = new GEODBEntities1())
                    {
                        string sql1 = string.Format("SELECT * FROM DLDYPL WHERE Shape.STIntersects(geometry::STGeomFromText(('POINT({0} {1})'),4490))=1", x, y);
                        string sql2 = string.Format("SELECT * FROM DLDYPL WHERE Shape.STIntersects(geometry::STGeomFromText(('POINT({0} {1})'),4490).STBuffer({2}))=1", x, y, distance / 111000);
                        string sql = string.Format("IF EXISTS ({0}) {1} ELSE {2}", sql1, sql1, sql2);
                        var dldypls = ctx.Database.SqlQuery<DLDYPL>(sql);
                        foreach (var item in dldypls)
                        {
                            res.wyid += item.WYID + ",";
                        }
                        if (res.wyid.Length>0)
                        {
                            res.wyid = res.wyid.Remove(res.wyid.Length - 1);
                        }
                        else
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
            AppLog.Info(id+"="+res.wyid);
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
        public string wyid;
        public string message;
    }
}