﻿namespace FastDFS.Client.Component
{
    public class NameValuePair
    {
        private string _name;
        private string _value;

        public NameValuePair()
        {
        }

        /// <summary>
        /// 初始化<see cref="NameValuePair"/> 对象.
        /// </summary>
        /// <param name="name">Metedata的名称.</param>
        public NameValuePair(string name)
        {
            _name = name;
        }

        /// <summary>
        /// 初始化<see cref="NameValuePair"/> 对象.
        /// </summary>
        /// <param name="name">Mmetedata的名称.</param>
        /// <param name="value">Metedata的值.</param>
        public NameValuePair(string name, string value)
        {
            _name = name;
            _value = value;
        }

        /// <summary>
        /// 得到或者设置Metedata的名称
        /// </summary>
        /// <value>Metedata的名称.</value>
        public virtual string Name
        {
            get { return _name; }

            set { _name = value; }
        }

        /// <summary>
        /// 得到或者设置Metedata的值.
        /// </summary>
        /// <value>Metedata的值.</value>
        public virtual string Value
        {
            get { return _value; }

            set { _value = value; }
        }
    }
}
