using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamixGenerator
{
    internal static class TypeHelper
    {
        public static Type FindType(string pTypeName)
        {
            var type = Type.GetType(pTypeName);

            if (type != null)
                return type;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetTypes().FirstOrDefault(t => t.FullName == pTypeName);

                if (type != null)
                    return type;
            }

            return null;
        }
    }
}
