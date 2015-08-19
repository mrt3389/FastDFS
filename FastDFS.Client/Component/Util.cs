﻿using System;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using log4net;
using System.Security.Cryptography;

namespace FastDFS.Client.Component
{
    public sealed class Util
    {
        private static readonly ILog _logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        /// <summary>
        /// 将head命令转换成二进制
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="pkgLength"></param>
        /// <param name="errno"></param>
        /// <returns></returns>
        public static byte[] PackHeader(byte cmd, long pkgLength, byte errno)
        {
            
            byte[] headerBuffer = new byte[Protocol.FDFS_PROTO_PKG_LEN_SIZE + 2];
            headerBuffer = InitializeBuffer(headerBuffer, 0);
            byte[] hexs = LongToBuffer(pkgLength);
            Array.Copy(hexs, 0, headerBuffer, 0, hexs.Length);
            headerBuffer[Protocol.FDFS_PROTO_PKG_LEN_SIZE] = cmd;
            headerBuffer[Protocol.FDFS_PROTO_PKG_LEN_SIZE+1] = errno;
            return headerBuffer;
        }


        /// <summary>
        /// 接受从服务器传回客户端的信息.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="cmd">The CMD.</param>
        /// <param name="bodyLength">Length of the body.</param>
        /// <returns></returns>
        public static HeaderInfo RecvHeader(NetworkStream stream, byte cmd, long bodyLength, string source)
        {
            byte[] headerBuffer = new byte[Protocol.FDFS_PROTO_PKG_LEN_SIZE + 2];
            int bytes;

            if (headerBuffer.Length != (bytes = ReadInput(stream, headerBuffer, 0, headerBuffer.Length)))
            {
                string message = string.Format("服务器端发送的包大小和头大小不相等.包大小为：{0},头大小为：{1}", bytes, headerBuffer.Length);
                if (null != _logger) _logger.Error(message);
                throw new IOException(message);
            }

            if (cmd != headerBuffer[Protocol.FDFS_PROTO_PKG_LEN_SIZE])
            {
                string message = string.Format("来源：{0},服务器接收的命令不符合协议规定。接收协议为:{1},期望协议为:{2}",
                                               source, headerBuffer[Protocol.FDFS_PROTO_PKG_LEN_SIZE], cmd);
                if (null != _logger) _logger.Error(message);
                throw new IOException(message);
            }

            if (0 != headerBuffer[Protocol.FDFS_PROTO_PKG_LEN_SIZE+1])
            {
                return new HeaderInfo(headerBuffer[Protocol.FDFS_PROTO_PKG_LEN_SIZE+1], 0);
            }

            long pkgLength = BufferToLong(headerBuffer, 0);
            if (0 > pkgLength)
            {
                string message = string.Format("服务器发送的数据流长度不符合规定。数据流长度为:{0}", pkgLength);
                if (null != _logger) _logger.Error(message);
                throw new IOException(message);
            }

            if (0 <= bodyLength && pkgLength != bodyLength)
            {
                string message = string.Format("服务器发送的数据流长度不符合协议规定。现数据流长度为:{0},协议规定数据流长度为：{1}", pkgLength, bodyLength);
                if (null != _logger) _logger.Error(message);
                throw new IOException(message);
            }

            return new HeaderInfo(0, pkgLength);
        }

        /// <summary>
        /// 接收从服务器传回客户端的信息
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="cmd">The CMD.</param>
        /// <param name="bodyLength">Length of the body.</param>
        /// <returns></returns>
        public static PackageInfo RecvPackage(NetworkStream stream, byte cmd, long bodyLength, string source)
        {
            HeaderInfo header = RecvHeader(stream, cmd, bodyLength, source);
            if (header.ErrorNo != 0) return new PackageInfo(header.ErrorNo, null);

            byte[] body = new byte[(int)header.Length];
            int totalBytes = 0;
            int remainBytes = (int)header.Length;
            int bytes;

            while (totalBytes < header.Length)
            {
                if ((bytes = ReadInput(stream, body, totalBytes, remainBytes)) < 0) break;

                totalBytes += bytes;
                remainBytes -= bytes;
            }

            if (totalBytes != header.Length)
                throw new IOException(string.Format("Recv package size {0} != {1}", totalBytes, header.Length));

            return new PackageInfo(0, body);
        }


