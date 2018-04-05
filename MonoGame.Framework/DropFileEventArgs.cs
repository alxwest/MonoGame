using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Xna.Framework
{
    public class DropFileEventArgs : EventArgs
    {
        public DropFileEventArgs(string filePath)
        {
            this.FilePath = filePath;
        }

        public string FilePath {
            get; private set;
        }
    }
}
