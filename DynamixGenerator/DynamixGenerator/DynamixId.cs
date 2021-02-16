using System;

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
