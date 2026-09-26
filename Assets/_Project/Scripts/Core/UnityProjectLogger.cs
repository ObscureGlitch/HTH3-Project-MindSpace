using System;
using UnityEngine;

namespace TheLastWatch.Core
{
    public sealed class UnityProjectLogger : IProjectLogger
    {
        private const string Prefix = "[The Last Watch] ";
        private readonly bool _verbose;

        public UnityProjectLogger(bool verbose)
        {
            _verbose = verbose;
        }

        public void Info(string message)
        {
            if (_verbose)
            {
                Debug.Log(Prefix + message);
            }
        }

        public void Warning(string message)
        {
            Debug.LogWarning(Prefix + message);
        }

        public void Error(string message, Exception exception = null)
        {
            Debug.LogError(Prefix + message);
            if (exception != null)
            {
                Debug.LogException(exception);
            }
        }
    }
}