        /// <summary>
        /// 取得元数据
        /// </summary>
        /// <param name="metadata">The meta_buff.</param>
        /// <returns></returns>
        public static NameValuePair[] SplitMetadata(string metadata)
        {
            return SplitMetadata(metadata, Protocol.FDFS_RECORD_SEPERATOR, Protocol.FDFS_FIELD_SEPERATOR);
        }

        /// <summary>
        /// 取得元数据.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="recordSeperator">The record seperator.</param>
        /// <param name="filedSeperator">The filed seperator.</param>
        /// <returns></returns>
        public static NameValuePair[] SplitMetadata(string buffer,
                                                    string recordSeperator, string filedSeperator)
        {
            string[] cols;
            NameValuePair[] nameValuePairs;

            string[] rows = buffer.Split(new string[] { recordSeperator }, StringSplitOptions.RemoveEmptyEntries);
            nameValuePairs = new NameValuePair[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                cols = rows[i].Split(new string[] { filedSeperator }, 2, StringSplitOptions.RemoveEmptyEntries);
                nameValuePairs[i] = new NameValuePair(cols[0]);
                if (cols.Length == 2)
                {
                    nameValuePairs[i].Value = (cols[1]);
                }
            }

            return nameValuePairs;
        }

        /// <summary>
        /// 将元数据和转换成协议规定格式
        /// </summary>
        /// <param name="nameValuePair">The name value pair.</param>
        /// <returns></returns>
        public static string PackMetadata(NameValuePair[] nameValuePair)
        {
            if (nameValuePair.Length == 0)
            {
                return "";
            }

            StringBuilder sb = new StringBuilder(32 * nameValuePair.Length);
            sb.Append(nameValuePair[0].Name).Append(Protocol.FDFS_FIELD_SEPERATOR).Append(nameValuePair[0].Value);
            for (int i = 1; i < nameValuePair.Length; i++)
            {
                sb.Append(Protocol.FDFS_RECORD_SEPERATOR);
                sb.Append(nameValuePair[i].Name).Append(Protocol.FDFS_FIELD_SEPERATOR).Append(nameValuePair[i].Value);
            }

            return sb.ToString();
        }

        /// <summary>
        /// 将long数据类型转换成二进制
        /// </summary>
        /// <param name="n">The n.</param>
        /// <returns></returns>
        public static byte[] LongToBuffer(long n)
        {
            byte[] buffer = new byte[8];
            buffer[0] = (byte)((n >> 56) & 0xFF);
            buffer[1] = (byte)((n >> 48) & 0xFF);
            buffer[2] = (byte)((n >> 40) & 0xFF);
            buffer[3] = (byte)((n >> 32) & 0xFF);
            buffer[4] = (byte)((n >> 24) & 0xFF);
            buffer[5] = (byte)((n >> 16) & 0xFF);
            buffer[6] = (byte)((n >> 8) & 0xFF);
            buffer[7] = (byte)(n & 0xFF);

            return buffer;
        }

        /// <summary>
        /// 将二进制转换成long数据类型
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <returns></returns>
        public static long BufferToLong(byte[] buffer, int offset)
        {
            return (((long)(buffer[offset] >= 0 ? buffer[offset] : 256 + buffer[offset])) << 56) |
                   (((long)(buffer[offset + 1] >= 0 ? buffer[offset + 1] : 256 + buffer[offset + 1])) << 48) |
                   (((long)(buffer[offset + 2] >= 0 ? buffer[offset + 2] : 256 + buffer[offset + 2])) << 40) |
                   (((long)(buffer[offset + 3] >= 0 ? buffer[offset + 3] : 256 + buffer[offset + 3])) << 32) |
                   (((long)(buffer[offset + 4] >= 0 ? buffer[offset + 4] : 256 + buffer[offset + 4])) << 24) |
                   (((long)(buffer[offset + 5] >= 0 ? buffer[offset + 5] : 256 + buffer[offset + 5])) << 16) |
                   (((long)(buffer[offset + 6] >= 0 ? buffer[offset + 6] : 256 + buffer[offset + 6])) << 8) |
                   ((buffer[offset + 7] >= 0 ? buffer[offset + 7] : 256 + buffer[offset + 7]));
        }


