using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICard_API_Project
{
    public class DatabaseSyncException : Exception
    {
        // Pass the message and the original SQL crash up the chain
        public DatabaseSyncException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
