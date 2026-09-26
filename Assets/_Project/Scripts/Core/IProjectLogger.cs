using System;

namespace TheLastWatch.Core
{
    public interface IProjectLogger
    {
        void Info(string message);
        void Warning(string message);
        void Error(string message, Exception exception = null);
    }
}