        /// <summary>
        /// Fills the array with an specific value from an specific index to an specific index.
        /// </summary>
        /// <param _name="array">The array to be filled.</param>
        /// <param _name="fromindex">The first index to be filled.</param>
        /// <param _name="toindex">The last index to be filled.</param>
        /// <param _name="val">The value to fill the array with.</param>
        public static void Fill(Array array, Int32 fromindex, Int32 toindex, Object val)
        {
            Object temp = val;
            Type elementtype = array.GetType().GetElementType();
            if (elementtype != val.GetType())
                temp = Convert.ChangeType(val, elementtype);
            if (array.Length == 0)
                throw (new NullReferenceException());
            if (fromindex > toindex)
                throw (new ArgumentException());
            if ((fromindex < 0) || (array).Length < toindex)
                throw (new IndexOutOfRangeException());
            for (int index = (fromindex > 0) ? fromindex-- : fromindex; index < toindex; index++)
                array.SetValue(temp, index);
        }

        /// <summary>
        /// Fills the array with an specific value.
        /// </summary>
        /// <param _name="array">The array to be filled.</param>
        /// <param _name="val">The value to fill the array with.</param>
        public static void Fill(Array array, Object val)
        {
            Fill(array, 0, array.Length, val);
        }

        public static int ReadInput(Stream sourceStream, byte[] target, int start, int count)
        {
            if (target.Length == 0)
                return 0;

            byte[] receiver = new byte[target.Length];

            int bytesRead = sourceStream.Read(receiver, start, count);

            if (bytesRead == 0)
                return -1;

            for (int i = start; i < start + bytesRead; i++)
                target[i] = receiver[i];

            return bytesRead;
        }

        public static byte[] InitializeBuffer(byte[] buffer, byte by)
        {
            for (long i = 0; i < buffer.LongLength; i++)
            {
                buffer[i] = by;
            }
            return buffer;

        }

        /// <summary>
        /// Converts an array of bytes to an array of chars
        /// </summary>
        /// <param _name="byteArray">The array of bytes to convert</param>
        /// <returns>The new array of chars</returns>
        public static char[] ToCharArray(byte[] byteArray)
        {
            return Encoding.UTF8.GetChars(byteArray);
        }

        public static string GetFilePath(string fileName)
        {
            int index = fileName.IndexOf("/M00/");//处理标识符
            return index > -1 ? fileName.Remove(0, index + 4) : fileName;
        }

        public static byte[] StringToByte(string input)
        {
            return Encoding.UTF8.GetBytes(input);
        }
        /// <summary>
        /// get token for file URL
        /// </summary>
        /// <param name="file_id">file_id the file id return by FastDFS server</param>
        /// <param name="ts">ts unix timestamp, unit: second</param>
        /// <param name="secret_key">secret_key the secret key</param>
        /// <returns>token string</returns>

        public static string GetToken(string file_id, int ts, string secret_key)
        {
            byte[] bsFileId = StringToByte(file_id);
            byte[] bsKey = StringToByte(secret_key);
            byte[] bsTimestamp = StringToByte(ts.ToString());

            byte[] buff = new byte[bsFileId.Length + bsKey.Length + bsTimestamp.Length];
            Array.Copy(bsFileId, 0, buff, 0, bsFileId.Length);
            Array.Copy(bsKey, 0, buff, bsFileId.Length, bsKey.Length);
            Array.Copy(bsTimestamp, 0, buff, bsFileId.Length + bsKey.Length, bsTimestamp.Length);

            return md5(buff);
        }
        /// <summary>
        /// md5 function
        /// </summary>
        /// <param name="source">source the input buffer </param>
        /// <returns>md5 string </returns>
        public static string md5(byte[] source)
        {

            string pwd = "";
            MD5 md5 = MD5.Create();//实例化一个md5对像
                                   // 加密后是一个字节类型的数组，这里要注意编码UTF8/Unicode等的选择　
            byte[] s = md5.ComputeHash(source);
            // 通过使用循环，将字节类型的数组转换为字符串，此字符串是常规字符格式化所得
            for (int i = 0; i < s.Length; i++)
            {
                // 将得到的字符串使用十六进制类型格式。格式后的字符是小写的字母，如果使用大写（X）则格式后的字符是大写字符 
                pwd = pwd + s[i].ToString("X2");
                int len = pwd.Length;

            }
            return pwd.ToLower();
        }

    }
}
