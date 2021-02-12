using System.Collections.Generic;
using System.IO;
using Bali.Attributes;
using Bali.Attributes.Builders;
using Bali.Constants;
using Bali.IO;

namespace Bali
{
    /// <summary>
    /// Represents a single <c>class</c> or <c>interface</c>.
    /// </summary>
    public class ClassFile
    {
        /// <summary>
        /// Gets or sets the magic value.
        /// </summary>
        /// <remarks>This should always have the value <b>0xCAFEBABE</b>.</remarks>
        public uint Magic
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the minor version of this <see cref="ClassFile"/>.
        /// </summary>
        public ushort MinorVersion
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the major version of this <see cref="ClassFile"/>.
        /// </summary>
        public ushort MajorVersion
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the <see cref="ConstantPool"/>.
        /// </summary>
        public ConstantPool Constants
        {
            get;
            private set;
        } = new(new List<Constant>());
        
        /// <summary>
        /// Gets or sets the <see cref="AccessFlags"/>.
        /// </summary>
        public ClassAccessFlags AccessFlags
        {
            get;
            set;
        }
        
        /// <summary>
        /// Gets or sets the index into the <see cref="ConstantPool"/> that describes this class.
        /// </summary>
        public ushort ThisClassIndex
        {
            get;
            set;
        }
        
        /// <summary>
        /// Gets or sets the index into the <see cref="ConstantPool"/> that describes the superclass.
        /// </summary>
        public ushort SuperClassIndex
        {
            get;
            set;
        }
        
        /// <summary>
        /// Gets or sets the indices into the <see cref="Constants"/> representing direct superinterfaces of this class.
        /// </summary>
        public IList<ushort> Interfaces
        {
            get;
            private set;
        } = new List<ushort>();

        /// <summary>
        /// Gets or sets the fields of this class.
        /// </summary>
        public IList<JvmFieldInfo> Fields
        {
            get;
            private set;
        } = new List<JvmFieldInfo>();

        /// <summary>
        /// Gets or sets the methods of this class.
        /// </summary>
        public IList<JvmMethodInfo> Methods
        {
            get;
            private set;
        } = new List<JvmMethodInfo>();

        /// <summary>
        /// Gets or sets the attributes of this class.
        /// </summary>
        public IList<JvmAttribute> Attributes
        {
            get;
            private set;
        } = new List<JvmAttribute>();

        public void Write(string path)
        {
            using var ms = new MemoryStream();
            Write(ms);
            File.WriteAllBytes(path, ms.ToArray());
        }

        public void Write(Stream stream)
        {
            stream.WriteU4(Magic);
            stream.WriteU2(MinorVersion);
            stream.WriteU2(MajorVersion);

            var constantPoolWriter = new ConstantPoolWriter(Constants, stream);
            constantPoolWriter.WriteConstantPool();
            
            stream.WriteU2((ushort) AccessFlags);
            stream.WriteU2(ThisClassIndex);
            stream.WriteU2(SuperClassIndex);
            
            stream.WriteU2((ushort) Interfaces.Count);
            foreach (ushort index in Interfaces)
                stream.WriteU2(index);

            var attributeDirector = new JvmAttributeDirector(Constants);
            stream.WriteU2((ushort) Fields.Count);
            foreach (var field in Fields)
            {
                stream.WriteU2((byte) field.AccessFlags);
                stream.WriteU2(field.NameIndex);
                stream.WriteU2(field.DescriptorIndex);
                stream.WriteU2((ushort) field.Attributes.Count);
                foreach (var attribute in field.Attributes)
                    attributeDirector.ConstructAttribute(attribute, stream);
            }
            
            stream.WriteU2((ushort) Methods.Count);
            foreach (var method in Methods)
            {
                stream.WriteU2((byte) method.AccessFlags);
                stream.WriteU2(method.NameIndex);
                stream.WriteU2(method.DescriptorIndex);
                stream.WriteU2((ushort) method.Attributes.Count);
                foreach (var attribute in method.Attributes)
                    attributeDirector.ConstructAttribute(attribute, stream);
            }
            
            stream.WriteU2((ushort) Attributes.Count);
            foreach (var attribute in Attributes)
                attributeDirector.ConstructAttribute(attribute, stream);
        }

        /// <summary>
        /// Reads and parses a <see cref="ClassFile"/> from the given <paramref name="path"/>.
        /// </summary>
        /// <param name="path">The file's path to read.</param>
        /// <returns>The read and parsed <see cref="ClassFile"/>.</returns>
        public static ClassFile FromFile(string path)
        {
            using var stream = File.OpenRead(path);
            return FromStream(stream);
        }

        /// <summary>
        /// Reads and parses a <see cref="ClassFile"/> from an array of <paramref name="bytes"/>.
        /// </summary>
        /// <param name="bytes">The raw bytes.</param>
        /// <returns>The read and parsed <see cref="ClassFile"/>.</returns>
        public static ClassFile FromBytes(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes);
            return FromStream(stream);
        }

        /// <summary>
        /// Reads and parses a <see cref="ClassFile"/> from the given input <paramref name="stream"/>.
        /// </summary>
        /// <param name="stream">The input <see cref="Stream"/> to read data from.</param>
        /// <returns>The read and parsed <see cref="ClassFile"/>.</returns>
        public static ClassFile FromStream(Stream stream)
        {
            var header = new ClassFileHeaderReader(stream).ReadHeader();
            var constantPool = new ConstantPoolReader(stream, (ushort) (stream.ReadU2() - 1)).ReadConstantPool();
            var body = new ClassFileBodyReader(stream).ReadBody();
            var metadata = new MetadataBodyReader(stream, constantPool).ReadMetadataBody();
            
            return new ClassFile
            {
                Magic = header.Magic,
                MinorVersion = header.Minor,
                MajorVersion = header.Major,
                Constants = constantPool,
                AccessFlags = body.AccessFlags,
                ThisClassIndex = body.ThisClass,
                SuperClassIndex = body.SuperClass,
                Interfaces = metadata.Interfaces,
                Fields = metadata.Fields,
                Methods = metadata.Methods,
                Attributes = metadata.Attributes
            };
        }
    }
}