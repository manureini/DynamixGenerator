using System;

namespace DynamixGenerator
{
    public class DynamixReferenceProperty : DynamixProperty
    {
        public virtual Type ReferencedType { get; set; }

        public virtual bool IsOneToMany { get; set; }
    }
}
