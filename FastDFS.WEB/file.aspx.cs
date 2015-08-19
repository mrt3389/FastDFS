using System;
using FastDFS.Client.Service;

namespace FastDFS.WEB
{
    public partial class file : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {

        }

        protected void Button1_Click(object sender, EventArgs e)
        {
            //string filePath = FastDFSClient.Upload("test", FileUpload1.FileBytes,"jpg");
            string filePath = FastDFSClient.Upload(FileUpload1.FileBytes, "jpg");
            Response.Write(filePath);
        }
    }
}