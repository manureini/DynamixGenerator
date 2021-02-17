﻿using System;

namespace DynamixGenerator
{
    public class DynamixProperty
    {
        private Type mType;

        public virtual Guid Id { get; set; }

        public virtual string Name { get; set; }

        public virtual string DefaultCode { get; set; }

        public virtual Type Type
        {
            get
            {
                if (mType != null)
                    return mType;

                mType = Type.GetType(TypeName);
                return mType;
            }
            set
            {
                mType = value;
                TypeName = mType.AssemblyQualifiedName;
            }
        }

        public virtual string TypeName { get; set; }

        public virtual DynamixClass DynamixClass { get; set; }

        public virtual bool IsReference { get; set; }

        public virtual bool IsOneToMany { get; set; }

        public virtual bool IsUnique { get; set; }

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
