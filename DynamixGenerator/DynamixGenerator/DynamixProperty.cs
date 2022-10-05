using MShared;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace DynamixGenerator
{
    [Index(isUnique: true, nameof(DynamixProperty.Name), nameof(DynamixProperty.DynamixClassId))]
    public class DynamixProperty
    {
        private Type mType;

        public virtual Guid Id { get; set; }

        public virtual string Name { get; set; }

        public virtual string DefaultCode { get; set; }

        [NotMapped]
        public virtual Type Type
        {
            get
            {
                if (mType != null)
                    return mType;

                mType = TypeHelper.FindType(TypeName);

                //Dynamix Types can't be found, because they don't exists, yet
                if (mType == null && DynamixClass != null && !TypeName.StartsWith(DynamixClass.Namespace))
                {
                    throw new Exception($"Type with name {TypeName} not found!");
                }

                return mType;
            }
            set
            {
                mType = value;
                TypeName = mType.FullName;
            }
        }

        public virtual string TypeName { get; set; }

        public virtual Guid DynamixClassId { get; set; }
        [ForeignKey(nameof(DynamixClassId))]
        public virtual DynamixClass DynamixClass { get; set; }

        public virtual bool IsReference { get; set; }

        public virtual bool IsOneToMany { get; set; }

        public virtual bool IsUnique { get; set; }

        public virtual string Formula { get; set; }

        public virtual string ReferencedPropertyName { get; set; }

        public virtual string AttributeCode { get; set; }

        public virtual string GetPropertyTypeName()
        {
            return Type?.FullName ?? TypeName;
        }

        public virtual void UpdateTypeReferenceFromClass(Type pClassType)
        {
            Type = pClassType.GetProperty(Name).PropertyType;

            if (IsOneToMany)
            {
                Type = Type.GetGenericArguments()[0];
            }
        }
    }
}
