using System.Collections.Generic;
using System.Text;

namespace DynamixGenerator
{
    public static class DynamixGenerator
    {
        public static string GenerateCode(string pAssemblyName, IEnumerable<DynamixClass> pDynamixClasses)
        {
            StringBuilder sb = new StringBuilder();

            foreach (var dynClass in pDynamixClasses)
            {
                sb.AppendLine($"namespace {dynClass.Namespace ?? pAssemblyName}");
                sb.AppendLine("{"); //namespace

                sb.AppendLine("[global::System.CodeDom.Compiler.GeneratedCode(\"Dynamix\", \"1.0\")]");
                sb.AppendLine($"[global::DynamixGenerator.DynamixId(\"{dynClass.Id}\")]");
                sb.AppendLine($"public class {dynClass.Name}");
                sb.AppendLine("{"); //class

                foreach (var property in dynClass.Properties)
                {
                    string typeName = property.Type.FullName;

                    if (property.Type is DynamixType dt && dt.Namespace == null)
                    {
                        typeName = pAssemblyName + "." + dt.Name;
                    }

                    sb.AppendLine($@"public global::{typeName} {property.Name} {{ get; set; }}");

                    if (property.DefaultCode != null)
                    {
                        sb.Append(" = ");
                        sb.Append(property.DefaultCode);
                        sb.Append(";");
                    }
                }

                sb.AppendLine("}"); //class
                sb.AppendLine("}"); //namespace

                sb.AppendLine();
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
