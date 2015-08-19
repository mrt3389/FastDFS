namespace FastDFS.Client.Component
{
    public class HeaderInfo
    {
        private long _length;

        /// <summary>
        /// Get or set header info length
        /// </summary>
        /// <value>The header info length.</value>
        public long Length
        {
            set { _length = value; }
            get { return _length; }
        }

        private byte _errorNo;

        /// <summary>
        /// Get or set server error NO.
        /// </summary>
        /// <value>error NO..</value>
        public byte ErrorNo
        {
            set { _errorNo = value; }
            get { return _errorNo; }
        }

        /// <summary>
        /// Constructor <see cref="HeaderInfo"/>.
        /// </summary>
        /// <param name="errorNo">Error NO..</param>
        /// <param name="length">The header info length.</param>
        public HeaderInfo(byte errorNo, long length)
        {
            _errorNo = errorNo;
            _length = length;
        }
    }
}
