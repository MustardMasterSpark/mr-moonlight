using System;

namespace Burntwax
{
    public class InvalidPathSpecifiedException : Exception
    {
        public InvalidPathSpecifiedException(string Attribute) : base($"{Attribute} does not exist at the provided path")
        {

        }
    }

}
