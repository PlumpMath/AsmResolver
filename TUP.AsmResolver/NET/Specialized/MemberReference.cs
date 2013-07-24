﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TUP.AsmResolver.NET.Specialized
{
    public abstract class MemberReference : MetaDataMember
    {
        PInvokeImplementation pinvokeimpl = null;
        CustomAttribute[] customAttributes = null;
        internal TypeReference declaringType = null;

        public MemberReference(MetaDataRow row)
            : base(row)
        {
        }

        public abstract string Name { get; }
        public abstract string FullName { get; }
        public virtual TypeReference DeclaringType
        {
            get
            {
                if (declaringType == null)
                {
                    netheader.TablesHeap.MemberRefParent.TryGetMember(Convert.ToInt32(metadatarow._parts[0]), out declaringType);
                }
                return declaringType;
            }
        }

        public override string ToString()
        {
            return FullName;
        }

        public virtual CustomAttribute[] CustomAttributes
        {
            get
            {
                if (customAttributes == null && netheader.TablesHeap.HasTable(MetaDataTableType.CustomAttribute))
                {
                    List<CustomAttribute> customattributes = new List<CustomAttribute>();

                    foreach (var member in netheader.TablesHeap.GetTable(MetaDataTableType.CustomAttribute).Members)
                    {
                        CustomAttribute attribute = member as CustomAttribute;
                        if (attribute.Parent != null && attribute.Parent.metadatatoken == this.metadatatoken)
                            customattributes.Add(attribute);
                    }

                    customAttributes = customattributes.ToArray();
                }
                return customAttributes;
            }
        }

        public PInvokeImplementation PInvokeImplementation
        {
            get
            {
                if (pinvokeimpl == null && netheader.TablesHeap.HasTable(MetaDataTableType.ImplMap))
                {
                    foreach (var member in netheader.TablesHeap.GetTable(MetaDataTableType.ImplMap).Members)
                    {
                        PInvokeImplementation implementation = member as PInvokeImplementation;
                        if (implementation.Member.metadatatoken == this.metadatatoken)
                        {
                            pinvokeimpl = implementation;
                            break;
                        }
                    }
                }
                return pinvokeimpl;
            }
        }

        public bool HasCustomAttributes
        {
            get
            {
                return CustomAttributes != null && CustomAttributes.Length > 0;
            }
        }

        public override void ClearCache()
        {
            customAttributes = null;
            declaringType = null;
            pinvokeimpl = null;
        }

        public override void LoadCache()
        {
            pinvokeimpl = PInvokeImplementation;
            customAttributes = CustomAttributes;
            declaringType = DeclaringType;
        }
    }
}
