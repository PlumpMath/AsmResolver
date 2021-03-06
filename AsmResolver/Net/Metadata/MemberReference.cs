﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using AsmResolver.Net.Builder;
using AsmResolver.Net.Signatures;

namespace AsmResolver.Net.Metadata
{
    public class MemberReferenceTable : MetadataTable<MemberReference> {
        public override MetadataTokenType TokenType
        {
            get { return MetadataTokenType.MemberRef; }
        }

        public override uint GetElementByteCount()
        {
            return (uint)TableStream.GetIndexEncoder(CodedIndex.MemberRefParent).IndexSize +
                   (uint)TableStream.StringIndexSize +
                   (uint)TableStream.BlobIndexSize;
        }

        protected override MemberReference ReadMember(MetadataToken token, ReadingContext context)
        {
            var reader = context.Reader;
            return new MemberReference(Header, token, new MetadataRow<uint, uint, uint>()
            {
                Column1 = reader.ReadIndex(TableStream.GetIndexEncoder(CodedIndex.MemberRefParent).IndexSize),
                Column2 = reader.ReadIndex(TableStream.StringIndexSize),
                Column3 = reader.ReadIndex(TableStream.BlobIndexSize)
            });
        }

        protected override void UpdateMember(NetBuildingContext context, MemberReference member)
        {
            var row = member.MetadataRow;
            row.Column1 = TableStream.GetIndexEncoder(CodedIndex.MemberRefParent)
                .EncodeToken(member.Parent.MetadataToken);
            row.Column2 = context.GetStreamBuffer<StringStreamBuffer>().GetStringOffset(member.Name);
            row.Column3 = context.GetStreamBuffer<BlobStreamBuffer>().GetBlobOffset(member.Signature);
        }

        protected override void WriteMember(WritingContext context, MemberReference member)
        {
            var writer = context.Writer;
            var row = member.MetadataRow;

            writer.WriteIndex(TableStream.GetIndexEncoder(CodedIndex.MemberRefParent).IndexSize, row.Column1);
            writer.WriteIndex(TableStream.StringIndexSize, row.Column2);
            writer.WriteIndex(TableStream.BlobIndexSize, row.Column3);
        }
    }

    public class MemberReference : MetadataMember<MetadataRow<uint, uint, uint>>, ICustomAttributeType, ICallableMemberReference
    {
        private readonly LazyValue<string> _name;
        private readonly LazyValue<MemberSignature> _signature;
        private readonly LazyValue<IMemberRefParent> _parent;
        private string _fullName;
        private CustomAttributeCollection _customAttributes;

        public MemberReference(IMemberRefParent parent, string name, MemberSignature signature)
            : base(null, new MetadataToken(MetadataTokenType.MemberRef), new MetadataRow<uint, uint, uint>())
        {
            _parent = new LazyValue<IMemberRefParent>(parent);
            _name = new LazyValue<string>(name);
            _signature = new LazyValue<MemberSignature>(signature);
        }

        internal MemberReference(MetadataHeader header, MetadataToken token, MetadataRow<uint, uint, uint> row)
            : base(header, token, row)
        {
            var tableStream = header.GetStream<TableStream>();

            _parent = new LazyValue<IMemberRefParent>(() =>
            {
                var parentToken = tableStream.GetIndexEncoder(CodedIndex.MemberRefParent).DecodeIndex(row.Column1);
                return parentToken.Rid != 0 ? (IMemberRefParent)tableStream.ResolveMember(parentToken) : null;
            });

            _name = new LazyValue<string>(() => header.GetStream<StringStream>().GetStringByOffset(row.Column2));

            _signature = new LazyValue<MemberSignature>(() => 
                CallingConventionSignature.FromReader(header, header.GetStream<BlobStream>().CreateBlobReader(row.Column3)) as MemberSignature);
        }

        public IMemberRefParent Parent
        {
            get { return _parent.Value; }
            set { _parent.Value = value; }
        }

        public string Name
        {
            get { return _name.Value; }
            set
            {
                _name.Value = value;
                _fullName = null;
            }
        }

        public string FullName
        {
            get { return _fullName ?? (_fullName = this.GetFullName(Signature)); }
        }

        public ITypeDefOrRef DeclaringType
        {
            get
            {
                var declaringType = Parent as ITypeDefOrRef;
                if (declaringType != null)
                    return declaringType;

                var method = Parent as MethodDefinition;
                if (method != null)
                    return method.DeclaringType;
                
                // TODO: handle modulereference parent

                return null;
            }
        }

        public MemberSignature Signature
        {
            get { return _signature.Value; }
            set
            {
                _signature.Value = value;
                _fullName = null;
            }
        }

        CallingConventionSignature ICallableMemberReference.Signature
        {
            get { return Signature; }
        }

        public CustomAttributeCollection CustomAttributes
        {
            get
            {
                if (_customAttributes != null)
                    return _customAttributes;
                _customAttributes = new CustomAttributeCollection(this);
                return _customAttributes;
            }
        }
        
        public override string ToString()
        {
            return FullName;
        }

        public IMetadataMember Resolve()
        {
            if (Header == null || Header.MetadataResolver == null || Signature == null)
                throw new MemberResolutionException(this);

            return Signature.IsMethod
                ? (IMetadataMember)Header.MetadataResolver.ResolveMethod(this)
                : Header.MetadataResolver.ResolveField(this);
        }
    }
}
