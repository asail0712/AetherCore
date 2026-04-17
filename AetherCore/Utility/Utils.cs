using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace AetherCore.Utility
{
    public sealed class TypeWithAttribute<TAttr> where TAttr : Attribute
    {
        public required Type Implementation { get; init; }
        public required TAttr Attribute { get; init; }
    }

    public static class Utils
    {
        /// <summary>
        /// 計算輸入字串與 salt 混合後的 SHA256 雜湊值
        /// </summary>
        /// <param name="rawData">原始字串</param>
        /// <param name="salt">加鹽字串</param>
        /// <returns>SHA256 雜湊值的十六進位字串</returns>
        public static string ComputeSha256Hash(string rawData, string salt = "")
        {
            // 將原始資料與 salt 合併
            string saltedData = rawData + salt;

            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes            = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(saltedData));
                StringBuilder builder   = new StringBuilder();

                // 將雜湊的 byte 陣列轉成十六進位字串
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public static bool FindAssemblyByName(string[]? assemblyNames, ref List<Assembly> assemblies)
        {
            if (assemblyNames == null || assemblyNames.Length == 0 || assemblies == null)
                return false;

            foreach(var assemblyName in assemblyNames)
            {
                var assembly = Assembly.Load(assemblyName);

                assemblies.Add(assembly);
            }

            return true;
        }

        public static IEnumerable<TypeWithAttribute<T>> FindAllTypeWithAttribute<T>(IEnumerable<Assembly> assemblies)
            where T : Attribute
        {
            return assemblies
                        .SelectMany(a =>
                        {
                            try
                            {
                                return a.GetTypes();
                            }
                            catch (ReflectionTypeLoadException ex)
                            {
                                // ex.Types: Type?[] -> 過濾 null + 轉成 IEnumerable<Type>
                                return ex.Types.OfType<Type>();
                            }
                        })
                        .Where(t => t is { IsClass: true, IsAbstract: false })
                        .Select(t => new TypeWithAttribute<T>
                        {
                            Implementation  = t,
                            Attribute       = t.GetCustomAttribute<T>()!  // 先取出來
                        })
                        .Where(x => x.Attribute != null); // 過濾沒標記的
        }
    }
}
