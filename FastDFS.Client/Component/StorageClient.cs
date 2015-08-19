using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using FastDFS.Client.Core.Pool;
using FastDFS.Client.Service;
using log4net;

namespace FastDFS.Client.Component
{
    public class StorageClient
    {
        private static readonly ILog _logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static TcpConnection GetStorageConnection(string groupName)
        {
            StorageServerInfo storageServerInfo = TrackerClient.GetStoreStorage(groupName);
            IObjectPool<TcpConnection> pool = TcpConnectionPoolManager.GetPool(storageServerInfo.IpAddress, storageServerInfo.Port, false, true);
            try
            {
                TcpConnection storageConnection = pool.GetObject(storageServerInfo.IpAddress, storageServerInfo.Port);
                storageConnection.Index = storageServerInfo.StorePathIndex;
                if (null != _logger)
                    _logger.InfoFormat("Storage可用连接数为:{0}", pool.NumIdle);
                return storageConnection;
            }
            catch (Exception exc)
            {
                if (null != _logger)
                    _logger.WarnFormat("连接Storage服务器时发生异常,异常信息为:{0}", exc.Message);
                throw;
            }
        }

        /// <summary>
        /// 上传文件.
        /// </summary>
        /// <param name="groupName">Name of the group.</param>
        /// <param name="localFileName">Name of the local file.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="extension">The extension.</param>
        /// <param name="metadatas">The metadatas.</param>
        /// <returns></returns>
        protected static string[] DoUpload(string groupName, string localFileName, byte[] buffer,
                                                     string extension, NameValuePair[] metadatas)
        {
            bool isGoing = false;
            DateTime begin = DateTime.Now;
            do
            {
                TimeSpan span = DateTime.Now.Subtract(begin);
                if ((int)span.TotalSeconds > FastDFSService.NetworkTimeout / 1000) //超过网络延迟还未成功上传图片，即失败
                {
                    break;
                }
                TcpConnection storageConnection = GetStorageConnection(groupName);
                if (null != _logger)
                    _logger.InfoFormat("Storage服务器的IP是:{0}.端口为{1}", storageConnection.IpAddress, storageConnection.Port);
                long totalBytes = 0;
                try
                {
                    long length;
                    FileStream stream;

                    byte[] metadatasBuffer = metadatas == null
                                                 ? new byte[0]
                                                 : Encoding.GetEncoding(FastDFSService.Charset).GetBytes(
                                                       Util.PackMetadata(metadatas));
                    byte[] contentBuffer = buffer;
                    //byte[] bufferSize = new byte[1 + 2 * Protocol.TRACKER_PROTO_PKG_LEN_SIZE];
                    byte storePathIndex = (byte)storageConnection.Index;
                    if (!string.IsNullOrEmpty(localFileName))
                    {
                        FileInfo fileInfo = new FileInfo(localFileName);
                        length = fileInfo.Exists ? fileInfo.Length : 0;
                        stream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read);
                    }
                    else
                    {
                        length = buffer.Length;
                        stream = null;
                    }
                    #region 拷贝后缀扩展名值
                    byte[] extensionBuffer = new byte[Protocol.FDFS_FILE_EXT_NAME_MAX_LEN];
                    Util.InitializeBuffer(extensionBuffer, 0);
                    if (!string.IsNullOrEmpty(extension))
                    {
                        byte[] bs = Util.StringToByte(extension);
                        int ext_name_len = bs.Length;
                        if (ext_name_len > Protocol.FDFS_FILE_EXT_NAME_MAX_LEN)
                            ext_name_len = Protocol.FDFS_FILE_EXT_NAME_MAX_LEN;
                        Array.Copy(bs, 0, extensionBuffer, 0, ext_name_len);
                    }
                    #endregion
                    length = 1 + Protocol.FDFS_PROTO_PKG_LEN_SIZE + Protocol.FDFS_FILE_EXT_NAME_MAX_LEN + buffer.Length;
                    byte[] bodyBuffer = new byte[length];
                    bodyBuffer[0] = storePathIndex;
                    byte[] fileSizeBuffer = Util.LongToBuffer(buffer.Length);
                    Array.Copy(fileSizeBuffer, 0, bodyBuffer, 1, fileSizeBuffer.Length);
                    Array.Copy(extensionBuffer, 0, bodyBuffer, 1 + Protocol.FDFS_PROTO_PKG_LEN_SIZE, extensionBuffer.Length);
                    Array.Copy(contentBuffer, 0, bodyBuffer, 1 + Protocol.FDFS_PROTO_PKG_LEN_SIZE + Protocol.FDFS_FILE_EXT_NAME_MAX_LEN, contentBuffer.Length);
                    //Util.InitializeBuffer(bufferSize, 0);
                    //bufferSize[0] = (byte)storageConnection.Index;
                    //byte[] hexBuffer = Util.LongToBuffer(metadatasBuffer.Length);
                    //Array.Copy(hexBuffer, 0, bufferSize, 1, hexBuffer.Length);
                    //hexBuffer = Util.LongToBuffer(length);
                    //Array.Copy(hexBuffer, 0, bufferSize, 1 + Protocol.TRACKER_PROTO_PKG_LEN_SIZE, hexBuffer.Length);

                    byte[] header = Util.PackHeader(Protocol.STORAGE_PROTO_CMD_UPLOAD_FILE, length,0);

                    PackageInfo pkgInfo = null;
                    if (!storageConnection.Connected)
                    {
                        storageConnection.Connect();

                    }

                    NetworkStream outStream = storageConnection.GetStream();

                    outStream.Write(header, 0, header.Length);
                    outStream.Write(bodyBuffer, 0, bodyBuffer.Length);
                    //outStream.Write(extensionBuffer, 0, extensionBuffer.Length);
                    //outStream.Write(metadatasBuffer, 0, metadatasBuffer.Length);
                    if (stream != null)
                    {
                       int readBytes;
                        byte[] buff = new byte[128 * 1024];

                        while ((readBytes = Util.ReadInput(stream, buff, 0, buff.Length)) >= 0)
                        {
                            if (readBytes == 0) continue;
                            outStream.Write(buff, 0, readBytes);
                            totalBytes += readBytes;
                        }
                    }
                    else
                    {
                        outStream.Write(buffer, 0, buffer.Length);
                    }

                    pkgInfo = Util.RecvPackage(outStream, Protocol.STORAGE_PROTO_CMD_RESP, -1, "storage");

                    if (pkgInfo.ErrorNo != 0) return null;

                    if (pkgInfo.Body.Length <= Protocol.FDFS_GROUP_NAME_MAX_LEN)
                        throw new Exception(string.Format("_body length: {0} <= {1}",
                                                          pkgInfo.Body.Length, Protocol.FDFS_GROUP_NAME_MAX_LEN));

                    char[] chars = Util.ToCharArray(pkgInfo.Body);
                    string newGroupName = new string(chars, 0, Protocol.FDFS_GROUP_NAME_MAX_LEN).Trim();
                    string remoteFileName = new string(chars, Protocol.FDFS_GROUP_NAME_MAX_LEN,
                                                       pkgInfo.Body.Length - Protocol.FDFS_GROUP_NAME_MAX_LEN);
                    string[] results = new string[]
                                          {
                                               newGroupName, remoteFileName,storageConnection.IpAddress
                                           };
                    return results;
                }
                catch (Exception exc)
                {
                    try
                    {
                        storageConnection.GetStream().Close();
                    }
                    catch
                    {

                    }
                    if (null != _logger)
                    {
                        _logger.Error(string.Format("上传文件发生异常！异常类型:{0},详细信息:{1}!", exc.InnerException.GetType(), exc));
                    }
                    storageConnection.Close();
                    isGoing = true;
                }
                finally
                {
                    storageConnection.Close(false, true);
                }
            } while (isGoing);
            return null;
        }
    }
}
