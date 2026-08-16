using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Utility.Notifications;

namespace Utility.Tests.Compatibility
{
    internal static class AssemblyMetadata
    {
        internal static bool HasMemberReference(
            string ownerNamespace,
            string ownerName,
            string memberName,
            int? parameterCount = null
        )
        {
            using FileStream stream = File.OpenRead(typeof(Toast).Assembly.Location);
            using var peReader = new PEReader(stream);
            MetadataReader metadata = peReader.GetMetadataReader();

            foreach (MemberReferenceHandle handle in metadata.MemberReferences)
            {
                MemberReference member = metadata.GetMemberReference(handle);
                if (metadata.GetString(member.Name) != memberName)
                    continue;
                if (member.Parent.Kind != HandleKind.TypeReference)
                    continue;

                TypeReference owner = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
                if (
                    metadata.GetString(owner.Namespace) != ownerNamespace
                    || metadata.GetString(owner.Name) != ownerName
                )
                {
                    continue;
                }

                if (parameterCount == null)
                    return true;

                BlobReader signature = metadata.GetBlobReader(member.Signature);
                SignatureHeader header = signature.ReadSignatureHeader();
                if (header.Kind != SignatureKind.Method)
                    continue;
                if (header.IsGeneric)
                    signature.ReadCompressedInteger();

                if (signature.ReadCompressedInteger() == parameterCount.Value)
                    return true;
            }

            return false;
        }

        internal static bool HasMemberReferenceNamed(string memberName)
        {
            using FileStream stream = File.OpenRead(typeof(Toast).Assembly.Location);
            using var peReader = new PEReader(stream);
            MetadataReader metadata = peReader.GetMetadataReader();

            foreach (MemberReferenceHandle handle in metadata.MemberReferences)
            {
                MemberReference member = metadata.GetMemberReference(handle);
                if (metadata.GetString(member.Name) == memberName)
                    return true;
            }

            return false;
        }

        internal static bool HasAssemblyReference(string assemblyName)
        {
            using FileStream stream = File.OpenRead(typeof(Toast).Assembly.Location);
            using var peReader = new PEReader(stream);
            MetadataReader metadata = peReader.GetMetadataReader();

            foreach (AssemblyReferenceHandle handle in metadata.AssemblyReferences)
            {
                AssemblyReference reference = metadata.GetAssemblyReference(handle);
                if (metadata.GetString(reference.Name) == assemblyName)
                    return true;
            }

            return false;
        }
    }
}
