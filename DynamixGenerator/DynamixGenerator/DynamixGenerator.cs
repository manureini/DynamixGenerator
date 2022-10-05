using System.Collections.Generic;
using System.Text;

namespace DynamixGenerator
{
    public static class DynamixGenerator
    {
        public static string GenerateCode(IEnumerable<DynamixClass> pDynamixClasses)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using System.ComponentModel.DataAnnotations;");

            foreach (var dynClass in pDynamixClasses)
            {
                sb.AppendLine($"namespace {dynClass.Namespace}");
                sb.AppendLine("{"); //namespace

                sb.AppendLine("[global::System.CodeDom.Compiler.GeneratedCode(\"Dynamix\", \"1.0\")]");
                sb.AppendLine($"[global::DynamixGenerator.DynamixId(\"{dynClass.Id}\")]");
                sb.AppendLine($"public class {dynClass.Name}");

                bool inherits = !string.IsNullOrEmpty(dynClass.InheritsFrom);
                bool implements = !string.IsNullOrEmpty(dynClass.Implements);

                if (inherits || implements)
                {
                    sb.AppendLine($" : ");
                }

                if (inherits)
                {
                    sb.AppendLine(dynClass.InheritsFrom);
                }

                if (inherits && implements)
                {
                    sb.AppendLine(", ");
                }

                if (implements)
                {
                    sb.AppendLine(dynClass.Implements);
                }

                sb.AppendLine("{"); //class

                if (!inherits)
                {
                    sb.AppendLine("public virtual global::System.Guid Id { get; set; }");
                }

                foreach (var property in dynClass.Properties)
                {
                    if (!inherits && property.Name == "Id")
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(property.AttributeCode))
                    {
                        sb.AppendLine(property.AttributeCode);
                    }

                    string typename = property.GetPropertyTypeName();

                    if (property.IsOneToMany)
                    {
                        typename = $"System.Collections.Generic.ICollection<{typename}>";
                    }

                    sb.AppendLine($@"public virtual global::{typename} {property.Name} {{ get; set; }}");

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
