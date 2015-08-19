using System;
using System.Reflection;
using log4net;
using log4net.Config;
using System.IO;
using FastDFS.Client.Service;

namespace FastDFS.WEB
{
    public class Global : System.Web.HttpApplication
    {
        private static readonly ILog _logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        protected void Application_Start(object sender, EventArgs e)
        {
            try
            {
                XmlConfigurator.Configure(new FileInfo(Server.MapPath(@"~/Config/log4net.config")));
                FastDFSService.Start();

                if (null != _logger)
                    _logger.Info("应用程序启动！");
            }
            catch (Exception exc)
            {
                if (null != _logger)
                    _logger.Error(exc.Message);
            }
        }

        protected void Session_Start(object sender, EventArgs e)
        {

        }

        protected void Application_BeginRequest(object sender, EventArgs e)
        {

        }

        protected void Application_AuthenticateRequest(object sender, EventArgs e)
        {

        }

        protected void Application_Error(object sender, EventArgs e)
        {

        }

        protected void Session_End(object sender, EventArgs e)
        {

        }

        protected void Application_End(object sender, EventArgs e)
        {
            //软重启FastDFSService
            //FastDFSService.Reset();
            FastDFSService.Stop();
            if (null != _logger)
                _logger.Info("关闭应用程序！");
        }
    }
}