using System;

namespace ImageProcessor.Admin.Models
{
    internal class ExceptionThrownEventArgs<T> : EventArgs
    {
        private readonly T _request;
        private readonly Exception _exceptionObject;

        public ExceptionThrownEventArgs(T request, Exception exceptionObject)
        {
            _request = request;
            _exceptionObject = exceptionObject;
        }

        public T Request
        {
            get { return _request; }
        }

        public Exception ExceptionObject
        {
            get { return _exceptionObject; }
        }

        public bool Rethrow { get; set; }
    }
}