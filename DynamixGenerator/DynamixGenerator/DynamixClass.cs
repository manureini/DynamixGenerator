using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DynamixGenerator
{
    public class DynamixClass
    {
        [Key]
        public virtual Guid Id { get; set; }

        public virtual string Name { get; set; }

        public virtual string Namespace { get; set; }

        public virtual ICollection<DynamixProperty> Properties { get; set; }

        public Type GetTypeReference()
        {
            return new DynamixType(Id, Namespace, Name);
        }
    }
}
