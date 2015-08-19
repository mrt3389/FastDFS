﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FastDFS.Client.Core.Pool
{
    /// <summary>
    /// 对象池化工厂
    /// </summary>
    public interface IPoolableObjectFactory<T>
    {
        /// <summary>
        /// 创建对象
        /// </summary>
        T CreateObject();

        /// <summary>
        /// 销毁对象.
        /// </summary>
        void DestroyObject(T obj);

        /// <summary>
        /// 检查并确保对象的安全
        /// </summary>
        bool ValidateObject(T obj);

        /// <summary>
        /// 激活对象池中的待用对象. 
        /// </summary>
        /// <param name="obj">The obj.</param>
        /// <param name="ipAddress">The ip address.</param>
        /// <param name="port">The port.</param>
        void ActivateObject(T obj, string ipAddress, int port);

        /// <summary>
        /// 卸载内存中正在使用的对象.
        /// </summary>
        void PassivateObject(T obj);

    }
}
