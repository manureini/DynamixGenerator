using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamixGenerator
{
    public class DynamixId : Attribute
    {
        public Guid Id {get; protected set;}

        public DynamixId(string pId)
        {
            Id = Guid.Parse(pId);
        }
    }
}
